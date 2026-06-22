using System;
using System.Collections.Generic;
using System.Linq;

namespace Deucarian.Persistence
{
    /// <summary>Single document migration step.</summary>
    public interface IDocumentMigration
    {
        /// <summary>Document id this migration applies to.</summary>
        DocumentId DocumentId { get; }

        /// <summary>Source schema version.</summary>
        SchemaVersion FromVersion { get; }

        /// <summary>Destination schema version.</summary>
        SchemaVersion ToVersion { get; }

        /// <summary>Migrates payload JSON text.</summary>
        string Migrate(string payloadJson, IPersistenceSerializer serializer);
    }

    /// <summary>Delegate migration step.</summary>
    public sealed class DelegateDocumentMigration : IDocumentMigration
    {
        private readonly Func<string, IPersistenceSerializer, string> _migrate;

        /// <summary>Creates a delegate migration.</summary>
        public DelegateDocumentMigration(DocumentId documentId, SchemaVersion fromVersion, SchemaVersion toVersion, Func<string, IPersistenceSerializer, string> migrate)
        {
            DocumentId = documentId;
            FromVersion = fromVersion;
            ToVersion = toVersion;
            _migrate = migrate ?? throw new ArgumentNullException(nameof(migrate));
        }

        /// <inheritdoc />
        public DocumentId DocumentId { get; }

        /// <inheritdoc />
        public SchemaVersion FromVersion { get; }

        /// <inheritdoc />
        public SchemaVersion ToVersion { get; }

        /// <inheritdoc />
        public string Migrate(string payloadJson, IPersistenceSerializer serializer) => _migrate(payloadJson, serializer);
    }

    /// <summary>Validated migration set.</summary>
    public sealed class DocumentMigrationSet
    {
        private readonly Dictionary<int, IDocumentMigration> _bySource;

        /// <summary>Empty migration set.</summary>
        public static readonly DocumentMigrationSet Empty = new DocumentMigrationSet(Array.Empty<IDocumentMigration>());

        /// <summary>Creates a migration set.</summary>
        public DocumentMigrationSet(IEnumerable<IDocumentMigration> migrations)
        {
            if (migrations == null)
            {
                throw new ArgumentNullException(nameof(migrations));
            }

            _bySource = new Dictionary<int, IDocumentMigration>();
            foreach (IDocumentMigration migration in migrations)
            {
                if (migration == null)
                {
                    throw new ArgumentException("Migration cannot be null.", nameof(migrations));
                }

                if (migration.ToVersion.Value <= migration.FromVersion.Value)
                {
                    throw new ArgumentException("Migration edges must move forward.", nameof(migrations));
                }

                if (_bySource.ContainsKey(migration.FromVersion.Value))
                {
                    throw new ArgumentException($"Duplicate migration from version {migration.FromVersion.Value}.", nameof(migrations));
                }

                _bySource.Add(migration.FromVersion.Value, migration);
            }
        }

        /// <summary>Gets whether a migration path exists.</summary>
        public bool CanMigrate(SchemaVersion fromVersion, SchemaVersion toVersion)
        {
            if (fromVersion.Value == toVersion.Value)
            {
                return true;
            }

            int version = fromVersion.Value;
            HashSet<int> visited = new HashSet<int>();
            while (version < toVersion.Value)
            {
                if (!visited.Add(version) || !_bySource.TryGetValue(version, out IDocumentMigration migration))
                {
                    return false;
                }

                version = migration.ToVersion.Value;
            }

            return version == toVersion.Value;
        }

        /// <summary>Validates the set for a document definition.</summary>
        public void ValidateFor(DocumentId documentId, SchemaVersion currentVersion)
        {
            foreach (IDocumentMigration migration in _bySource.Values)
            {
                if (!migration.DocumentId.Equals(documentId))
                {
                    throw new ArgumentException("Migration document id does not match document definition.");
                }

                if (migration.ToVersion.Value > currentVersion.Value)
                {
                    throw new ArgumentException("Migration targets a version newer than the document definition.");
                }
            }
        }

        /// <summary>Migrates payload from one version to another.</summary>
        public bool TryMigrate(string payloadJson, SchemaVersion fromVersion, SchemaVersion toVersion, IPersistenceSerializer serializer, out string migratedPayload, out string message)
        {
            migratedPayload = payloadJson;
            message = string.Empty;
            int version = fromVersion.Value;
            HashSet<int> visited = new HashSet<int>();
            while (version < toVersion.Value)
            {
                if (!visited.Add(version))
                {
                    message = "Migration cycle detected.";
                    return false;
                }

                if (!_bySource.TryGetValue(version, out IDocumentMigration migration))
                {
                    message = $"Missing migration from version {version}.";
                    return false;
                }

                migratedPayload = migration.Migrate(migratedPayload, serializer);
                version = migration.ToVersion.Value;
            }

            if (version != toVersion.Value)
            {
                message = "Migration path overshot target version.";
                return false;
            }

            return true;
        }

        /// <summary>Gets all migrations.</summary>
        public IReadOnlyList<IDocumentMigration> Migrations => _bySource.Values.OrderBy(migration => migration.FromVersion.Value).ToArray();
    }
}
