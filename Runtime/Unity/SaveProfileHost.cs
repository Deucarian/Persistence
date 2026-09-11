using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Deucarian.Persistence.Unity
{
    /// <summary>Configure one profile during application composition; consumers only save and load.</summary>
    [DisallowMultipleComponent]
    public sealed class SaveProfileHost : MonoBehaviour
    {
        private SaveProfile profile;
        private bool ownsProfile;
        private bool destroyed;
        public void Configure(SaveProfile value, bool takeOwnership = false)
        {
            if (destroyed) throw new ObjectDisposedException(nameof(SaveProfileHost));
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (profile != null) throw new InvalidOperationException("This save host is already configured.");
            profile = value;
            ownsProfile = takeOwnership;
        }

        public Task<WriteResult> SaveAsync<T>(string id, T data, CancellationToken cancellationToken = default) =>
            Profile.SaveAsync(id, data, cancellationToken);
        public Task<LoadResult<T>> LoadAsync<T>(string id, CancellationToken cancellationToken = default) =>
            Profile.LoadAsync<T>(id, cancellationToken);
        private SaveProfile Profile => profile ?? throw new InvalidOperationException("Configure a save profile first.");
        private void OnDestroy()
        {
            destroyed = true;
            if (ownsProfile) profile?.Dispose();
            profile = null;
        }
    }
}
