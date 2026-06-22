using System;

namespace Deucarian.Persistence
{
    /// <summary>Result returned by load operations.</summary>
    public sealed class LoadResult<T>
    {
        private LoadResult(LoadOutcome outcome, PersistenceFailureReason failureReason, RecoverySource recoverySource, T document, SchemaVersion schemaVersion, string message)
        {
            Outcome = outcome;
            FailureReason = failureReason;
            RecoverySource = recoverySource;
            Document = document;
            SchemaVersion = schemaVersion;
            Message = message ?? string.Empty;
        }

        /// <summary>Gets whether the operation produced a usable document.</summary>
        public bool Succeeded => FailureReason == PersistenceFailureReason.None;

        /// <summary>Gets the load outcome.</summary>
        public LoadOutcome Outcome { get; }

        /// <summary>Gets the failure reason.</summary>
        public PersistenceFailureReason FailureReason { get; }

        /// <summary>Gets the recovery source.</summary>
        public RecoverySource RecoverySource { get; }

        /// <summary>Gets the document, when available.</summary>
        public T Document { get; }

        /// <summary>Gets the loaded schema version.</summary>
        public SchemaVersion SchemaVersion { get; }

        /// <summary>Gets a diagnostic message.</summary>
        public string Message { get; }

        /// <summary>Creates a successful result.</summary>
        public static LoadResult<T> Success(LoadOutcome outcome, RecoverySource source, T document, SchemaVersion schemaVersion, string message = "")
            => new LoadResult<T>(outcome, PersistenceFailureReason.None, source, document, schemaVersion, message);

        /// <summary>Creates a failed result.</summary>
        public static LoadResult<T> Failure(LoadOutcome outcome, PersistenceFailureReason reason, RecoverySource source, string message)
            => new LoadResult<T>(outcome, reason, source, default, default, message);
    }

    /// <summary>Result returned by write/delete operations.</summary>
    public sealed class WriteResult
    {
        private WriteResult(WriteOutcome outcome, PersistenceFailureReason failureReason, string message)
        {
            Outcome = outcome;
            FailureReason = failureReason;
            Message = message ?? string.Empty;
        }

        /// <summary>Gets whether the operation succeeded.</summary>
        public bool Succeeded => FailureReason == PersistenceFailureReason.None;

        /// <summary>Gets the write outcome.</summary>
        public WriteOutcome Outcome { get; }

        /// <summary>Gets the failure reason.</summary>
        public PersistenceFailureReason FailureReason { get; }

        /// <summary>Gets a diagnostic message.</summary>
        public string Message { get; }

        /// <summary>Creates success.</summary>
        public static WriteResult Success(WriteOutcome outcome, string message = "") => new WriteResult(outcome, PersistenceFailureReason.None, message);

        /// <summary>Creates failure.</summary>
        public static WriteResult Failure(WriteOutcome outcome, PersistenceFailureReason reason, string message) => new WriteResult(outcome, reason, message);
    }

    /// <summary>Document definition used by the persistence service.</summary>
    public sealed class DocumentDefinition<T>
    {
        /// <summary>Creates a document definition.</summary>
        public DocumentDefinition(
            DocumentId documentId,
            SchemaVersion currentVersion,
            Func<T> defaultFactory,
            IDocumentValidator<T> validator = null,
            DocumentMigrationSet migrations = null,
            int backupRetention = 3)
        {
            if (defaultFactory == null)
            {
                throw new ArgumentNullException(nameof(defaultFactory));
            }

            if (backupRetention < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(backupRetention), "Backup retention cannot be negative.");
            }

            DocumentId = documentId;
            CurrentVersion = currentVersion;
            DefaultFactory = defaultFactory;
            Validator = validator;
            Migrations = migrations ?? DocumentMigrationSet.Empty;
            BackupRetention = backupRetention;
            Migrations.ValidateFor(DocumentId, CurrentVersion);
        }

        /// <summary>Gets the document id.</summary>
        public DocumentId DocumentId { get; }

        /// <summary>Gets the current schema version.</summary>
        public SchemaVersion CurrentVersion { get; }

        /// <summary>Gets the default factory.</summary>
        public Func<T> DefaultFactory { get; }

        /// <summary>Gets the validator.</summary>
        public IDocumentValidator<T> Validator { get; }

        /// <summary>Gets migrations.</summary>
        public DocumentMigrationSet Migrations { get; }

        /// <summary>Gets backup retention.</summary>
        public int BackupRetention { get; }
    }
}
