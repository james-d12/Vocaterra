using System.Collections.ObjectModel;

namespace Vocaterra.Game.Core.Items.Gear;

/// <summary>
/// What the player wears (I03a, inventory-plan 3.4): one stack of one item in each slot of each group of <see cref="EquipmentData"/>. Equipping moves an item from a bag slot to a slot, swapping with what is
/// worn there (the old item takes the bag slot the new one left); unequipping moves it back to a given bag slot or the first free one. Every change goes through the <see cref="IEquipRule"/>s first, and a refusal returns
/// its <see cref="EquipResult"/> and leaves the bag and the worn items exactly as they were. The code never names a group: it follows the data's flags. The bag's size, and the rule that a bag with items cannot
/// come off, are I03b's: they are one more rule in <see cref="EquipRules"/> and a larger <see cref="Inventory"/>.
/// </summary>
public sealed class Equipment
{
    private readonly ItemStack[] _slots;
    private readonly IReadOnlyList<IEquipRule> _rules;

    /// <summary>Nothing worn.</summary>
    /// <param name="data">The slot groups.</param>
    /// <param name="items">The item catalogue, where the worn items are looked up.</param>
    /// <param name="rules">The rules, asked in order; <see cref="EquipRules.Standard"/> when <see langword="null"/>.</param>
    public Equipment(EquipmentData data, IItemCatalogue items, IReadOnlyList<IEquipRule>? rules = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(items);
        Data = data;
        Items = items;
        _rules = rules ?? EquipRules.Standard();
        _slots = new ItemStack[data.SlotCount];
        Array.Fill(_slots, ItemStack.Empty);
    }

    /// <summary>The slot groups.</summary>
    public EquipmentData Data { get; }

    /// <summary>The item catalogue.</summary>
    public IItemCatalogue Items { get; }

    /// <summary>Every slot's contents, group by group (<see cref="EquipmentData.FlatIndex"/>).</summary>
    public IReadOnlyList<ItemStack> Slots => new ReadOnlyCollection<ItemStack>(_slots);

    /// <summary>Whether nothing is worn.</summary>
    public bool IsEmpty => Array.TrueForAll(_slots, static s => s.IsEmpty);

    /// <summary>What is worn in <paramref name="slot"/> (empty when nothing is).</summary>
    /// <param name="slot">A slot of the data.</param>
    public ItemStack Get(EquipSlot slot) => _slots[Data.FlatIndex(slot)];

    /// <summary>Every worn item with its slot, in slot order.</summary>
    public IEnumerable<(EquipSlot Slot, ItemDefinition Item)> Worn()
    {
        for (var flat = 0; flat < _slots.Length; flat++)
        {
            if (!_slots[flat].IsEmpty)
            {
                yield return (Data.SlotAt(flat), Items.Get(_slots[flat].Item));
            }
        }
    }

    /// <summary>The first slot that would take <paramref name="item"/> now, an empty one before a worn one, or <see langword="null"/> when no slot would (the item is refused everywhere).</summary>
    /// <param name="bag">The bag the rules read.</param>
    /// <param name="item">The item.</param>
    public EquipSlot? FindSlotFor(Inventory bag, ItemDefinition item)
    {
        ArgumentNullException.ThrowIfNull(bag);
        ArgumentNullException.ThrowIfNull(item);
        foreach (var wantEmpty in new[] { true, false })
        {
            foreach (var slot in Data.AllSlots())
            {
                var worn = Get(slot);
                if (worn.IsEmpty != wantEmpty)
                {
                    continue;
                }

                if (CanWear(bag, item, slot) == EquipResult.Done)
                {
                    return slot;
                }
            }
        }

        return null;
    }

    /// <summary>Whether <paramref name="item"/> could be worn in <paramref name="slot"/> now, swapping with what is there: <see cref="EquipResult.Done"/>, or the first rule's reason. Nothing changes.</summary>
    /// <param name="bag">The bag the rules read.</param>
    /// <param name="item">The item.</param>
    /// <param name="slot">A slot of the data.</param>
    public EquipResult CanWear(Inventory bag, ItemDefinition item, EquipSlot slot)
    {
        ArgumentNullException.ThrowIfNull(bag);
        ArgumentNullException.ThrowIfNull(item);
        if (!Data.Contains(slot))
        {
            return EquipResult.NoSuchSlot;
        }

        var worn = Get(slot);
        return EquipRules.Judge(_rules, new EquipChange(slot, item, worn.IsEmpty ? null : Items.Get(worn.Item)), this, bag);
    }

