using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
namespace Deucarian.Persistence.Unity
{
    /// <summary>Derive a concrete component with a serializable document type. The profile owns storage and migrations.</summary>
    public abstract class SaveDocumentTrigger<TDocument> : MonoBehaviour
    {
        [SerializeField] private SaveProfileHost host;
        [SerializeField] private SaveKey<TDocument> key;
        [SerializeField] private TDocument document;
        [SerializeField] private UnityEvent saved = new UnityEvent();
        [SerializeField] private UnityEvent<TDocument> loaded = new UnityEvent<TDocument>();
        [SerializeField] private UnityEvent failed = new UnityEvent();
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        public WriteResult LastWrite { get; private set; }
        public LoadResult<TDocument> LastLoad { get; private set; }
        private SaveProfileHost Host => host != null ? host : throw new InvalidOperationException("Assign a configured SaveProfileHost to this document component.");
        public Task<WriteResult> SaveAsync(TDocument value, CancellationToken cancellationToken = default) => Host.SaveAsync(key, value, cancellationToken);
        public Task<LoadResult<TDocument>> LoadAsync(CancellationToken cancellationToken = default) => Host.LoadAsync(key, cancellationToken);
        public async void Save()
        {
            WriteResult result;
            try { result = await SaveAsync(document, lifetime.Token); }
            catch (OperationCanceledException) { return; }
            if (this == null) return;
            LastWrite = result;
            if (result.Succeeded) saved.Invoke(); else failed.Invoke();
        }
        public async void Load()
        {
            LoadResult<TDocument> result;
            try { result = await LoadAsync(lifetime.Token); }
            catch (OperationCanceledException) { return; }
            if (this == null) return;
            LastLoad = result;
            if (result.Succeeded) { document = result.Document; loaded.Invoke(document); } else failed.Invoke();
        }
        protected virtual void OnDestroy() { lifetime.Cancel(); lifetime.Dispose(); }
    }
}
