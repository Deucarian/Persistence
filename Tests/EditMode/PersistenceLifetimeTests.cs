using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Deucarian.Persistence.Tests
{
    public sealed class PersistenceLifetimeTests
    {
        private static readonly DocumentDefinition<Document> Definition = new DocumentDefinition<Document>(
            new DocumentId("lifetime"), new SchemaVersion(1), () => new Document { Value = "default" });
        private static readonly DocumentLocation Location = new DocumentLocation(Definition.DocumentId, SaveSlotId.Default);

        [Test]
        public async Task PreCanceledDeleteDoesNotReleaseAnUnacquiredLock()
        {
            using (var service = new PersistenceService(new InMemoryTextStorage()))
            {
                var result = await service.DeleteAsync(Location, new CancellationToken(true));
                Assert.That(result.Outcome, Is.EqualTo(WriteOutcome.Canceled));
                Assert.That((await Save(service, "after cancellation")).Succeeded, Is.True);
            }
        }

        [Test]
        public async Task CanceledQueuedDeleteDoesNotLetAnotherOperationPassAnActiveSave()
        {
            var storage = new ControlledStorage { PauseWrite = true };
            using (var service = new PersistenceService(storage))
            using (var cancellation = new CancellationTokenSource())
            {
                var save = Save(service, "current");
                await storage.WriteStarted.Task;
                var delete = service.DeleteAsync(Location, cancellation.Token);
                cancellation.Cancel();
                Assert.That((await delete).Outcome, Is.EqualTo(WriteOutcome.Canceled));
                var load = service.LoadAsync(Definition, SaveSlotId.Default);
                Assert.That(load.IsCompleted, Is.False, "Canceled waiters must not release the active writer's lock.");
                storage.ResumeWrite.TrySetResult(true);
                Assert.That((await save).Succeeded, Is.True);
                Assert.That((await load).Document.Value, Is.EqualTo("current"));
            }
        }

        [Test]
        public async Task DeleteRemovesRecoveryHistoryButNotAnotherSlot()
        {
            var storage = new InMemoryTextStorage();
            using (var service = new PersistenceService(storage))
            {
                await Save(service, "old");
                await Save(service, "new");
                var otherSlot = new SaveSlotId("other");
                await service.SaveAsync(Definition, new Document { Value = "keep" }, otherSlot);
                storage.Files[Location.FileStem + ".json.tmp.abandoned"] = "partial";
                Assert.That((await service.DeleteAsync(Location)).Outcome, Is.EqualTo(WriteOutcome.Deleted));
                Assert.That((await service.LoadAsync(Definition, SaveSlotId.Default)).Outcome, Is.EqualTo(LoadOutcome.CreatedDefault));
                Assert.That((await service.LoadAsync(Definition, otherSlot)).Document.Value, Is.EqualTo("keep"));
                Assert.That(await storage.ListAsync(Location.FileStem + ".json", default), Is.Empty);
            }
        }

        [Test]
        public async Task DeleteAlsoRemovesBackupOnlyDocuments()
        {
            var storage = new InMemoryTextStorage();
            using (var service = new PersistenceService(storage))
            {
                await Save(service, "old");
                await Save(service, "new");
                storage.Files.Remove(Location.FileStem + ".json");
                Assert.That((await service.DeleteAsync(Location)).Outcome, Is.EqualTo(WriteOutcome.Deleted));
                Assert.That((await service.LoadAsync(Definition, SaveSlotId.Default)).Outcome, Is.EqualTo(LoadOutcome.CreatedDefault));
            }
        }

        [Test]
        public async Task InterruptedBackupDeletionKeepsPrimaryUntilRetrySucceeds()
        {
            var storage = new ControlledStorage();
            using (var service = new PersistenceService(storage))
            {
                await Save(service, "old");
                await Save(service, "new");
                storage.FailBackupDelete = true;
                Assert.That((await service.DeleteAsync(Location)).Outcome, Is.EqualTo(WriteOutcome.StorageFailure));
                Assert.That((await service.LoadAsync(Definition, SaveSlotId.Default)).Document.Value, Is.EqualTo("new"));
                storage.FailBackupDelete = false;
                Assert.That((await service.DeleteAsync(Location)).Outcome, Is.EqualTo(WriteOutcome.Deleted));
                Assert.That((await service.LoadAsync(Definition, SaveSlotId.Default)).Outcome, Is.EqualTo(LoadOutcome.CreatedDefault));
            }
        }

        [Test]
        public async Task DisposeDrainsAcceptedWorkAndRejectsNewOperations()
        {
            var storage = new ControlledStorage { PauseWrite = true };
            var service = new PersistenceService(storage);
            var save = Save(service, "accepted");
            await storage.WriteStarted.Task;
            var queuedLoad = service.LoadAsync(Definition, SaveSlotId.Default);
            service.Dispose();
            service.Dispose();
            Assert.That((await Save(service, "rejected")).Outcome, Is.EqualTo(WriteOutcome.Disposed));
            Assert.That((await service.DeleteAsync(Location)).Outcome, Is.EqualTo(WriteOutcome.Disposed));
            Assert.That((await service.LoadAsync(Definition, SaveSlotId.Default)).FailureReason, Is.EqualTo(PersistenceFailureReason.Disposed));
            storage.ResumeWrite.TrySetResult(true);
            Assert.That((await save).Outcome, Is.EqualTo(WriteOutcome.Saved));
            Assert.That((await queuedLoad).Document.Value, Is.EqualTo("accepted"));
        }

        private static Task<WriteResult> Save(PersistenceService service, string value) =>
            service.SaveAsync(Definition, new Document { Value = value }, SaveSlotId.Default);

        public sealed class Document
        {
            public string Value { get; set; }
        }

        private sealed class ControlledStorage : ITextStorage
        {
            private readonly InMemoryTextStorage inner = new InMemoryTextStorage();
            public readonly TaskCompletionSource<bool> WriteStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public readonly TaskCompletionSource<bool> ResumeWrite = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public bool PauseWrite;
            public bool FailBackupDelete;

            public async Task WriteTextAsync(string path, string text, CancellationToken token)
            {
                if (PauseWrite)
                {
                    PauseWrite = false;
                    WriteStarted.TrySetResult(true);
                    await ResumeWrite.Task.ConfigureAwait(false);
                }
                await inner.WriteTextAsync(path, text, token).ConfigureAwait(false);
            }

            public Task DeleteAsync(string path, CancellationToken token)
            {
                if (FailBackupDelete && path.Contains(".bak.")) throw new IOException("Interrupted backup deletion.");
                return inner.DeleteAsync(path, token);
            }

            public Task<bool> ExistsAsync(string path, CancellationToken token) => inner.ExistsAsync(path, token);
            public Task<string> ReadTextAsync(string path, CancellationToken token) => inner.ReadTextAsync(path, token);
            public Task MoveAsync(string from, string to, bool overwrite, CancellationToken token) => inner.MoveAsync(from, to, overwrite, token);
            public Task<IReadOnlyList<string>> ListAsync(string prefix, CancellationToken token) => inner.ListAsync(prefix, token);
        }
    }
}
