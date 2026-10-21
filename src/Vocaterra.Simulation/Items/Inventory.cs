using System.Collections.ObjectModel;

namespace Vocaterra.Simulation.Items;

/// <summary>
/// The player's inventory: <see cref="SlotCount"/> slots of stacks, the first <see cref="HotbarSize"/> of them the hotbar. Adding fills the
/// existing stacks of the kind first (lowest slot first), then the empty slots; a stack never exceeds <see cref="ItemDefinition.MaxStack"/>.
/// Everything is integer arithmetic over the slots, so the same operations give the same contents on every machine.
/// </summary>
public sealed class Inventory
{
    /// <summary>The number of slots.</summary>
    public const int SlotCount = 40;

    /// <summary>The number of slots the hotbar shows (the first ones).</summary>
    public const int HotbarSize = 10;

    private readonly ItemStack[] _slots = new ItemStack[SlotCount];

    /// <summary>An empty inventory.</summary>
    public Inventory()
    {
        for (var i = 0; i < SlotCount; i++)
        {
            _slots[i] = ItemStack.Empty;
        }
    }

    /// <summary>The slots, in order (the hotbar is the first <see cref="HotbarSize"/>).</summary>
    public IReadOnlyList<ItemStack> Slots => new ReadOnlyCollection<ItemStack>(_slots);

    /// <summary>An independent copy of this inventory (for what-if checks).</summary>
    public Inventory Copy()
    {
        var copy = new Inventory();
        for (var i = 0; i < SlotCount; i++)
        {
            copy._slots[i] = _slots[i];
        }

        return copy;
    }

    /// <summary>Whether a slot index is one of the hotbar's.</summary>
    public static bool IsHotbarSlot(int slot) => (uint)slot < (uint)HotbarSize;

    /// <summary>The total number of <paramref name="item"/> in every slot.</summary>
    public int Count(ItemId item)
    {
        var total = 0;
        foreach (var stack in _slots)
        {
            if (!stack.IsEmpty && stack.Item == item)
            {
                total += stack.Count;
            }
        }

        return total;
    }

    /// <summary>Whether all <paramref name="count"/> of <paramref name="item"/> fit into the inventory now (nothing is changed).</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is not positive.</exception>
    public bool CanAdd(ItemId item, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        return Fit(item, count, apply: false) == 0;
    }

    /// <summary>
    /// Adds <paramref name="count"/> of <paramref name="item"/>, as far as the inventory has room.
    /// </summary>
    /// <returns>How many did not fit (zero when all of them went in).</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is not positive.</exception>
    public int Add(ItemId item, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        return Fit(item, count, apply: true);
    }

    /// <summary>Removes <paramref name="count"/> of <paramref name="item"/> from the slots, lowest slot first, all or nothing.</summary>
    /// <returns><see langword="true"/> when that many were there and were removed.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is not positive.</exception>
    public bool Remove(ItemId item, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        if (Count(item) < count)
        {
            return false;
        }

        var left = count;
        for (var i = 0; i < SlotCount && left > 0; i++)
        {
            if (_slots[i].IsEmpty || _slots[i].Item != item)
            {
                continue;
            }

            var take = Math.Min(left, _slots[i].Count);
            left -= take;
            SetSlot(i, new ItemStack(item, _slots[i].Count - take));
        }

        return true;
    }

    /// <summary>Removes <paramref name="count"/> from one slot.</summary>
    /// <returns><see langword="true"/> when the slot held at least that many.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The slot index or count is out of range.</exception>
    public bool RemoveFromSlot(int slot, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        ArgumentOutOfRangeException.ThrowIfNegative(slot);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(slot, SlotCount);
        var stack = _slots[slot];
        if (stack.IsEmpty || stack.Count < count)
        {
            return false;
        }

        SetSlot(slot, new ItemStack(stack.Item, stack.Count - count));
        return true;
    }

    /// <summary>Puts a stack in a slot (used for the starting kit and by tests). A count of zero or less empties the slot.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The slot is out of range, or the count is above the item's stack limit.</exception>
    public void SetSlot(int slot, ItemStack stack)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(slot);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(slot, SlotCount);
        if (stack.Count <= 0)
        {
            _slots[slot] = ItemStack.Empty;
            return;
        }

        if (stack.Count > ItemDefinition.Get(stack.Item).MaxStack)
        {
            throw new ArgumentOutOfRangeException(nameof(stack), stack, "The count is above the stack limit of the item.");
        }

        _slots[slot] = stack;
    }

    private int Fit(ItemId item, int count, bool apply)
    {
        var limit = ItemDefinition.Get(item).MaxStack;
        var left = count;
        var working = apply ? null : (ItemStack[])_slots.Clone();
        for (var pass = 0; pass < 2 && left > 0; pass++)
        {
            for (var i = 0; i < SlotCount && left > 0; i++)
            {
                var current = working is null ? _slots[i] : working[i];
                var matches = pass == 0 ? !current.IsEmpty && current.Item == item : current.IsEmpty;
                if (!matches)
                {
                    continue;
                }

                var held = pass == 0 ? current.Count : 0;
                var take = Math.Min(left, limit - held);
                left -= take;
                var next = new ItemStack(item, held + take);
                if (working is null)
                {
                    SetSlot(i, next);
                }
                else
                {
                    working[i] = next;
                }
            }
        }

        return left;
    }
}
