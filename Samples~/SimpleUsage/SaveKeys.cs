namespace Deucarian.Persistence.Unity.Samples.SimpleUsage
{
    [SaveKeySet]
    public static class SaveKeys
    {
        public static SaveKey<SimpleUsageExample.SettingsData> Settings => new Definition();
        private sealed class Definition : SaveKey<SimpleUsageExample.SettingsData>
        {
            public Definition() : base("settings") { }
        }
    }
}
