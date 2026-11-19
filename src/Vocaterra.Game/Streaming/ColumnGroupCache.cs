using System.Collections.Concurrent;
using Vocaterra.Game.Core.World.Generation.Procedural;

namespace Vocaterra.Game.Streaming;

/// <summary>
/// The shaped windows of the groups of 4 x 4 chunk columns the residency has made bands from (B39), kept until the columns' layers have been made from them: a group's climate, height model and shapers cost once for its sixteen
/// columns, where each column's own window costs them again (a column's window is 42 columns wide for its 16, so neighbours repeat about 7 times the same work). Groups are aligned to the chunk grid (the group of column
/// (cx, cz) starts at (cx rounded down to a multiple of 4, cz likewise)), so every column has one group and a job needs no agreement with another. A group is immutable, so any worker may read it; the cache drops the oldest
/// first, which is the nearest, whose columns are asked for first, and a column whose group was dropped (or never made) is made from its own window, exactly as before (the chunks are the same bytes either way).
/// </summary>
/// <remarks>
/// Memory: a group holds the window of 90 x 90 columns, about 0.5 MB (<see cref="ColumnGroup.Bytes"/>), and a job that makes one holds about as much again while it shapes it, so at most about 8 MB more is in flight with 8 workers and the cache holds
/// <paramref name="limit"/> groups (128 by default: about 64 MB, the groups of a disc of 20 chunk columns round the player).
/// </remarks>
public sealed class ColumnGroupCache(int limit = 128)
{
    /// <summary>How many chunk columns a group spans on a side.</summary>
    public const int Span = 4;

    private readonly ConcurrentDictionary<long, ColumnGroup> _groups = new();
    private readonly ConcurrentQueue<long> _order = new();

    /// <summary>How many groups are held.</summary>
    public int Count => _groups.Count;

    /// <summary>About how many bytes the held groups take.</summary>
    public long Bytes => _groups.Values.Sum(static g => g.Bytes);

    /// <summary>The first chunk column of the group a column belongs to.</summary>
    /// <param name="chunkX">The chunk column.</param>
    /// <param name="chunkZ">The chunk row.</param>
    public static (int X, int Z) OriginOf(int chunkX, int chunkZ) => ((chunkX >> 2) << 2, (chunkZ >> 2) << 2);

    /// <summary>The key of the group a column belongs to.</summary>
    /// <param name="chunkX">The chunk column.</param>
    /// <param name="chunkZ">The chunk row.</param>
    public static long KeyOf(int chunkX, int chunkZ) => ColumnBandCache.KeyOf(chunkX >> 2, chunkZ >> 2);

    /// <summary>The group a column belongs to, if its window is held.</summary>
    /// <param name="chunkX">The chunk column.</param>
    /// <param name="chunkZ">The chunk row.</param>
    /// <param name="group">The group.</param>
    public bool TryGet(int chunkX, int chunkZ, out ColumnGroup group) => _groups.TryGetValue(KeyOf(chunkX, chunkZ), out group!);

    /// <summary>Keeps the group of a column, dropping the oldest ones past the limit.</summary>
    /// <param name="chunkX">The chunk column.</param>
    /// <param name="chunkZ">The chunk row.</param>
    /// <param name="group">The group (it must hold the column).</param>
    public void Add(int chunkX, int chunkZ, ColumnGroup group)
    {
        var key = KeyOf(chunkX, chunkZ);
        _groups[key] = group;
        _order.Enqueue(key);
        while (_groups.Count > limit && _order.TryDequeue(out var oldest))
        {
            _ = _groups.TryRemove(oldest, out _);
        }
    }
}
