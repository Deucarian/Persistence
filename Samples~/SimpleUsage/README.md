# Simple usage

Configure a SaveProfileHost once with a SaveProfile(service, slot), after registering each DocumentDefinition<T> on the profile. For example, register a settings definition with DocumentId(SaveKeys.Settings.Id), SchemaVersion(1), and an explicit default factory for SettingsData. Definitions retain validation, migrations, backup policy, and the existing serializer. Supply your storage/serializer to PersistenceService at composition time; this adapter adds no reflection-based mapping.

SaveProfile borrows the service unless ownsService is true. SaveProfileHost borrows the profile unless Configure(profile, takeOwnership: true) is used. Ownership is released on host destruction, not on ordinary view hiding. Separate profiles use distinct SaveSlotIds. Save/Load retain WriteResult and LoadResult<T>, including recovery and cancellation outcomes. Wrong or unknown document types fail explicitly.

Import the **Simple Usage** sample from Unity Package Manager. Its caller script is:

Definition fields now use named, domain-specific keys. Select an existing definition from the Inspector dropdown or pass the same named key in code. Declare each project key once in a marked key set; ordinary caller methods do not accept raw IDs. Generated keys for asset-authored definitions require no asset reference in the caller. Owner-issued selection and row handles represent runtime instances.

```csharp
using UnityEngine;

namespace Deucarian.Persistence.Unity.Samples.SimpleUsage
{
    public sealed class SimpleUsageExample : MonoBehaviour
    {
        [SerializeField] private SaveKey<SettingsData> settings = SaveKeys.Settings;

        [SerializeField] private SaveProfileHost saves;
        public System.Threading.Tasks.Task Save(SettingsData data) => saves.SaveAsync(settings, data);
        public System.Threading.Tasks.Task<Deucarian.Persistence.LoadResult<SettingsData>> Load() => saves.LoadAsync<SettingsData>(settings);
        public sealed class SettingsData { public float Volume = 1f; }
    }
}
```
