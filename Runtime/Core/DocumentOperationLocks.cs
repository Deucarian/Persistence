using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Deucarian.Persistence
{
    internal sealed class DocumentOperationLocks : IDisposable
    {
        private readonly object gate = new object();
        private readonly Dictionary<DocumentLocation, Entry> entries = new Dictionary<DocumentLocation, Entry>();
        private bool disposed;

        public bool IsDisposed { get { lock (gate) return disposed; } }

        public async Task<IDisposable> AcquireAsync(DocumentLocation location, CancellationToken token)
        {
            Entry entry;
            lock (gate)
            {
                if (disposed) return null;
                if (!entries.TryGetValue(location, out entry))
                {
                    entry = new Entry();
                    entries.Add(location, entry);
                }
                entry.Users++;
            }

            try
            {
                await entry.Semaphore.WaitAsync(token).ConfigureAwait(false);
                return new Lease(this, location, entry);
            }
            catch
            {
                Return(location, entry, acquired: false);
                throw;
            }
        }

        public void Dispose()
        {
            // Accepted operations retain ownership until they drain; shutdown never blocks a Unity thread.
            lock (gate) disposed = true;
        }

        private void Return(DocumentLocation location, Entry entry, bool acquired)
        {
            lock (gate)
            {
                if (acquired) entry.Semaphore.Release();
                if (--entry.Users != 0) return;
                entries.Remove(location);
                entry.Semaphore.Dispose();
            }
        }

        private sealed class Entry
        {
            public readonly SemaphoreSlim Semaphore = new SemaphoreSlim(1, 1);
            public int Users;
        }

        private sealed class Lease : IDisposable
        {
            private DocumentOperationLocks owner;
            private readonly DocumentLocation location;
            private readonly Entry entry;

            public Lease(DocumentOperationLocks owner, DocumentLocation location, Entry entry)
            {
                this.owner = owner;
                this.location = location;
                this.entry = entry;
            }

            public void Dispose() => Interlocked.Exchange(ref owner, null)?.Return(location, entry, acquired: true);
        }
    }
}
