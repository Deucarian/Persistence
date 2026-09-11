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

        public Task<WriteResult> SaveAsync<T>(ISaveKey<T> key, T document, CancellationToken cancellationToken = default) =>
            persistence.SaveAsync(Definition(key), document, Slot, cancellationToken);
        public Task<LoadResult<T>> LoadAsync<T>(ISaveKey<T> key, CancellationToken cancellationToken = default) =>
            persistence.LoadAsync(Definition(key), Slot, cancellationToken);

        private DocumentDefinition<T> Definition<T>(ISaveKey<T> key)
        {
            ThrowIfDisposed();
            if (key == null) throw new ArgumentNullException(nameof(key), "Select a SaveKey matching your document type or pass a named save definition.");
            string id = key.Id;
            if (!definitions.TryGetValue(id, out var definition))
                throw new KeyNotFoundException("SaveProfile for slot '" + Slot.Value + "' has no definition for '" + id + "'. Register its DocumentDefinition with this profile before saving or loading.");
            return definition as DocumentDefinition<T> ??
                throw new InvalidOperationException("Save definition '" + id + "' is registered for a different data type. Register the DocumentDefinition matching this SaveKey's data type.");
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
