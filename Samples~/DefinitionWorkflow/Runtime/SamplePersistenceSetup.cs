using UnityEngine;
namespace Deucarian.Persistence.Unity.Samples.DefinitionWorkflow
{
    [DefaultExecutionOrder(-2000)]
    public sealed class SamplePersistenceSetup : MonoBehaviour
    {
        [SerializeField] private SaveProfileHost host;
        private void Awake()
        {
            var profile = new SaveProfile(new PersistenceService(new InMemoryTextStorage()), new SaveSlotId("workflow"), ownsService: true);
            profile.Register(new DocumentDefinition<SampleSettings>(new DocumentId(SampleSaveKeys.Settings.Id), new SchemaVersion(1), () => new SampleSettings()));
            host.Configure(profile, takeOwnership: true);
        }
    }
}