    /// <summary>
    /// Moves the item in a bag slot to <paramref name="slot"/>, the item worn there (if any) going to the bag slot it came from. Nothing changes unless the result is <see cref="EquipResult.Done"/>.
    /// </summary>
    /// <param name="bag">The bag.</param>
    /// <param name="bagSlot">The bag slot the item is in.</param>
    /// <param name="slot">The slot to wear it in.</param>
    public EquipResult Equip(Inventory bag, int bagSlot, EquipSlot slot)
    {
        ArgumentNullException.ThrowIfNull(bag);
        if (!Data.Contains(slot))
        {
            return EquipResult.NoSuchSlot;
        }

        if ((uint)bagSlot >= (uint)Inventory.SlotCount)
        {
            return EquipResult.NoSuchBagSlot;
        }

        var stack = bag.Slots[bagSlot];
        if (stack.IsEmpty)
        {
            return EquipResult.NothingToEquip;
        }

        if (stack.Count != 1)
        {
            return EquipResult.StackTooLarge;
        }

        var worn = Get(slot);
        var change = new EquipChange(slot, Items.Get(stack.Item), worn.IsEmpty ? null : Items.Get(worn.Item));
        var verdict = EquipRules.Judge(_rules, change, this, bag);
        if (verdict != EquipResult.Done)
        {
            return verdict;
        }

        _slots[Data.FlatIndex(slot)] = stack;
        bag.SetSlot(bagSlot, worn); // the swapped item takes the freed place; an empty stack empties it
        return EquipResult.Done;
    }

    /// <summary>
    /// Moves the item worn in <paramref name="slot"/> to a bag slot: the one asked for when it is empty, else (with <paramref name="bagSlot"/> negative) the first empty one. Nothing changes unless the result is <see cref="EquipResult.Done"/>.
    /// </summary>
    /// <param name="bag">The bag.</param>
    /// <param name="slot">The slot to empty.</param>
    /// <param name="bagSlot">The bag slot to put it in, or -1 for the first empty one.</param>
    public EquipResult Unequip(Inventory bag, EquipSlot slot, int bagSlot = -1)
    {
        ArgumentNullException.ThrowIfNull(bag);
        if (!Data.Contains(slot))
        {
            return EquipResult.NoSuchSlot;
        }

        var worn = Get(slot);
        if (worn.IsEmpty)
        {
            return EquipResult.NothingWorn;
        }

        var verdict = EquipRules.Judge(_rules, new EquipChange(slot, null, Items.Get(worn.Item)), this, bag);
        if (verdict != EquipResult.Done)
        {
            return verdict;
        }

        if (bagSlot >= Inventory.SlotCount)
        {
            return EquipResult.NoSuchBagSlot;
        }

        var target = bagSlot;
        if (bagSlot >= 0)
        {
            if (!bag.Slots[bagSlot].IsEmpty)
            {
                return EquipResult.BagSlotOccupied;
            }
        }
        else
        {
            target = FirstFree(bag);
            if (target < 0)
            {
                return EquipResult.BagFull;
            }
        }

        bag.SetSlot(target, worn);
        _slots[Data.FlatIndex(slot)] = ItemStack.Empty;
        return EquipResult.Done;
    }

    /// <summary>Puts a stack in a slot with no rule asked (a load, a test). A count of zero or less empties the slot.</summary>
    /// <param name="slot">A slot of the data.</param>
    /// <param name="stack">What to put there: one item.</param>
    /// <exception cref="ArgumentOutOfRangeException">The slot does not exist, the count is above 1 or the item has no definition.</exception>
    public void Set(EquipSlot slot, ItemStack stack)
    {
        var flat = Data.FlatIndex(slot);
        if (stack.Count <= 0)
        {
            _slots[flat] = ItemStack.Empty;
            return;
        }

        ArgumentOutOfRangeException.ThrowIfGreaterThan(stack.Count, 1, nameof(stack));
        _ = Items.Get(stack.Item);
        _slots[flat] = stack;
    }

    /// <summary>The slots as plain values, in <see cref="Slots"/> order (what a state keeps).</summary>
    public IReadOnlyList<ItemStack> Capture() => [.. _slots];

    /// <summary>Puts back what <see cref="Capture"/> gave; <see langword="null"/> or an empty list means nothing worn.</summary>
    /// <param name="slots">The slots in order.</param>
    /// <exception cref="ArgumentException">The list has the wrong number of slots.</exception>
    public void Restore(IReadOnlyList<ItemStack>? slots)
    {
        if (slots is null or { Count: 0 })
        {
            Array.Fill(_slots, ItemStack.Empty);
            return;
        }

        if (slots.Count != _slots.Length)
        {
            throw new ArgumentException($"The worn items have {slots.Count} slots, not {_slots.Length}.", nameof(slots));
        }

        for (var i = 0; i < slots.Count; i++)
        {
            Set(Data.SlotAt(i), slots[i]);
        }
    }

    private static int FirstFree(Inventory bag)
    {
        for (var i = 0; i < Inventory.SlotCount; i++)
        {
            if (bag.Slots[i].IsEmpty)
            {
                return i;
            }
        }

        return -1;
    }
}
