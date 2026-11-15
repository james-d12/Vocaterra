using System.Collections.Concurrent;
using Vocaterra.Game.Core.World.Streaming;

namespace Vocaterra.Game.Streaming;

/// <summary>
/// The surface bands of the chunk columns asked for so far (B07), kept because a band costs the shaped ground of a column's window and the plan asks for the same columns again each time the player
/// crosses a chunk. A band is pure, so two threads that make the same one at once make the same answer and either may win; the cache is cleared when it grows past a bound.
/// </summary>
public sealed class ColumnBandCache(SurfaceBands bands, int limit = 400_000)
{
    private readonly ConcurrentDictionary<long, ColumnBand> _cache = new();

    /// <summary>How many bands are held.</summary>
    public int Count => _cache.Count;

    /// <summary>The key of a column.</summary>
    /// <param name="chunkX">The chunk column.</param>
    /// <param name="chunkZ">The chunk row.</param>
    public static long KeyOf(int chunkX, int chunkZ) => ((long)chunkX << 32) | (uint)chunkZ;

    /// <summary>The band if it was made already.</summary>
    /// <param name="chunkX">The chunk column.</param>
    /// <param name="chunkZ">The chunk row.</param>
    /// <param name="band">The band.</param>
    public bool TryGet(int chunkX, int chunkZ, out ColumnBand band) => _cache.TryGetValue(KeyOf(chunkX, chunkZ), out band);

    /// <summary>The band of a column, made now when it is not held (any thread).</summary>
    /// <param name="chunkX">The chunk column.</param>
    /// <param name="chunkZ">The chunk row.</param>
    public ColumnBand Get(int chunkX, int chunkZ)
    {
        var key = KeyOf(chunkX, chunkZ);
        if (_cache.TryGetValue(key, out var held))
        {
            return held;
        }

        var made = bands.Of(chunkX, chunkZ);
        if (_cache.Count > limit)
        {
            _cache.Clear();
        }

        _cache[key] = made;
        return made;
    }
}
