using System.Collections.ObjectModel;

namespace Vocaterra.Game.Core.Items.Gear;

/// <summary>
/// What the player wears (I03a, inventory-plan 3.4): one stack of one item in each slot of each group of <see cref="EquipmentData"/>. Equipping moves an item from a bag slot to a slot, swapping with what is
/// worn there (the old item takes the bag slot the new one left); unequipping moves it back to a given bag slot or the first free one. Every change goes through the <see cref="IEquipRule"/>s first, and a refusal returns
/// its <see cref="EquipResult"/> and leaves the bag and the worn items exactly as they were. The code never names a group: it follows the data's flags. A worn bag (an item with a <see cref="ItemDefinition.Capacity"/>) sets the
/// size of the <see cref="Inventory"/> (I03b): every change that goes through leaves the bag's active count at its base plus the worn capacity (<see cref="SyncBag"/>), and <see cref="BagCapacityRule"/> refuses a change that would
/// drop a slot holding an item.
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

    /// <summary>Counts every change of what is worn (I09): a drawing that caches what it made from the worn items rebuilds when this number moved.</summary>
    public int Revision { get; private set; }

    /// <summary>The slot groups.</summary>
    public EquipmentData Data { get; }

    /// <summary>The item catalogue.</summary>
    public IItemCatalogue Items { get; }

    /// <summary>Every slot's contents, group by group (<see cref="EquipmentData.FlatIndex"/>).</summary>
    public IReadOnlyList<ItemStack> Slots => new ReadOnlyCollection<ItemStack>(_slots);

    /// <summary>Whether nothing is worn.</summary>
    public bool IsEmpty => Array.TrueForAll(_slots, static s => s.IsEmpty);

    /// <summary>The bag slots the worn items add to the inventory's base: the sum of their <see cref="ItemDefinition.Capacity"/>.</summary>
    public int Capacity => CapacityOf(_slots);

    /// <summary>The bag slots a list of worn stacks (as <see cref="Capture"/> gives them) would add; <see langword="null"/> adds none.</summary>
    /// <param name="worn">The stacks, one per slot.</param>
    public int CapacityOf(IReadOnlyList<ItemStack>? worn)
    {
        var total = 0;
        foreach (var stack in worn ?? [])
        {
            if (!stack.IsEmpty && Items.Find(stack.Item)?.Capacity is { } capacity)
            {
                total += capacity;
            }
        }

        return total;
    }

    /// <summary>The inventory's active slot count when the items worn are <paramref name="worn"/> (null for nothing): the base and the capacity, never above the most the inventory has room for.</summary>
    /// <param name="bag">The inventory.</param>
    /// <param name="worn">The stacks, one per slot, or <see langword="null"/>.</param>
    public int ActiveSlotsFor(Inventory bag, IReadOnlyList<ItemStack>? worn)
    {
        ArgumentNullException.ThrowIfNull(bag);
        return Math.Min(bag.BaseSlots + CapacityOf(worn), bag.MaxSlots);
    }

    /// <summary>The inventory's active slot count after <paramref name="change"/> is made (the rules and the unequip path read it; nothing changes).</summary>
    /// <param name="bag">The inventory.</param>
    /// <param name="change">What changes.</param>
    public int ActiveSlotsAfter(Inventory bag, in EquipChange change)
    {
        ArgumentNullException.ThrowIfNull(bag);
        return Math.Clamp(bag.BaseSlots + Capacity - (change.Outgoing?.Capacity ?? 0) + (change.Incoming?.Capacity ?? 0), bag.BaseSlots, bag.MaxSlots);
    }

    /// <summary>Sets the inventory's active slot count to its base plus the worn capacity. Call it after <see cref="Set"/> or <see cref="Restore"/>; <see cref="Equip"/> and <see cref="Unequip"/> do it themselves.</summary>
    /// <param name="bag">The inventory.</param>
    /// <exception cref="InvalidOperationException">The count would drop a slot that holds an item.</exception>
    public void SyncBag(Inventory bag)
    {
        ArgumentNullException.ThrowIfNull(bag);
        bag.SetActiveCount(ActiveSlotsFor(bag, _slots));
    }

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

    /// <summary>What is worn in the first slot whose <c>wear</c> kind is <paramref name="wear"/> and holds something (empty when none does): the off-hand cell of the HUD finds its item this way, by the kind, never by the group's id (I04b).</summary>
    /// <param name="wear">The slot kind (<see cref="AppearanceRoles.OffHandWear"/>).</param>
    public ItemStack StackOfWear(string wear)
    {
        ArgumentNullException.ThrowIfNull(wear);
        for (var flat = 0; flat < _slots.Length; flat++)
        {
            if (!_slots[flat].IsEmpty && string.Equals(Data.Spec(Data.SlotAt(flat)).Wear, wear, StringComparison.Ordinal))
            {
                return _slots[flat];
            }
        }

        return ItemStack.Empty;
    }

    /// <summary>
    /// The slot a quick wear (a shift-click, <c>--equip</c>) puts <paramref name="item"/> in, or <see langword="null"/> when no slot would take it. Only slots whose group applies its stats are candidates (I10b,
    /// QA-2026-11-19-progression-3): an empty one first, else a worn one, swapped, so a coat put on over a shirt takes the shirt's place and counts. A group that gives no stats (the cosmetic layer) is never chosen here:
    /// the player fills it by dropping an item on its cell (<see cref="CanWear"/> and <see cref="Equip"/> still accept it there). The rule is the group's <see cref="EquipmentGroup.StatsApply"/> flag, never a group's id.
    /// </summary>
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
                if (worn.IsEmpty != wantEmpty || !Data.Groups[slot.Group].StatsApply)
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

    /// <summary>
    /// Why <see cref="FindSlotFor"/> found no slot for <paramref name="item"/>: the verdict at the first slot of its wear kind (a slot that applies its stats before one that does not), <see cref="EquipResult.NotWearable"/> for an item
    /// with no wear kind, <see cref="EquipResult.WrongSlot"/> when no slot has its kind.
    /// </summary>
    /// <param name="bag">The bag the rules read.</param>
    /// <param name="item">The item.</param>
    public EquipResult RefusalFor(Inventory bag, ItemDefinition item)
    {
        ArgumentNullException.ThrowIfNull(bag);
        ArgumentNullException.ThrowIfNull(item);
        if (item.Wear is null)
        {
            return EquipResult.NotWearable;
        }

        var kind = Data.AllSlots().Where(s => string.Equals(Data.Spec(s).Wear, item.Wear, StringComparison.Ordinal)).OrderByDescending(s => Data.Groups[s.Group].StatsApply).Select(static s => (EquipSlot?)s).FirstOrDefault();
        var verdict = kind is { } slot ? CanWear(bag, item, slot) : EquipResult.WrongSlot;
        return verdict == EquipResult.Done ? EquipResult.WrongSlot : verdict; // only a slot that gives no stats takes it: a quick wear never fills one
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

    /// <summary>Whether what is worn in <paramref name="slot"/> could be taken out now (a bag with items in the slots it would drop cannot): <see cref="EquipResult.Done"/> (also for an empty slot), or the first rule's reason. Nothing changes.</summary>
    /// <param name="bag">The bag the rules read.</param>
    /// <param name="slot">A slot of the data.</param>
    public EquipResult CanTakeOff(Inventory bag, EquipSlot slot)
    {
        ArgumentNullException.ThrowIfNull(bag);
        if (!Data.Contains(slot))
        {
            return EquipResult.NoSuchSlot;
        }

        var worn = Get(slot);
        return worn.IsEmpty ? EquipResult.Done : EquipRules.Judge(_rules, new EquipChange(slot, null, Items.Get(worn.Item)), this, bag);
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

        if ((uint)bagSlot >= (uint)bag.SlotCount)
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
        Revision++;
        bag.SetSlot(bagSlot, worn); // the swapped item takes the freed place; an empty stack empties it
        SyncBag(bag); // a bag that went on gives its slots; one swapped for a smaller one dropped empty slots (the rule saw to that)
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

        var change = new EquipChange(slot, null, Items.Get(worn.Item));
        var verdict = EquipRules.Judge(_rules, change, this, bag);
        if (verdict != EquipResult.Done)
        {
            return verdict;
        }

        var room = ActiveSlotsAfter(bag, change); // a bag that comes off takes its slots with it: the item goes into the slots that stay
        if (bagSlot >= room)
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
            target = FirstFree(bag, room);
            if (target < 0)
            {
                return EquipResult.BagFull;
            }
        }

        bag.SetSlot(target, worn);
        _slots[Data.FlatIndex(slot)] = ItemStack.Empty;
        Revision++;
        SyncBag(bag);
        return EquipResult.Done;
    }

    /// <summary>Puts a stack in a slot with no rule asked (a load, a test). A count of zero or less empties the slot.</summary>
    /// <param name="slot">A slot of the data.</param>
    /// <param name="stack">What to put there: one item.</param>
    /// <exception cref="ArgumentOutOfRangeException">The slot does not exist, the count is above 1 or the item has no definition.</exception>
    public void Set(EquipSlot slot, ItemStack stack)
    {
        var flat = Data.FlatIndex(slot);
        Revision++;
        if (stack.Count <= 0)
        {
            _slots[flat] = ItemStack.Empty;
            return;
        }

        ArgumentOutOfRangeException.ThrowIfGreaterThan(stack.Count, 1, nameof(stack));
        _ = Items.Get(stack.Item);
        _slots[flat] = stack;
    }

    /// <summary>Sets the wear of what is worn in <paramref name="slot"/> (I07); nothing when the slot is empty. It leaves <see cref="Revision"/> alone: the look of an item does not change as it wears.</summary>
    /// <param name="slot">A slot of the data.</param>
    /// <param name="wear">The new wear.</param>
    public void SetWear(EquipSlot slot, int wear)
    {
        var flat = Data.FlatIndex(slot);
        if (!_slots[flat].IsEmpty)
        {
            _slots[flat] = _slots[flat].WithWear(wear);
        }
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
            Revision++;
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

    private static int FirstFree(Inventory bag, int room)
    {
        for (var i = 0; i < room; i++)
        {
            if (bag.Slots[i].IsEmpty)
            {
                return i;
            }
        }

        return -1;
    }
}
