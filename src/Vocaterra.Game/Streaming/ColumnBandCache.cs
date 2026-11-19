using System.Collections.Concurrent;
using Vocaterra.Game.Core.World.Generation.Procedural;
using Vocaterra.Game.Core.World.Streaming;

namespace Vocaterra.Game.Streaming;

/// <summary>
/// The surface bands of the chunk columns asked for so far (B07), kept because a band costs the shaped ground of a column's window and the plan asks for the same columns again each time the player
/// crosses a chunk. A band is pure, so two threads that make the same one at once make the same answer and either may win; the cache is cleared when it grows past a bound.
/// </summary>
public sealed class ColumnBandCache(SurfaceBands bands, int limit = 400_000, int groupLimit = 128)
{
    private readonly ConcurrentDictionary<long, ColumnBand> _cache = new();

    /// <summary>The shaped windows of the groups whose bands were made through them (B39), for the residency's layers to be cut from.</summary>
    public ColumnGroupCache Groups { get; } = new(groupLimit);

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

        return Put(chunkX, chunkZ, bands.Of(chunkX, chunkZ));
    }

    /// <summary>The band of a column, from the shaped window of a group that holds it when one is given (B39: the ring's footprint makes the bands round it and its own columns from one window); the same band as <see cref="Get(int, int)"/>.</summary>
    /// <param name="chunkX">The chunk column.</param>
    /// <param name="chunkZ">The chunk row.</param>
    /// <param name="group">The group's shaped window, or <see langword="null"/> to make the column's own.</param>
    public ColumnBand Get(int chunkX, int chunkZ, ColumnGroup? group)
    {
        if (_cache.TryGetValue(KeyOf(chunkX, chunkZ), out var held))
        {
            return held;
        }

        return Put(chunkX, chunkZ, group is null ? bands.Of(chunkX, chunkZ) : bands.Of(chunkX, chunkZ, group));
    }

    /// <summary>
    /// The band of a column, made through the shaped window of its group of 4 x 4 chunk columns (B39): the window is shaped once and kept in <see cref="Groups"/> for the columns' layers, and every band of the group that is not held is made from it
    /// in the same call, so the sixteen columns pay for one window between them. A group with no procedural column (the valley's) needs no window and makes the one band.
    /// </summary>
    /// <param name="chunkX">The chunk column.</param>
    /// <param name="chunkZ">The chunk row.</param>
    public ColumnBand GetThroughGroup(int chunkX, int chunkZ)
    {
        if (_cache.TryGetValue(KeyOf(chunkX, chunkZ), out var held))
        {
            return held;
        }

        var (minX, minZ) = ColumnGroupCache.OriginOf(chunkX, chunkZ);
        var span = ColumnGroupCache.Span;
        var procedural = false;
        for (var z = minZ; z < minZ + span && !procedural; z++)
        {
            for (var x = minX; x < minX + span && !procedural; x++)
            {
                procedural = bands.IsProcedural(x, z);
            }
        }

        if (!procedural)
        {
            return Get(chunkX, chunkZ);
        }

        if (!Groups.TryGet(chunkX, chunkZ, out var group))
        {
            group = bands.ShapeGroup(minX, minZ, span);
            Groups.Add(chunkX, chunkZ, group);
        }

        for (var z = minZ; z < minZ + span; z++)
        {
            for (var x = minX; x < minX + span; x++)
            {
                _ = Get(x, z, group);
            }
        }

        return Get(chunkX, chunkZ);
    }

    private ColumnBand Put(int chunkX, int chunkZ, ColumnBand made)
    {
        if (_cache.Count > limit)
        {
            _cache.Clear();
        }

        _cache[KeyOf(chunkX, chunkZ)] = made;
        return made;
    }
}
