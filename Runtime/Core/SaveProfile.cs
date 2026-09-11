using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Deucarian.Persistence
{
    /// <summary>A scoped save slot with explicitly registered typed document definitions.</summary>
    public sealed class SaveProfile : IDisposable
    {
        private readonly IPersistenceService persistence;
        private readonly bool ownsService;
        private readonly Dictionary<string, object> definitions = new Dictionary<string, object>(StringComparer.Ordinal);
        private bool disposed;
        public SaveSlotId Slot { get; }

        public SaveProfile(IPersistenceService persistence, SaveSlotId slot, bool ownsService = false)
        {
            this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
            if (string.IsNullOrWhiteSpace(slot.Value)) throw new ArgumentException("A save slot is required.", nameof(slot));
            Slot = slot;
            this.ownsService = ownsService;
        }

        public void Register<T>(DocumentDefinition<T> definition)
        {
            ThrowIfDisposed();
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            definitions.Add(definition.DocumentId.Value, definition);
        }

        public Task<WriteResult> SaveAsync<T>(string id, T document, CancellationToken cancellationToken = default) =>
            persistence.SaveAsync(Definition<T>(id), document, Slot, cancellationToken);
        public Task<LoadResult<T>> LoadAsync<T>(string id, CancellationToken cancellationToken = default) =>
            persistence.LoadAsync(Definition<T>(id), Slot, cancellationToken);

        private DocumentDefinition<T> Definition<T>(string id)
        {
            ThrowIfDisposed();
            if (!definitions.TryGetValue(id, out var definition))
                throw new KeyNotFoundException("No document definition is registered with ID '" + id + "'.");
            return definition as DocumentDefinition<T> ??
                throw new InvalidOperationException("The document ID is registered for a different data type.");
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            definitions.Clear();
            if (ownsService) persistence.Dispose();
        }
        private void ThrowIfDisposed() { if (disposed) throw new ObjectDisposedException(nameof(SaveProfile)); }
    }
}
