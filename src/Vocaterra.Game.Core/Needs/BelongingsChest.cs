using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Needs;

/// <summary>
/// The belongings a death left (N04, adaptation design 9.7): the cell of the <see cref="BlockId.Chest"/> block and what is in it, one total per item kind in the order the bag
/// held them. It is plain state: the session places and removes the block, the save keeps the cell and the stacks (the block itself is in the world's block edits).
/// </summary>
public sealed class BelongingsChest
{
    private readonly List<(ItemId Item, int Count)> _items = [];

    /// <summary>A chest at a cell, holding nothing yet.</summary>
    /// <param name="cell">The cell of its block.</param>
    public BelongingsChest(BlockPos cell) => Cell = cell;

    /// <summary>The cell of the chest's block.</summary>
    public BlockPos Cell { get; }

    /// <summary>What is in the chest, one total per item kind.</summary>
    public IReadOnlyList<(ItemId Item, int Count)> Items => _items;

    /// <summary>Whether nothing is left in it.</summary>
    public bool IsEmpty => _items.Count == 0;

    /// <summary>The number of items in it, all kinds together.</summary>
    public int Total => _items.Sum(static i => i.Count);

    /// <summary>Puts items in (a total per kind adds to the kind's).</summary>
    /// <param name="item">The kind.</param>
    /// <param name="count">How many; zero or less adds nothing.</param>
    public void Add(ItemId item, int count)
    {
        if (count <= 0)
        {
            return;
        }

        var index = _items.FindIndex(i => i.Item == item);
        if (index >= 0)
        {
            _items[index] = (item, _items[index].Count + count);
        }
        else
        {
            _items.Add((item, count));
        }
    }

    /// <summary>
    /// Takes back what the bag can hold, kind by kind in order; what does not fit stays.
    /// </summary>
    /// <param name="bag">The player's inventory.</param>
    /// <returns>How many items went into the bag.</returns>
    public int TakeInto(Inventory bag)
    {
        ArgumentNullException.ThrowIfNull(bag);
        var taken = 0;
        for (var i = 0; i < _items.Count; i++)
        {
            var (item, count) = _items[i];
            var left = bag.Add(item, count);
            taken += count - left;
            _items[i] = (item, left);
        }

        _items.RemoveAll(static i => i.Count <= 0);
        return taken;
    }
}
