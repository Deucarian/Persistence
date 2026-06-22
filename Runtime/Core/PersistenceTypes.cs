using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Deucarian.Persistence
{
    /// <summary>Stable persistence document identifier. Values must be safe as file-name components.</summary>
    public readonly struct DocumentId : IEquatable<DocumentId>, IComparable<DocumentId>
    {
        private readonly string _value;

        /// <summary>Creates a document identifier.</summary>
        public DocumentId(string value)
        {
            PathSafety.ThrowIfUnsafeSegment(value, nameof(value));
            _value = value;
        }

        /// <summary>Gets the identifier value.</summary>
        public string Value => _value ?? string.Empty;

        /// <inheritdoc />
        public bool Equals(DocumentId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is DocumentId other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

        /// <inheritdoc />
        public int CompareTo(DocumentId other) => string.Compare(Value, other.Value, StringComparison.Ordinal);

        /// <inheritdoc />
        public override string ToString() => Value;
    }

    /// <summary>Optional save slot identifier. Values must be safe as file-name components.</summary>
    public readonly struct SaveSlotId : IEquatable<SaveSlotId>
    {
        private readonly string _value;

        /// <summary>Default slot.</summary>
        public static readonly SaveSlotId Default = new SaveSlotId("default");

        /// <summary>Creates a save slot identifier.</summary>
        public SaveSlotId(string value)
        {
            PathSafety.ThrowIfUnsafeSegment(value, nameof(value));
            _value = value;
        }

        /// <summary>Gets the slot value.</summary>
        public string Value => string.IsNullOrEmpty(_value) ? "default" : _value;

        /// <inheritdoc />
        public bool Equals(SaveSlotId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is SaveSlotId other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

        /// <inheritdoc />
        public override string ToString() => Value;
    }

    /// <summary>Document plus slot location.</summary>
    public readonly struct DocumentLocation : IEquatable<DocumentLocation>
    {
        /// <summary>Creates a document location.</summary>
        public DocumentLocation(DocumentId documentId, SaveSlotId slotId)
        {
            DocumentId = documentId;
            SlotId = slotId;
        }

        /// <summary>Gets the document id.</summary>
        public DocumentId DocumentId { get; }

        /// <summary>Gets the slot id.</summary>
        public SaveSlotId SlotId { get; }

        /// <summary>Gets a safe base file name for the location.</summary>
        public string FileStem => DocumentId.Value + "__" + SlotId.Value;

        /// <inheritdoc />
        public bool Equals(DocumentLocation other) => DocumentId.Equals(other.DocumentId) && SlotId.Equals(other.SlotId);

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is DocumentLocation other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => (DocumentId.GetHashCode() * 397) ^ SlotId.GetHashCode();

        /// <inheritdoc />
        public override string ToString() => FileStem;
    }

    /// <summary>Document schema version.</summary>
    public readonly struct SchemaVersion : IEquatable<SchemaVersion>, IComparable<SchemaVersion>
    {
        /// <summary>Creates a schema version.</summary>
        public SchemaVersion(int value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Schema version cannot be negative.");
            }

            Value = value;
        }

        /// <summary>Gets the version number.</summary>
        public int Value { get; }

        /// <inheritdoc />
        public bool Equals(SchemaVersion other) => Value == other.Value;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is SchemaVersion other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => Value;

        /// <inheritdoc />
        public int CompareTo(SchemaVersion other) => Value.CompareTo(other.Value);

        /// <inheritdoc />
        public override string ToString() => Value.ToString();
    }

    /// <summary>Load outcome.</summary>
    public enum LoadOutcome
    {
        /// <summary>Primary document loaded.</summary>
        LoadedPrimary,
        /// <summary>A backup was used after primary failed.</summary>
        RecoveredFromBackup,
        /// <summary>No save existed and the configured default was returned.</summary>
        CreatedDefault,
        /// <summary>Document was migrated successfully.</summary>
        Migrated,
        /// <summary>The saved schema is newer than this app supports.</summary>
        UnsupportedNewerSchema,
        /// <summary>A required migration step is missing.</summary>
        MissingMigration,
        /// <summary>Validation failed.</summary>
        ValidationFailure,
        /// <summary>Deserialization, checksum, or format corruption was detected.</summary>
        DeserializationFailure,
        /// <summary>Storage failed.</summary>
        StorageFailure,
        /// <summary>Operation was canceled.</summary>
        Canceled
    }

    /// <summary>Write outcome.</summary>
    public enum WriteOutcome
    {
        /// <summary>Document was written.</summary>
        Saved,
        /// <summary>Document was deleted.</summary>
        Deleted,
        /// <summary>Document did not exist when delete was requested.</summary>
        Missing,
        /// <summary>Validation failed before save.</summary>
        ValidationFailure,
        /// <summary>Storage failed.</summary>
        StorageFailure,
        /// <summary>Operation was canceled.</summary>
        Canceled,
        /// <summary>The service is disposed.</summary>
        Disposed
    }

    /// <summary>Failure category.</summary>
    public enum PersistenceFailureReason
    {
        /// <summary>No failure.</summary>
        None,
        /// <summary>Unsupported newer schema.</summary>
        UnsupportedNewerSchema,
        /// <summary>Missing migration.</summary>
        MissingMigration,
        /// <summary>Duplicate or invalid migration.</summary>
        InvalidMigration,
        /// <summary>Validation failed.</summary>
        ValidationFailure,
        /// <summary>Serialization failed.</summary>
        SerializationFailure,
        /// <summary>Deserialization or envelope parsing failed.</summary>
        DeserializationFailure,
        /// <summary>Checksum mismatch.</summary>
        ChecksumMismatch,
        /// <summary>Storage failed.</summary>
        StorageFailure,
        /// <summary>Operation canceled.</summary>
        Canceled,
        /// <summary>Service disposed.</summary>
        Disposed
    }

    /// <summary>Source used to satisfy a load.</summary>
    public enum RecoverySource
    {
        /// <summary>No saved data source.</summary>
        None,
        /// <summary>Primary save file.</summary>
        Primary,
        /// <summary>Backup save file.</summary>
        Backup,
        /// <summary>Default factory.</summary>
        Default
    }

    /// <summary>Injected UTC clock for deterministic tests.</summary>
    public interface IPersistenceClock
    {
        /// <summary>Gets current UTC time.</summary>
        DateTimeOffset UtcNow { get; }
    }

    /// <summary>System UTC clock.</summary>
    public sealed class SystemPersistenceClock : IPersistenceClock
    {
        /// <inheritdoc />
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    /// <summary>Supplies a root path for file storage.</summary>
    public interface IPersistencePathProvider
    {
        /// <summary>Gets the save root path.</summary>
        string GetRootPath();
    }

    /// <summary>Constant root path provider.</summary>
    public sealed class FixedPathProvider : IPersistencePathProvider
    {
        private readonly string _rootPath;

        /// <summary>Creates a fixed path provider.</summary>
        public FixedPathProvider(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                throw new ArgumentException("Root path cannot be empty.", nameof(rootPath));
            }

            _rootPath = rootPath;
        }

        /// <inheritdoc />
        public string GetRootPath() => _rootPath;
    }

    /// <summary>Validation callback for documents.</summary>
    public interface IDocumentValidator<in T>
    {
        /// <summary>Validates a document.</summary>
        ValidationResult Validate(T document);
    }

    /// <summary>Validation result.</summary>
    public readonly struct ValidationResult
    {
        private ValidationResult(bool succeeded, string message)
        {
            Succeeded = succeeded;
            Message = message ?? string.Empty;
        }

        /// <summary>Gets whether validation succeeded.</summary>
        public bool Succeeded { get; }

        /// <summary>Gets the validation message.</summary>
        public string Message { get; }

        /// <summary>Successful validation.</summary>
        public static ValidationResult Success() => new ValidationResult(true, string.Empty);

        /// <summary>Failed validation.</summary>
        public static ValidationResult Failure(string message) => new ValidationResult(false, message);
    }

    /// <summary>Delegate validator.</summary>
    public sealed class DelegateDocumentValidator<T> : IDocumentValidator<T>
    {
        private readonly Func<T, ValidationResult> _validate;

        /// <summary>Creates a delegate validator.</summary>
        public DelegateDocumentValidator(Func<T, ValidationResult> validate)
        {
            _validate = validate ?? throw new ArgumentNullException(nameof(validate));
        }

        /// <inheritdoc />
        public ValidationResult Validate(T document) => _validate(document);
    }

    /// <summary>Path safety helpers.</summary>
    public static class PathSafety
    {
        /// <summary>Throws when a consumer-provided segment can escape the save root.</summary>
        public static void ThrowIfUnsafeSegment(string value, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Identifier cannot be empty.", paramName);
            }

            if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
            {
                throw new ArgumentException("Identifier cannot contain leading or trailing whitespace.", paramName);
            }

            if (value == "." || value == ".." || value.Contains("..") || Path.IsPathRooted(value) ||
                value.IndexOfAny(new[] { '/', '\\' }) >= 0)
            {
                throw new ArgumentException("Identifier cannot contain path traversal, roots, or separators.", paramName);
            }

            char[] invalid = Path.GetInvalidFileNameChars();
            for (int index = 0; index < value.Length; index++)
            {
                char c = value[index];
                if (Array.IndexOf(invalid, c) >= 0)
                {
                    throw new ArgumentException("Identifier contains an invalid file-name character.", paramName);
                }
            }
        }

        /// <summary>Combines and verifies that the result remains under the root.</summary>
        public static string CombineUnderRoot(string rootPath, string safeRelativePath)
        {
            string root = Path.GetFullPath(rootPath);
            string full = Path.GetFullPath(Path.Combine(root, safeRelativePath));
            string rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? root
                : root + Path.DirectorySeparatorChar;
            if (!full.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase) && !string.Equals(full, root, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Resolved persistence path escaped the configured root.");
            }

            return full;
        }
    }

    /// <summary>Versioned persistence service.</summary>
    public interface IPersistenceService : IDisposable
    {
        /// <summary>Loads a document.</summary>
        Task<LoadResult<T>> LoadAsync<T>(DocumentDefinition<T> definition, SaveSlotId slotId, CancellationToken cancellationToken = default);

        /// <summary>Saves a document.</summary>
        Task<WriteResult> SaveAsync<T>(DocumentDefinition<T> definition, T document, SaveSlotId slotId, CancellationToken cancellationToken = default);

        /// <summary>Deletes a document.</summary>
        Task<WriteResult> DeleteAsync(DocumentLocation location, CancellationToken cancellationToken = default);
    }
}
