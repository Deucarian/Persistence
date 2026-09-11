namespace Deucarian.Persistence.Tests
{
    internal sealed class SaveProfileTestsKey<T> : ISaveKey<T>
    {
        public SaveProfileTestsKey(string id) { Id = id; }
        public string Id { get; }
    }
}
