using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Water.Flow;

/// <summary>
/// The cells waiting for a flow update, served in a fixed order: due tick, then y (low first), then z, then x. A cell is queued once; scheduling it again
/// keeps the earlier due tick. Nothing here depends on hash order, so two runs that schedule the same cells serve them in the same order, and the queue
/// saves as a plain sorted list.
/// </summary>
public sealed class FlowQueue
{
    private readonly SortedSet<FlowEntry> _entries = new(FlowEntry.Order);
    private readonly Dictionary<BlockPos, long> _due = [];

    /// <summary>How many cells are waiting.</summary>
    public int Count => _entries.Count;

    /// <summary>Whether the cell is waiting.</summary>
    public bool Contains(BlockPos position) => _due.ContainsKey(position);

    /// <summary>Queues a cell to fall due at <paramref name="due"/>; a cell already queued keeps the earlier of the two ticks. Returns whether the queue changed.</summary>
    public bool Schedule(BlockPos position, long due)
    {
        if (_due.TryGetValue(position, out var existing))
        {
            if (existing <= due)
            {
                return false;
            }

            _entries.Remove(new FlowEntry(existing, position));
        }

        _due[position] = due;
        _entries.Add(new FlowEntry(due, position));
        return true;
    }

    /// <summary>Takes the first entry if it is due at <paramref name="tick"/> (its due tick is not after it).</summary>
    public bool TryDequeue(long tick, out FlowEntry entry)
    {
        if (_entries.Count > 0 && _entries.Min.Due <= tick)
        {
            entry = _entries.Min;
            _entries.Remove(entry);
            _due.Remove(entry.Position);
            return true;
        }

        entry = default;
        return false;
    }

    /// <summary>The waiting entries in serving order.</summary>
    public IReadOnlyList<FlowEntry> Snapshot() => _entries.ToArray();

    /// <summary>Empties the queue.</summary>
    public void Clear()
    {
        _entries.Clear();
        _due.Clear();
    }

    /// <summary>Replaces the queue's content with the entries (a save puts its queue back with this).</summary>
    public void Restore(IEnumerable<FlowEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        Clear();
        foreach (var entry in entries)
        {
            _ = Schedule(entry.Position, entry.Due);
        }
    }
}
