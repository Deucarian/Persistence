using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Deucarian.Persistence
{
    /// <summary>Default local persistence service.</summary>
    public sealed class PersistenceService : IPersistenceService
    {
        private readonly ITextStorage _storage;
        private readonly IPersistenceSerializer _serializer;
        private readonly IPersistenceClock _clock;
        private readonly Dictionary<DocumentLocation, SemaphoreSlim> _locks = new Dictionary<DocumentLocation, SemaphoreSlim>();
        private readonly object _gate = new object();
        private int _sequence;
        private bool _disposed;

        /// <summary>Creates a persistence service.</summary>
        public PersistenceService(ITextStorage storage, IPersistenceSerializer serializer = null, IPersistenceClock clock = null)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _serializer = serializer ?? new NewtonsoftPersistenceSerializer();
            _clock = clock ?? new SystemPersistenceClock();
        }

        /// <inheritdoc />
        public async Task<LoadResult<T>> LoadAsync<T>(DocumentDefinition<T> definition, SaveSlotId slotId, CancellationToken cancellationToken = default)
        {
            if (_disposed)
            {
                return LoadResult<T>.Failure(LoadOutcome.StorageFailure, PersistenceFailureReason.Disposed, RecoverySource.None, "Persistence service is disposed.");
            }

            DocumentLocation location = new DocumentLocation(definition.DocumentId, slotId);
            SemaphoreSlim semaphore = GetLock(location);
            try
            {
                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return LoadResult<T>.Failure(LoadOutcome.Canceled, PersistenceFailureReason.Canceled, RecoverySource.None, "Load canceled.");
            }

            try
            {
                return await LoadInternalAsync(definition, location, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <inheritdoc />
        public async Task<WriteResult> SaveAsync<T>(DocumentDefinition<T> definition, T document, SaveSlotId slotId, CancellationToken cancellationToken = default)
        {
            if (_disposed)
            {
                return WriteResult.Failure(WriteOutcome.Disposed, PersistenceFailureReason.Disposed, "Persistence service is disposed.");
            }

            ValidationResult validation = ValidateDocument(definition, document);
            if (!validation.Succeeded)
            {
                return WriteResult.Failure(WriteOutcome.ValidationFailure, PersistenceFailureReason.ValidationFailure, validation.Message);
            }

            DocumentLocation location = new DocumentLocation(definition.DocumentId, slotId);
            SemaphoreSlim semaphore = GetLock(location);
            try
            {
                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return WriteResult.Failure(WriteOutcome.Canceled, PersistenceFailureReason.Canceled, "Save canceled.");
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await CleanupTempFilesAsync(location, cancellationToken).ConfigureAwait(false);
                string primary = PrimaryName(location);
                string temp = TempName(location);
                string envelope = SaveEnvelopeCodec.Create(definition, document, _serializer, _clock.UtcNow);
                await _storage.WriteTextAsync(temp, envelope, cancellationToken).ConfigureAwait(false);
                await RotatePrimaryToBackupAsync(location, definition.BackupRetention, cancellationToken).ConfigureAwait(false);
                try
                {
                    await _storage.MoveAsync(temp, primary, true, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    await RestoreNewestBackupIfPrimaryMissingAsync(location, cancellationToken).ConfigureAwait(false);
                    throw;
                }

                await EnforceBackupRetentionAsync(location, definition.BackupRetention, cancellationToken).ConfigureAwait(false);
                return WriteResult.Success(WriteOutcome.Saved);
            }
            catch (OperationCanceledException)
            {
                return WriteResult.Failure(WriteOutcome.Canceled, PersistenceFailureReason.Canceled, "Save canceled.");
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException)
            {
                return WriteResult.Failure(WriteOutcome.StorageFailure, PersistenceFailureReason.StorageFailure, ex.Message);
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <inheritdoc />
        public async Task<WriteResult> DeleteAsync(DocumentLocation location, CancellationToken cancellationToken = default)
        {
            if (_disposed)
            {
                return WriteResult.Failure(WriteOutcome.Disposed, PersistenceFailureReason.Disposed, "Persistence service is disposed.");
            }

            SemaphoreSlim semaphore = GetLock(location);
            try
            {
                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                string primary = PrimaryName(location);
                bool existed = await _storage.ExistsAsync(primary, cancellationToken).ConfigureAwait(false);
                await _storage.DeleteAsync(primary, cancellationToken).ConfigureAwait(false);
                return WriteResult.Success(existed ? WriteOutcome.Deleted : WriteOutcome.Missing);
            }
            catch (OperationCanceledException)
            {
                return WriteResult.Failure(WriteOutcome.Canceled, PersistenceFailureReason.Canceled, "Delete canceled.");
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return WriteResult.Failure(WriteOutcome.StorageFailure, PersistenceFailureReason.StorageFailure, ex.Message);
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _disposed = true;
            lock (_gate)
            {
                foreach (SemaphoreSlim semaphore in _locks.Values)
                {
                    semaphore.Dispose();
                }

                _locks.Clear();
            }
        }

        private async Task<LoadResult<T>> LoadInternalAsync<T>(DocumentDefinition<T> definition, DocumentLocation location, CancellationToken cancellationToken)
        {
            try
            {
                await CleanupTempFilesAsync(location, cancellationToken).ConfigureAwait(false);
                string primary = PrimaryName(location);
                if (await _storage.ExistsAsync(primary, cancellationToken).ConfigureAwait(false))
                {
                    LoadResult<T> primaryResult = await TryLoadFromFileAsync(definition, primary, RecoverySource.Primary, LoadOutcome.LoadedPrimary, cancellationToken).ConfigureAwait(false);
                    if (primaryResult.Succeeded)
                    {
                        return primaryResult;
                    }

                    IReadOnlyList<string> availableBackups = await BackupNamesNewestFirstAsync(location, cancellationToken).ConfigureAwait(false);
                    if (availableBackups.Count == 0)
                    {
                        return primaryResult;
                    }
                }

                IReadOnlyList<string> backups = await BackupNamesNewestFirstAsync(location, cancellationToken).ConfigureAwait(false);
                foreach (string backup in backups)
                {
                    LoadResult<T> result = await TryLoadFromFileAsync(definition, backup, RecoverySource.Backup, LoadOutcome.RecoveredFromBackup, cancellationToken).ConfigureAwait(false);
                    if (result.Succeeded)
                    {
                        return result;
                    }
                }

                if (!await _storage.ExistsAsync(primary, cancellationToken).ConfigureAwait(false) && backups.Count == 0)
                {
                    return CreateDefault(definition, "No save existed.");
                }

                return LoadResult<T>.Failure(LoadOutcome.DeserializationFailure, PersistenceFailureReason.DeserializationFailure, RecoverySource.None, "All saved copies failed.");
            }
            catch (OperationCanceledException)
            {
                return LoadResult<T>.Failure(LoadOutcome.Canceled, PersistenceFailureReason.Canceled, RecoverySource.None, "Load canceled.");
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return LoadResult<T>.Failure(LoadOutcome.StorageFailure, PersistenceFailureReason.StorageFailure, RecoverySource.None, ex.Message);
            }
        }

        private async Task<LoadResult<T>> TryLoadFromFileAsync<T>(DocumentDefinition<T> definition, string fileName, RecoverySource source, LoadOutcome baseOutcome, CancellationToken cancellationToken)
        {
            string text;
            try
            {
                text = await _storage.ReadTextAsync(fileName, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return LoadResult<T>.Failure(LoadOutcome.StorageFailure, PersistenceFailureReason.StorageFailure, source, ex.Message);
            }

            if (!_serializer.TryReadEnvelopeMetadata(text, out DocumentId documentId, out SchemaVersion version, out string payload, out string checksum, out string message) ||
                !documentId.Equals(definition.DocumentId))
            {
                return LoadResult<T>.Failure(LoadOutcome.DeserializationFailure, PersistenceFailureReason.DeserializationFailure, source, message);
            }

            if (!string.IsNullOrEmpty(checksum) && !SaveEnvelopeCodec.ChecksumMatches(payload, checksum))
            {
                return LoadResult<T>.Failure(LoadOutcome.DeserializationFailure, PersistenceFailureReason.ChecksumMismatch, source, "Checksum mismatch.");
            }

            if (version.Value > definition.CurrentVersion.Value)
            {
                return LoadResult<T>.Failure(LoadOutcome.UnsupportedNewerSchema, PersistenceFailureReason.UnsupportedNewerSchema, source, "Saved schema is newer than supported.");
            }

            LoadOutcome outcome = baseOutcome;
            if (version.Value < definition.CurrentVersion.Value)
            {
                try
                {
                    if (!definition.Migrations.TryMigrate(payload, version, definition.CurrentVersion, _serializer, out payload, out message))
                    {
                        return LoadResult<T>.Failure(LoadOutcome.MissingMigration, PersistenceFailureReason.MissingMigration, source, message);
                    }
                }
                catch (Exception ex)
                {
                    return LoadResult<T>.Failure(LoadOutcome.DeserializationFailure, PersistenceFailureReason.InvalidMigration, source, ex.Message);
                }

                version = definition.CurrentVersion;
                outcome = source == RecoverySource.Backup ? LoadOutcome.RecoveredFromBackup : LoadOutcome.Migrated;
            }

            T document;
            try
            {
                document = _serializer.Deserialize<T>(payload);
            }
            catch (Exception ex)
            {
                return LoadResult<T>.Failure(LoadOutcome.DeserializationFailure, PersistenceFailureReason.DeserializationFailure, source, ex.Message);
            }

            ValidationResult validation = ValidateDocument(definition, document);
            if (!validation.Succeeded)
            {
                return LoadResult<T>.Failure(LoadOutcome.ValidationFailure, PersistenceFailureReason.ValidationFailure, source, validation.Message);
            }

            return LoadResult<T>.Success(outcome, source, document, version);
        }

        private LoadResult<T> CreateDefault<T>(DocumentDefinition<T> definition, string message)
        {
            T document = definition.DefaultFactory();
            ValidationResult validation = ValidateDocument(definition, document);
            if (!validation.Succeeded)
            {
                return LoadResult<T>.Failure(LoadOutcome.ValidationFailure, PersistenceFailureReason.ValidationFailure, RecoverySource.Default, validation.Message);
            }

            return LoadResult<T>.Success(LoadOutcome.CreatedDefault, RecoverySource.Default, document, definition.CurrentVersion, message);
        }

        private static ValidationResult ValidateDocument<T>(DocumentDefinition<T> definition, T document)
        {
            if (document == null)
            {
                return ValidationResult.Failure("Document cannot be null.");
            }

            return definition.Validator == null ? ValidationResult.Success() : definition.Validator.Validate(document);
        }

        private SemaphoreSlim GetLock(DocumentLocation location)
        {
            lock (_gate)
            {
                if (!_locks.TryGetValue(location, out SemaphoreSlim semaphore))
                {
                    semaphore = new SemaphoreSlim(1, 1);
                    _locks.Add(location, semaphore);
                }

                return semaphore;
            }
        }

        private async Task RotatePrimaryToBackupAsync(DocumentLocation location, int retention, CancellationToken cancellationToken)
        {
            string primary = PrimaryName(location);
            if (retention <= 0 || !await _storage.ExistsAsync(primary, cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            string backup = BackupName(location);
            await _storage.MoveAsync(primary, backup, true, cancellationToken).ConfigureAwait(false);
        }

        private async Task RestoreNewestBackupIfPrimaryMissingAsync(DocumentLocation location, CancellationToken cancellationToken)
        {
            string primary = PrimaryName(location);
            if (await _storage.ExistsAsync(primary, cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            string backup = (await BackupNamesNewestFirstAsync(location, cancellationToken).ConfigureAwait(false)).FirstOrDefault();
            if (!string.IsNullOrEmpty(backup))
            {
                await _storage.MoveAsync(backup, primary, true, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task EnforceBackupRetentionAsync(DocumentLocation location, int retention, CancellationToken cancellationToken)
        {
            IReadOnlyList<string> backups = await BackupNamesNewestFirstAsync(location, cancellationToken).ConfigureAwait(false);
            for (int index = retention; index < backups.Count; index++)
            {
                await _storage.DeleteAsync(backups[index], cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task<IReadOnlyList<string>> BackupNamesNewestFirstAsync(DocumentLocation location, CancellationToken cancellationToken)
        {
            IReadOnlyList<string> names = await _storage.ListAsync(location.FileStem + ".json.bak.", cancellationToken).ConfigureAwait(false);
            return names.OrderByDescending(name => name, StringComparer.Ordinal).ToArray();
        }

        private async Task CleanupTempFilesAsync(DocumentLocation location, CancellationToken cancellationToken)
        {
            IReadOnlyList<string> names = await _storage.ListAsync(location.FileStem + ".json.tmp.", cancellationToken).ConfigureAwait(false);
            foreach (string name in names)
            {
                await _storage.DeleteAsync(name, cancellationToken).ConfigureAwait(false);
            }
        }

        private string TempName(DocumentLocation location)
        {
            return location.FileStem + ".json.tmp." + NextStamp();
        }

        private string BackupName(DocumentLocation location)
        {
            return location.FileStem + ".json.bak." + NextStamp();
        }

        private string NextStamp()
        {
            int sequence = Interlocked.Increment(ref _sequence);
            return _clock.UtcNow.UtcDateTime.ToString("yyyyMMddHHmmssfffffff") + "." + sequence.ToString("000000");
        }

        private static string PrimaryName(DocumentLocation location) => location.FileStem + ".json";
    }
}
