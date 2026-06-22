namespace Deucarian.Persistence.Unity
{
    /// <summary>Captures Unity's persistent data path on construction for later background-safe use.</summary>
    public sealed class UnityPersistentDataPathProvider : IPersistencePathProvider
    {
        private readonly string _path;

        /// <summary>Captures <c>Application.persistentDataPath</c>. Construct this on Unity's main thread.</summary>
        public UnityPersistentDataPathProvider()
        {
            _path = UnityEngine.Application.persistentDataPath;
        }

        /// <inheritdoc />
        public string GetRootPath() => _path;
    }
}
