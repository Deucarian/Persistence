using System;
namespace Deucarian.Persistence.Unity.Samples.DefinitionWorkflow
{
    [Serializable] public sealed class SampleSettings { public float Volume = 1f; }
    [SaveKeySet] public static class SampleSaveKeys
    {
        public static SaveKey<SampleSettings> Settings => new Key();
        private sealed class Key : SaveKey<SampleSettings> { public Key() : base("workflowsettings") { } }
    }
}
