using UnityEngine;

namespace Deucarian.Persistence.Unity.Samples.SimpleUsage
{
    public sealed class SimpleUsageExample : MonoBehaviour
    {
        [SerializeField] private SaveProfileHost saves;
        public System.Threading.Tasks.Task Save(SettingsData data) => saves.SaveAsync("settings", data);
        public System.Threading.Tasks.Task<Deucarian.Persistence.LoadResult<SettingsData>> Load() => saves.LoadAsync<SettingsData>("settings");
        public sealed class SettingsData { public float Volume = 1f; }
    }
}
