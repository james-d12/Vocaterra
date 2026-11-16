using System.Collections.ObjectModel;

namespace Vocaterra.Game.Core.Items;

/// <summary>
/// The player's inventory: <see cref="SlotCount"/> active slots of stacks, the first <see cref="HotbarSize"/> of them the hotbar (I03b, inventory-plan 3.3). A bag starts at <see cref="BaseSlots"/> slots (the hotbar
/// and the rows under it; the bundle's <c>equipment.json</c> says how many) and grows with the bag the player wears: the array is sized to <see cref="MaxSlots"/>, the base plus the largest
/// <see cref="ItemDefinition.Capacity"/> of the catalogue, and <see cref="SetActiveCount"/> (called by <see cref="Gear.Equipment"/> when a bag goes on or off) moves the active count. Slots past the active count are always empty,
/// and every operation (adding, removing, exchanging, setting a slot) sees only the active ones.
/// Adding fills the existing stacks of the kind first (lowest slot first), then the empty slots; a stack never exceeds the <see cref="ItemDefinition.MaxStack"/> of its item in the <see cref="IItemCatalogue"/> (the data's stack limit).
/// Everything is integer arithmetic over the slots, so the same operations give the same contents on every machine.
/// </summary>
public sealed class Inventory
{
    /// <summary>The number of slots the hotbar shows (the first ones).</summary>
    public const int HotbarSize = 10;

    /// <summary>The base slot count of an inventory built with no game data (a test's, the host's check tools): the hotbar and two rows of ten. A bundle's own is <see cref="Gear.EquipmentData.BagSlots"/>.</summary>
    public const int DefaultBaseSlots = 30;

    private readonly ItemStack[] _slots;
    private int _active;

    /// <summary>An empty inventory with no bag worn.</summary>
    /// <param name="items">The item catalogue: where each item's stack limit and each bag's capacity are read.</param>
    /// <param name="baseSlots">The slots with no bag worn (at least the hotbar); <see cref="DefaultBaseSlots"/> unless the bundle's data says otherwise.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="baseSlots"/> is below <see cref="HotbarSize"/>.</exception>
    public Inventory(IItemCatalogue items, int baseSlots = DefaultBaseSlots)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentOutOfRangeException.ThrowIfLessThan(baseSlots, HotbarSize);
        Items = items;
        BaseSlots = baseSlots;
        MaxSlots = baseSlots + items.LargestCapacity;
        _active = baseSlots;
        _slots = new ItemStack[MaxSlots];
        Array.Fill(_slots, ItemStack.Empty);
    }

    /// <summary>The item catalogue the stack limits come from.</summary>
    public IItemCatalogue Items { get; }

    /// <summary>The slots with no bag worn.</summary>
    public int BaseSlots { get; }

    /// <summary>The most slots the inventory can have: the base and the largest bag of the catalogue.</summary>
    public int MaxSlots { get; }

    /// <summary>The slots the player has now: <see cref="BaseSlots"/> and the capacity of the worn bag. Every consumer loops to this, never to <see cref="MaxSlots"/>.</summary>
    public int SlotCount => _active;

    /// <summary>The active slots, in order (the hotbar is the first <see cref="HotbarSize"/>): <see cref="SlotCount"/> of them.</summary>
    public IReadOnlyList<ItemStack> Slots => new ReadOnlyCollection<ItemStack>(new ArraySegment<ItemStack>(_slots, 0, _active));

    /// <summary>
    /// Sets the number of active slots (a bag went on or off). Nothing changes unless every slot the change drops is empty.
    /// </summary>
    /// <param name="count">The new count, from <see cref="BaseSlots"/> to <see cref="MaxSlots"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">The count is outside the base and the maximum.</exception>
    /// <exception cref="InvalidOperationException">A slot the change would drop holds an item (the bag rule of <see cref="Gear.EquipRules"/> refuses that first).</exception>
    public void SetActiveCount(int count)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, BaseSlots);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, MaxSlots);
        for (var i = count; i < _active; i++)
        {
            if (!_slots[i].IsEmpty)
            {
                throw new InvalidOperationException($"Slot {i} holds an item, so the inventory cannot shrink to {count} slots.");
            }
        }

        _active = count;
    }

    /// <summary>Whether every slot from <paramref name="count"/> up to the active count is empty (the inventory could shrink to <paramref name="count"/> slots now).</summary>
    /// <param name="count">The smaller count.</param>
    public bool IsEmptyFrom(int count)
    {
        for (var i = Math.Max(count, 0); i < _active; i++)
        {
            if (!_slots[i].IsEmpty)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>An independent copy of this inventory (for what-if checks).</summary>
    public Inventory Copy()
    {
        var copy = new Inventory(Items, BaseSlots) { _active = _active };
        Array.Copy(_slots, copy._slots, _slots.Length);
        return copy;
    }

    /// <summary>Whether a slot index is one of the hotbar's.</summary>
    public static bool IsHotbarSlot(int slot) => (uint)slot < (uint)HotbarSize;

    /// <summary>The total number of <paramref name="item"/> in every slot.</summary>
    public int Count(ItemId item)
    {
        var total = 0;
        for (var i = 0; i < _active; i++)
        {
            var stack = _slots[i];
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
    /// Whether an exchange can be made (nothing is changed): <paramref name="paidCount"/> of <paramref name="paid"/> are in the inventory, and all <paramref name="count"/>
    /// of <paramref name="item"/> fit once they are gone. The coins of a purchase free their slot for the items bought, and the goods of a sale theirs for the coins
    /// (village-plan V19, the review's R8).
    /// </summary>
    /// <param name="paid">What goes out.</param>
    /// <param name="paidCount">How many go out; zero for nothing.</param>
    /// <param name="item">What comes in.</param>
    /// <param name="count">How many come in.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="paidCount"/> is negative, or <paramref name="count"/> is not positive.</exception>
    public bool CanExchange(ItemId paid, int paidCount, ItemId item, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(paidCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        if (Count(paid) < paidCount)
        {
            return false;
        }

        if (paidCount == 0)
        {
            return CanAdd(item, count);
        }

        var after = Copy();
        _ = after.Remove(paid, paidCount);
        return after.CanAdd(item, count);
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
        for (var i = 0; i < _active && left > 0; i++)
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
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(slot, _active);
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
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(slot, _active);
        if (stack.Count <= 0)
        {
            _slots[slot] = ItemStack.Empty;
            return;
        }

        if (stack.Count > Items.Get(stack.Item).MaxStack)
        {
            throw new ArgumentOutOfRangeException(nameof(stack), stack, "The count is above the stack limit of the item.");
        }

        _slots[slot] = stack;
    }

    private int Fit(ItemId item, int count, bool apply)
    {
        var limit = Items.Get(item).MaxStack;
        var left = count;
        var working = apply ? null : (ItemStack[])_slots.Clone();
        for (var pass = 0; pass < 2 && left > 0; pass++)
        {
            for (var i = 0; i < _active && left > 0; i++)
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
