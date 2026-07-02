using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Deucarian.Persistence.Tests
{
    public sealed class PersistenceTests
    {
        private static readonly DocumentId SettingsId = new DocumentId("settings");
        private static readonly DocumentId ProfileId = new DocumentId("profile");
        private static readonly DocumentId RunId = new DocumentId("run-resume");

        [Test]
        public async Task SaveLoadRoundTrip()
        {
            Harness<SettingsDocument> harness = SettingsHarness();
            await harness.Service.SaveAsync(harness.Definition, new SettingsDocument { Volume = 0.5f, Locale = "nl-NL" }, SaveSlotId.Default);

            LoadResult<SettingsDocument> result = await harness.Service.LoadAsync(harness.Definition, SaveSlotId.Default);

            Assert.That(result.Outcome, Is.EqualTo(LoadOutcome.LoadedPrimary));
            Assert.That(result.Document.Locale, Is.EqualTo("nl-NL"));
        }

        [Test]
        public async Task MissingDocumentReturnsConfiguredDefault()
        {
            Harness<SettingsDocument> harness = SettingsHarness();

            LoadResult<SettingsDocument> result = await harness.Service.LoadAsync(harness.Definition, SaveSlotId.Default);

            Assert.That(result.Outcome, Is.EqualTo(LoadOutcome.CreatedDefault));
            Assert.That(result.Document.Locale, Is.EqualTo("en-US"));
        }

        [Test]
        public async Task OverwriteExistingSave()
        {
            Harness<SettingsDocument> harness = SettingsHarness();
            await harness.Service.SaveAsync(harness.Definition, new SettingsDocument { Volume = 0.1f, Locale = "en-US" }, SaveSlotId.Default);
            await harness.Service.SaveAsync(harness.Definition, new SettingsDocument { Volume = 0.9f, Locale = "fr-FR" }, SaveSlotId.Default);

            LoadResult<SettingsDocument> result = await harness.Service.LoadAsync(harness.Definition, SaveSlotId.Default);

            Assert.That(result.Document.Volume, Is.EqualTo(0.9f));
        }

        [Test]
        public async Task DeleteExistingAndMissingSave()
        {
            Harness<SettingsDocument> harness = SettingsHarness();
            await harness.Service.SaveAsync(harness.Definition, new SettingsDocument(), SaveSlotId.Default);

            WriteResult deleted = await harness.Service.DeleteAsync(new DocumentLocation(SettingsId, SaveSlotId.Default));
            WriteResult missing = await harness.Service.DeleteAsync(new DocumentLocation(SettingsId, SaveSlotId.Default));

            Assert.That(deleted.Outcome, Is.EqualTo(WriteOutcome.Deleted));
            Assert.That(missing.Outcome, Is.EqualTo(WriteOutcome.Missing));
        }

        [Test]
        public async Task IndependentSettingsProfileAndRunDocuments()
        {
            InMemoryTextStorage storage = new InMemoryTextStorage();
            var service = new PersistenceService(storage, clock: new ControlledClock());
            await service.SaveAsync(SettingsDefinition(), new SettingsDocument { Locale = "en-US" }, SaveSlotId.Default);
            await service.SaveAsync(ProfileDefinition(), new ProfileDocument { PlayerName = "Moss", Experience = 12 }, SaveSlotId.Default);
            await service.SaveAsync(RunDefinition(), new RunResumeDocument { RunId = "run-1", Tick = 40 }, SaveSlotId.Default);

            Assert.That(storage.Files.ContainsKey("settings__default.json"), Is.True);
            Assert.That(storage.Files.ContainsKey("profile__default.json"), Is.True);
            Assert.That(storage.Files.ContainsKey("run-resume__default.json"), Is.True);
        }

        [Test]
        public void FileBackedSaveCanCompleteWhenSynchronouslyWaitedUnderSynchronizationContext()
        {
            string root = Path.Combine(Path.GetTempPath(), "deucarian-persistence-sync-wait-" + Guid.NewGuid().ToString("N"));
            SynchronizationContext previous = SynchronizationContext.Current;
            try
            {
                Directory.CreateDirectory(root);
                SynchronizationContext.SetSynchronizationContext(new NonPumpingSynchronizationContext());
                var service = new PersistenceService(new FileTextStorage(new FixedPathProvider(root)));
                var document = new ProfileDocument
                {
                    PlayerName = "Player",
                    Notes = new string('x', 1024 * 1024)
                };

                Task<WriteResult> save = service.SaveAsync(ProfileDefinition(), document, SaveSlotId.Default);

                Assert.That(save.Wait(TimeSpan.FromSeconds(2)), Is.True, "File-backed save deadlocked while synchronously waiting on a captured synchronization context.");
                Assert.That(save.Result.Succeeded, Is.True);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previous);
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public async Task ValidationBeforeSaveAndAfterLoad()
        {
            Harness<SettingsDocument> harness = SettingsHarness();
            WriteResult save = await harness.Service.SaveAsync(harness.Definition, new SettingsDocument { Volume = 2f, Locale = "en-US" }, SaveSlotId.Default);
            await WriteEnvelope(harness.Storage, harness.Definition, new SettingsDocument { Volume = -1f, Locale = "en-US" }, version: 1);

            LoadResult<SettingsDocument> load = await harness.Service.LoadAsync(harness.Definition, SaveSlotId.Default);

            Assert.That(save.Outcome, Is.EqualTo(WriteOutcome.ValidationFailure));
            Assert.That(load.Outcome, Is.EqualTo(LoadOutcome.ValidationFailure));
        }

        [Test]
        public async Task InvalidDefaultFactoryBehavior()
        {
            InMemoryTextStorage storage = new InMemoryTextStorage();
            var service = new PersistenceService(storage);
            var definition = new DocumentDefinition<SettingsDocument>(
                SettingsId,
                new SchemaVersion(1),
                () => new SettingsDocument { Volume = 5f, Locale = "en-US" },
                SettingsValidator());

            LoadResult<SettingsDocument> result = await service.LoadAsync(definition, SaveSlotId.Default);

            Assert.That(result.Outcome, Is.EqualTo(LoadOutcome.ValidationFailure));
        }

        [Test]
        public void InvalidDocumentDefinitionsAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new DocumentDefinition<SettingsDocument>(SettingsId, new SchemaVersion(1), null));
            Assert.Throws<ArgumentException>(() => new DocumentDefinition<SettingsDocument>(SettingsId, new SchemaVersion(1), () => new SettingsDocument(), migrations: new DocumentMigrationSet(new[] { new DelegateDocumentMigration(new DocumentId("other"), new SchemaVersion(0), new SchemaVersion(1), (p, s) => p) })));
        }

        [Test]
        public async Task NoMigrationNeededAndOneStepMigration()
        {
            Harness<ProfileV2> harness = ProfileHarness();
            await WritePayload(harness.Storage, ProfileId, new SchemaVersion(1), "{\"name\":\"Ada\",\"xp\":7}");

            LoadResult<ProfileV2> result = await harness.Service.LoadAsync(harness.Definition, SaveSlotId.Default);

            Assert.That(result.Outcome, Is.EqualTo(LoadOutcome.Migrated));
            Assert.That(result.Document.PlayerName, Is.EqualTo("Ada"));
            Assert.That(result.Document.Progress.Experience, Is.EqualTo(7));
        }

        [Test]
        public async Task MultiStepMigrationWithStructuralChange()
        {
            Harness<ProfileV2> harness = ProfileHarness();
            await WritePayload(harness.Storage, ProfileId, new SchemaVersion(0), "{\"displayName\":\"Grace\",\"legacyXp\":9}");

            LoadResult<ProfileV2> result = await harness.Service.LoadAsync(harness.Definition, SaveSlotId.Default);

            Assert.That(result.Outcome, Is.EqualTo(LoadOutcome.Migrated));
            Assert.That(result.Document.PlayerName, Is.EqualTo("Grace"));
            Assert.That(result.Document.Progress.Experience, Is.EqualTo(9));
        }

        [Test]
        public async Task MissingMigrationDuplicateAndInvalidEdgesAreDetected()
        {
            InMemoryTextStorage storage = new InMemoryTextStorage();
            var missing = new DocumentDefinition<ProfileV2>(ProfileId, new SchemaVersion(2), () => new ProfileV2(), ProfileValidator());
            var service = new PersistenceService(storage);
            await WritePayload(storage, ProfileId, new SchemaVersion(0), "{\"displayName\":\"Grace\",\"legacyXp\":9}");

            LoadResult<ProfileV2> result = await service.LoadAsync(missing, SaveSlotId.Default);

            Assert.That(result.Outcome, Is.EqualTo(LoadOutcome.MissingMigration));
            Assert.Throws<ArgumentException>(() => new DocumentMigrationSet(new IDocumentMigration[]
            {
                new DelegateDocumentMigration(ProfileId, new SchemaVersion(0), new SchemaVersion(1), (p, s) => p),
                new DelegateDocumentMigration(ProfileId, new SchemaVersion(0), new SchemaVersion(1), (p, s) => p)
            }));
            Assert.Throws<ArgumentException>(() => new DocumentMigrationSet(new[] { new DelegateDocumentMigration(ProfileId, new SchemaVersion(2), new SchemaVersion(1), (p, s) => p) }));
        }

        [Test]
        public async Task FailedMigrationPreservesOriginalDataAndFutureSchemaFailsSafely()
        {
            InMemoryTextStorage storage = new InMemoryTextStorage();
            var failingMigrations = new DocumentMigrationSet(new[] { new DelegateDocumentMigration(ProfileId, new SchemaVersion(0), new SchemaVersion(1), (p, s) => throw new InvalidOperationException("boom")) });
            var definition = new DocumentDefinition<ProfileV2>(ProfileId, new SchemaVersion(1), () => new ProfileV2(), ProfileValidator(), failingMigrations);
            var service = new PersistenceService(storage);
            await WritePayload(storage, ProfileId, new SchemaVersion(0), "{\"displayName\":\"Grace\",\"legacyXp\":9}");
            string original = storage.Files["profile__default.json"];

            LoadResult<ProfileV2> failed = await service.LoadAsync(definition, SaveSlotId.Default);
            await WritePayload(storage, ProfileId, new SchemaVersion(99), "{\"playerName\":\"Future\"}");
            LoadResult<ProfileV2> future = await service.LoadAsync(definition, SaveSlotId.Default);

            Assert.That(failed.Outcome, Is.EqualTo(LoadOutcome.StorageFailure).Or.EqualTo(LoadOutcome.DeserializationFailure).Or.EqualTo(LoadOutcome.MissingMigration));
            Assert.That(storage.Files["profile__default.json"], Is.Not.Empty);
            Assert.That(original, Does.Contain("displayName"));
            Assert.That(future.Outcome, Is.EqualTo(LoadOutcome.UnsupportedNewerSchema));
        }

        [Test]
        public async Task MigratedDataIsValidated()
        {
            Harness<ProfileV2> harness = ProfileHarness();
            await WritePayload(harness.Storage, ProfileId, new SchemaVersion(0), "{\"displayName\":\"\",\"legacyXp\":-1}");

            LoadResult<ProfileV2> result = await harness.Service.LoadAsync(harness.Definition, SaveSlotId.Default);

            Assert.That(result.Outcome, Is.EqualTo(LoadOutcome.ValidationFailure));
        }

        [Test]
        public async Task CorruptedPrimaryRecoversFromNewestHealthyBackup()
        {
            Harness<SettingsDocument> harness = SettingsHarness();
            await harness.Service.SaveAsync(harness.Definition, new SettingsDocument { Locale = "old", Volume = 0.2f }, SaveSlotId.Default);
            await harness.Service.SaveAsync(harness.Definition, new SettingsDocument { Locale = "new", Volume = 0.3f }, SaveSlotId.Default);
            harness.Storage.Files["settings__default.json"] = "{ bad";

            LoadResult<SettingsDocument> result = await harness.Service.LoadAsync(harness.Definition, SaveSlotId.Default);

            Assert.That(result.Outcome, Is.EqualTo(LoadOutcome.RecoveredFromBackup));
            Assert.That(result.Document.Locale, Is.EqualTo("old"));
        }

        [Test]
        public async Task CorruptedNewestBackupFallsBackToOlderHealthyBackup()
        {
            Harness<SettingsDocument> harness = SettingsHarness();
            await harness.Service.SaveAsync(harness.Definition, new SettingsDocument { Locale = "oldest", Volume = 0.2f }, SaveSlotId.Default);
            await harness.Service.SaveAsync(harness.Definition, new SettingsDocument { Locale = "older", Volume = 0.3f }, SaveSlotId.Default);
            await harness.Service.SaveAsync(harness.Definition, new SettingsDocument { Locale = "primary", Volume = 0.4f }, SaveSlotId.Default);
            harness.Storage.Files["settings__default.json"] = "{ bad";
            string newest = NewestBackup(harness.Storage, "settings__default");
            harness.Storage.Files[newest] = "{ bad backup";

            LoadResult<SettingsDocument> result = await harness.Service.LoadAsync(harness.Definition, SaveSlotId.Default);

            Assert.That(result.Outcome, Is.EqualTo(LoadOutcome.RecoveredFromBackup));
            Assert.That(result.Document.Locale, Is.EqualTo("oldest"));
        }

        [Test]
        public async Task AllCopiesCorruptedTruncatedInvalidMetadataAndChecksumFailuresAreExplicit()
        {
            Harness<SettingsDocument> harness = SettingsHarness();
            harness.Storage.Files["settings__default.json"] = "{\"format\":\"deucarian.persistence.v1\"";
            LoadResult<SettingsDocument> truncated = await harness.Service.LoadAsync(harness.Definition, SaveSlotId.Default);
            harness.Storage.Files["settings__default.json"] = "{\"format\":\"deucarian.persistence.v1\"}";
            LoadResult<SettingsDocument> invalid = await harness.Service.LoadAsync(harness.Definition, SaveSlotId.Default);
            await WriteEnvelope(harness.Storage, harness.Definition, new SettingsDocument { Locale = "ok", Volume = 1f }, 1, checksum: "wrong");
            LoadResult<SettingsDocument> checksum = await harness.Service.LoadAsync(harness.Definition, SaveSlotId.Default);

            Assert.That(truncated.Outcome, Is.EqualTo(LoadOutcome.DeserializationFailure));
            Assert.That(invalid.Outcome, Is.EqualTo(LoadOutcome.DeserializationFailure));
            Assert.That(checksum.FailureReason, Is.EqualTo(PersistenceFailureReason.ChecksumMismatch));
        }

        [Test]
        public async Task AbandonedTemporaryFilesAreCleanedUp()
        {
            Harness<SettingsDocument> harness = SettingsHarness();
            harness.Storage.Files["settings__default.json.tmp.old"] = "partial";

            await harness.Service.SaveAsync(harness.Definition, new SettingsDocument(), SaveSlotId.Default);

            Assert.That(CountFiles(harness.Storage, ".tmp."), Is.EqualTo(0));
        }

        [Test]
        public async Task AtomicFaultsPreservePreviousHealthyPrimaryAndRetentionLimit()
        {
            InMemoryTextStorage inner = new InMemoryTextStorage();
            FaultInjectingTextStorage fault = new FaultInjectingTextStorage(inner);
            var service = new PersistenceService(fault, clock: new ControlledClock());
            DocumentDefinition<SettingsDocument> definition = SettingsDefinition(backupRetention: 1);
            await service.SaveAsync(definition, new SettingsDocument { Locale = "healthy", Volume = 1f }, SaveSlotId.Default);

            fault.NextFault = StorageFaultPoint.BeforeWrite;
            WriteResult beforeTemp = await service.SaveAsync(definition, new SettingsDocument { Locale = "bad", Volume = 1f }, SaveSlotId.Default);
            fault.NextFault = StorageFaultPoint.DuringWrite;
            WriteResult duringTemp = await service.SaveAsync(definition, new SettingsDocument { Locale = "bad", Volume = 1f }, SaveSlotId.Default);
            fault.NextFault = StorageFaultPoint.BeforeMove;
            WriteResult beforeReplace = await service.SaveAsync(definition, new SettingsDocument { Locale = "bad", Volume = 1f }, SaveSlotId.Default);
            fault.NextFault = StorageFaultPoint.DuringMove;
            WriteResult duringReplace = await service.SaveAsync(definition, new SettingsDocument { Locale = "bad", Volume = 1f }, SaveSlotId.Default);
            LoadResult<SettingsDocument> loaded = await service.LoadAsync(definition, SaveSlotId.Default);

            Assert.That(beforeTemp.Outcome, Is.EqualTo(WriteOutcome.StorageFailure));
            Assert.That(duringTemp.Outcome, Is.EqualTo(WriteOutcome.StorageFailure));
            Assert.That(beforeReplace.Outcome, Is.EqualTo(WriteOutcome.StorageFailure));
            Assert.That(duringReplace.Outcome, Is.EqualTo(WriteOutcome.StorageFailure));
            Assert.That(loaded.Document.Locale, Is.EqualTo("healthy"));
            Assert.That(CountFiles(inner, ".bak."), Is.LessThanOrEqualTo(1));
        }

        [Test]
        public async Task ConcurrentSameDocumentWritesAreSerializedAndDifferentDocumentsProceed()
        {
            InMemoryTextStorage storage = new InMemoryTextStorage();
            var service = new PersistenceService(storage, clock: new ControlledClock());
            DocumentDefinition<SettingsDocument> settings = SettingsDefinition();
            Task[] same = new Task[10];
            for (int index = 0; index < same.Length; index++)
            {
                int local = index;
                same[index] = service.SaveAsync(settings, new SettingsDocument { Locale = "l" + local, Volume = 1f }, SaveSlotId.Default);
            }

            await Task.WhenAll(same);
            await Task.WhenAll(
                service.SaveAsync(ProfileDefinition(), new ProfileDocument { PlayerName = "A" }, SaveSlotId.Default),
                service.SaveAsync(RunDefinition(), new RunResumeDocument { RunId = "R" }, SaveSlotId.Default));

            LoadResult<SettingsDocument> loaded = await service.LoadAsync(settings, SaveSlotId.Default);
            Assert.That(loaded.Succeeded, Is.True);
            Assert.That(storage.Files.ContainsKey("profile__default.json"), Is.True);
            Assert.That(storage.Files.ContainsKey("run-resume__default.json"), Is.True);
        }

        [Test]
        public async Task LoadDuringSaveSeesConsistentResultAndCancellationDoesNotCorruptStorage()
        {
            Harness<SettingsDocument> harness = SettingsHarness();
            await harness.Service.SaveAsync(harness.Definition, new SettingsDocument { Locale = "healthy", Volume = 1f }, SaveSlotId.Default);
            using (CancellationTokenSource cts = new CancellationTokenSource())
            {
                cts.Cancel();
                WriteResult canceled = await harness.Service.SaveAsync(harness.Definition, new SettingsDocument { Locale = "bad", Volume = 1f }, SaveSlotId.Default, cts.Token);
                LoadResult<SettingsDocument> loaded = await harness.Service.LoadAsync(harness.Definition, SaveSlotId.Default);
                Assert.That(canceled.Outcome, Is.EqualTo(WriteOutcome.Canceled));
                Assert.That(loaded.Document.Locale, Is.EqualTo("healthy"));
            }
        }

        [Test]
        public async Task DisposalPreventsUnsafeNewOperations()
        {
            Harness<SettingsDocument> harness = SettingsHarness();
            harness.Service.Dispose();

            WriteResult result = await harness.Service.SaveAsync(harness.Definition, new SettingsDocument(), SaveSlotId.Default);

            Assert.That(result.Outcome, Is.EqualTo(WriteOutcome.Disposed));
        }

        [Test]
        public void UnsafeIdentifiersAreRejectedAndRootIsPreserved()
        {
            Assert.Throws<ArgumentException>(() => new DocumentId(".."));
            Assert.Throws<ArgumentException>(() => new DocumentId("folder/name"));
            Assert.Throws<ArgumentException>(() => new DocumentId(Path.GetFullPath("x")));
            Assert.Throws<ArgumentException>(() => new SaveSlotId("a\\b"));
            string root = Path.Combine(Path.GetTempPath(), "deucarian-persistence-root");
            string destination = PathSafety.CombineUnderRoot(root, "safe.json");
            Assert.That(destination, Does.StartWith(Path.GetFullPath(root)));
        }

        [Test]
        public async Task SerializationHandlesUnicodeEmptyCollectionsDictionariesLargeDocumentsMalformedAndNullPolicy()
        {
            Harness<ProfileDocument> harness = new Harness<ProfileDocument>(new InMemoryTextStorage(), ProfileDefinition());
            ProfileDocument profile = new ProfileDocument
            {
                PlayerName = "ユニコード",
                Tags = new List<string>(),
                Counters = new Dictionary<string, int> { { "coins", 3 } },
                Notes = new string('x', 128 * 1024)
            };
            await harness.Service.SaveAsync(harness.Definition, profile, SaveSlotId.Default);
            LoadResult<ProfileDocument> loaded = await harness.Service.LoadAsync(harness.Definition, SaveSlotId.Default);
            harness.Storage.Files["profile__default.json"] = "{ malformed";
            LoadResult<ProfileDocument> malformed = await harness.Service.LoadAsync(harness.Definition, SaveSlotId.Default);

            Assert.That(loaded.Document.PlayerName, Is.EqualTo("ユニコード"));
            Assert.That(loaded.Document.Counters["coins"], Is.EqualTo(3));
            Assert.That(loaded.Document.Tags, Is.Empty);
            Assert.That(malformed.Outcome, Is.EqualTo(LoadOutcome.DeserializationFailure));
        }

        [Test]
        public async Task DonorStyleWorkflowLoadsSavesMigratesAndRecovers()
        {
            Harness<DonorStyleSaveV2> harness = DonorHarness();
            await WritePayload(harness.Storage, new DocumentId("donor-profile"), new SchemaVersion(1), "{\"bloodShards\":5,\"selectedPlayerClassId\":\"warrior\"}");

            LoadResult<DonorStyleSaveV2> migrated = await harness.Service.LoadAsync(harness.Definition, SaveSlotId.Default);
            await harness.Service.SaveAsync(harness.Definition, migrated.Document, SaveSlotId.Default);
            harness.Storage.Files["donor-profile__default.json"] = "{ corrupt";
            LoadResult<DonorStyleSaveV2> recovered = await harness.Service.LoadAsync(harness.Definition, SaveSlotId.Default);

            Assert.That(migrated.Outcome, Is.EqualTo(LoadOutcome.Migrated));
            Assert.That(migrated.Document.UnspentBloodShards, Is.EqualTo(5));
            Assert.That(recovered.Outcome, Is.EqualTo(LoadOutcome.RecoveredFromBackup));
        }

        private static Harness<SettingsDocument> SettingsHarness()
        {
            return new Harness<SettingsDocument>(new InMemoryTextStorage(), SettingsDefinition());
        }

        private static DocumentDefinition<SettingsDocument> SettingsDefinition(int backupRetention = 3)
        {
            return new DocumentDefinition<SettingsDocument>(
                SettingsId,
                new SchemaVersion(1),
                () => new SettingsDocument { Volume = 1f, Locale = "en-US" },
                SettingsValidator(),
                backupRetention: backupRetention);
        }

        private static IDocumentValidator<SettingsDocument> SettingsValidator()
        {
            return new DelegateDocumentValidator<SettingsDocument>(document =>
                document.Volume >= 0f && document.Volume <= 1f && !string.IsNullOrWhiteSpace(document.Locale)
                    ? ValidationResult.Success()
                    : ValidationResult.Failure("Invalid settings."));
        }

        private static DocumentDefinition<ProfileDocument> ProfileDefinition()
        {
            return new DocumentDefinition<ProfileDocument>(
                ProfileId,
                new SchemaVersion(1),
                () => new ProfileDocument { PlayerName = "Player" },
                new DelegateDocumentValidator<ProfileDocument>(document => string.IsNullOrWhiteSpace(document.PlayerName) ? ValidationResult.Failure("Missing player name.") : ValidationResult.Success()));
        }

        private static DocumentDefinition<RunResumeDocument> RunDefinition()
        {
            return new DocumentDefinition<RunResumeDocument>(
                RunId,
                new SchemaVersion(1),
                () => new RunResumeDocument { RunId = "none" },
                new DelegateDocumentValidator<RunResumeDocument>(document => string.IsNullOrWhiteSpace(document.RunId) ? ValidationResult.Failure("Missing run id.") : ValidationResult.Success()));
        }

        private static Harness<ProfileV2> ProfileHarness()
        {
            return new Harness<ProfileV2>(new InMemoryTextStorage(), new DocumentDefinition<ProfileV2>(
                ProfileId,
                new SchemaVersion(2),
                () => new ProfileV2 { PlayerName = "Player", Progress = new ProgressV2() },
                ProfileValidator(),
                ProfileMigrations()));
        }

        private static IDocumentValidator<ProfileV2> ProfileValidator()
        {
            return new DelegateDocumentValidator<ProfileV2>(document =>
                !string.IsNullOrWhiteSpace(document.PlayerName) && document.Progress != null && document.Progress.Experience >= 0
                    ? ValidationResult.Success()
                    : ValidationResult.Failure("Invalid profile."));
        }

        private static DocumentMigrationSet ProfileMigrations()
        {
            return new DocumentMigrationSet(new IDocumentMigration[]
            {
                new DelegateDocumentMigration(ProfileId, new SchemaVersion(0), new SchemaVersion(1), (payload, serializer) =>
                {
                    ProfileV0 legacy = serializer.Deserialize<ProfileV0>(payload);
                    return serializer.Serialize(new ProfileV1 { Name = legacy.DisplayName, Xp = legacy.LegacyXp });
                }),
                new DelegateDocumentMigration(ProfileId, new SchemaVersion(1), new SchemaVersion(2), (payload, serializer) =>
                {
                    ProfileV1 legacy = serializer.Deserialize<ProfileV1>(payload);
                    return serializer.Serialize(new ProfileV2 { PlayerName = legacy.Name, Progress = new ProgressV2 { Experience = legacy.Xp } });
                })
            });
        }

        private static Harness<DonorStyleSaveV2> DonorHarness()
        {
            DocumentId donorId = new DocumentId("donor-profile");
            var migrations = new DocumentMigrationSet(new[] { new DelegateDocumentMigration(donorId, new SchemaVersion(1), new SchemaVersion(2), (payload, serializer) =>
            {
                DonorStyleSaveV1 legacy = serializer.Deserialize<DonorStyleSaveV1>(payload);
                int shards = legacy.BloodShards;
                return serializer.Serialize(new DonorStyleSaveV2
                {
                    Version = 2,
                    LifetimeBloodShards = shards,
                    UnspentBloodShards = shards,
                    SelectedPlayerClassId = legacy.SelectedPlayerClassId,
                    MetaUpgradeRanks = new Dictionary<string, int>()
                });
            }) });
            return new Harness<DonorStyleSaveV2>(new InMemoryTextStorage(), new DocumentDefinition<DonorStyleSaveV2>(
                donorId,
                new SchemaVersion(2),
                () => new DonorStyleSaveV2 { SelectedPlayerClassId = string.Empty },
                new DelegateDocumentValidator<DonorStyleSaveV2>(document => document.UnspentBloodShards >= 0 ? ValidationResult.Success() : ValidationResult.Failure("Invalid shards.")),
                migrations));
        }

        private static async Task WriteEnvelope<T>(InMemoryTextStorage storage, DocumentDefinition<T> definition, T document, int version, string checksum = null)
        {
            var serializer = new NewtonsoftPersistenceSerializer();
            string payload = serializer.Serialize(document);
            string envelope = serializer.Serialize(new SaveEnvelope
            {
                DocumentId = definition.DocumentId.Value,
                SchemaVersion = version,
                Payload = payload,
                SavedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                Checksum = checksum ?? SaveEnvelopeCodec.ComputeChecksum(payload)
            });
            storage.Files[definition.DocumentId.Value + "__default.json"] = envelope;
            await Task.CompletedTask;
        }

        private static async Task WritePayload(InMemoryTextStorage storage, DocumentId documentId, SchemaVersion version, string payload)
        {
            var serializer = new NewtonsoftPersistenceSerializer();
            string envelope = serializer.Serialize(new SaveEnvelope
            {
                DocumentId = documentId.Value,
                SchemaVersion = version.Value,
                Payload = payload,
                SavedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                Checksum = SaveEnvelopeCodec.ComputeChecksum(payload)
            });
            storage.Files[documentId.Value + "__default.json"] = envelope;
            await Task.CompletedTask;
        }

        private static string NewestBackup(InMemoryTextStorage storage, string stem)
        {
            string selected = string.Empty;
            foreach (string name in storage.Files.Keys)
            {
                if (name.StartsWith(stem + ".json.bak.", StringComparison.Ordinal) && string.CompareOrdinal(name, selected) > 0)
                {
                    selected = name;
                }
            }

            return selected;
        }

        private static int CountFiles(InMemoryTextStorage storage, string contains)
        {
            int count = 0;
            foreach (string name in storage.Files.Keys)
            {
                if (name.Contains(contains))
                {
                    count++;
                }
            }

            return count;
        }

        private sealed class Harness<T>
        {
            public Harness(InMemoryTextStorage storage, DocumentDefinition<T> definition)
            {
                Storage = storage;
                Definition = definition;
                Service = new PersistenceService(storage, clock: new ControlledClock());
            }

            public InMemoryTextStorage Storage { get; }

            public DocumentDefinition<T> Definition { get; }

            public PersistenceService Service { get; }
        }

        private sealed class ControlledClock : IPersistenceClock
        {
            private long _ticks = new DateTimeOffset(2026, 6, 22, 0, 0, 0, TimeSpan.Zero).Ticks;

            public DateTimeOffset UtcNow => new DateTimeOffset(Interlocked.Add(ref _ticks, TimeSpan.TicksPerSecond), TimeSpan.Zero);
        }

        private sealed class FixedPathProvider : IPersistencePathProvider
        {
            private readonly string _root;

            public FixedPathProvider(string root)
            {
                _root = root;
            }

            public string GetRootPath() => _root;
        }

        private sealed class NonPumpingSynchronizationContext : SynchronizationContext
        {
            public override void Post(SendOrPostCallback d, object state)
            {
            }
        }

        public sealed class SettingsDocument
        {
            public float Volume { get; set; } = 1f;
            public string Locale { get; set; } = "en-US";
        }

        public sealed class ProfileDocument
        {
            public string PlayerName { get; set; } = "Player";
            public List<string> Tags { get; set; } = new List<string>();
            public Dictionary<string, int> Counters { get; set; } = new Dictionary<string, int>();
            public string Notes { get; set; }
            public int Experience { get; set; }
        }

        public sealed class RunResumeDocument
        {
            public string RunId { get; set; }
            public int Tick { get; set; }
        }

        public sealed class ProfileV2
        {
            public string PlayerName { get; set; }
            public ProgressV2 Progress { get; set; } = new ProgressV2();
        }

        public sealed class ProfileV0
        {
            public string DisplayName { get; set; }
            public int LegacyXp { get; set; }
        }

        public sealed class ProfileV1
        {
            public string Name { get; set; }
            public int Xp { get; set; }
        }

        public sealed class ProgressV2
        {
            public int Experience { get; set; }
        }

        public sealed class DonorStyleSaveV2
        {
            public int Version { get; set; } = 2;
            public int LifetimeBloodShards { get; set; }
            public int UnspentBloodShards { get; set; }
            public string SelectedPlayerClassId { get; set; }
            public Dictionary<string, int> MetaUpgradeRanks { get; set; } = new Dictionary<string, int>();
        }

        public sealed class DonorStyleSaveV1
        {
            public int BloodShards { get; set; }
            public string SelectedPlayerClassId { get; set; }
        }
    }
}
