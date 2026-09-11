namespace Deucarian.Persistence
{
    /// <summary>A declared Save identity accepted by the pure domain API.</summary>
    public interface ISaveKey<TData> { string Id { get; } }
}
