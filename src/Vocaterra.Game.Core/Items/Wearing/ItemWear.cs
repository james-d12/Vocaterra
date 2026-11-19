using Vocaterra.Game.Core.Items.Gear;

namespace Vocaterra.Game.Core.Items.Wearing;

/// <summary>
/// The wear engine (I07, inventory-plan 3.12): it applies one occurrence of a cause to the stacks the cause's <see cref="IWearSource"/> reaches, adding each durable item's rate, clamping at its maximum and telling once when an item reaches it;
/// and it repairs. It holds no state of its own: the wear lives in the <see cref="ItemStack"/>s of the bag and the worn slots, so a move, a save and the digest carry it. A worn-out item is never removed.
/// </summary>
public sealed class ItemWear
{
    private readonly Inventory _bag;
    private readonly Equipment _equipment;
    private readonly WearSources _sources;
    private readonly Func<int> _selectedSlot;
    private readonly Action<WornOutNotice> _wornOut;

    /// <summary>Creates the engine over a bag and the worn items.</summary>
    /// <param name="bag">The inventory.</param>
    /// <param name="equipment">The worn items.</param>
    /// <param name="sources">The wear sources by id.</param>
    /// <param name="selectedSlot">The selected hotbar slot now (the tool in the hand).</param>
    /// <param name="wornOut">Called once for each item that reaches its durability.</param>
    public ItemWear(Inventory bag, Equipment equipment, WearSources sources, Func<int> selectedSlot, Action<WornOutNotice> wornOut)
    {
        ArgumentNullException.ThrowIfNull(bag);
        ArgumentNullException.ThrowIfNull(equipment);
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(selectedSlot);
        ArgumentNullException.ThrowIfNull(wornOut);
        _bag = bag;
        _equipment = equipment;
        _sources = sources;
        _selectedSlot = selectedSlot;
        _wornOut = wornOut;
    }

    /// <summary>The selected hotbar slot (what the use source wears).</summary>
    public int SelectedSlot => _selectedSlot();

    /// <summary>Every worn slot that holds something, in slot order.</summary>
    public IEnumerable<WearSlot> WornSlots()
    {
        for (var flat = 0; flat < _equipment.Data.SlotCount; flat++)
        {
            if (!_equipment.Get(_equipment.Data.SlotAt(flat)).IsEmpty)
            {
                yield return new WearSlot(true, flat);
            }
        }
    }

    /// <summary>
    /// Applies <paramref name="quantity"/> occurrences of a cause: every stack its source reaches whose item's durability names the source gains <c>rate * quantity</c> wear, never past the maximum. Returns whether anything wore.
    /// A source the engine does not have, a non-positive quantity, or no durable item reached wears nothing.
    /// </summary>
    /// <param name="sourceId">The source's id (<see cref="UseWearSource.SourceId"/>, <see cref="TimeWearSource.SourceId"/>, <see cref="DamageWearSource.SourceId"/>).</param>
    /// <param name="quantity">How many units of the cause happened: gathers, hours or points of damage.</param>
    public bool Apply(string sourceId, long quantity)
    {
        ArgumentNullException.ThrowIfNull(sourceId);
        if (quantity <= 0 || _sources.Find(sourceId) is not { } source)
        {
            return false;
        }

        var wore = false;
        foreach (var slot in source.Targets(this))
        {
            var stack = Get(slot);
            if (stack.IsEmpty || _bag.Items.Find(stack.Item) is not { Durability: { } durability } definition || !string.Equals(durability.Source, sourceId, StringComparison.Ordinal) || stack.Wear >= durability.Max)
            {
                continue;
            }

            var next = (int)Math.Min(durability.Max, stack.Wear + (durability.Rate * Math.Min(quantity, durability.Max)));
            Set(slot, stack, next);
            wore = true;
            if (next >= durability.Max)
            {
                _wornOut(new WornOutNotice(stack.Item, definition.Tool is not null));
            }
        }

        return wore;
    }

    /// <summary>Whether a stack of <paramref name="item"/> in the bag or worn has any wear (a repair has something to do).</summary>
    /// <param name="item">The item.</param>
    public bool NeedsRepair(ItemId item) => MostWorn(item) is not null;

    /// <summary>Restores the most worn stack of <paramref name="item"/> (the bag first on a tie, then the worn slots) to no wear. Returns whether there was one.</summary>
    /// <param name="item">The item.</param>
    public bool Repair(ItemId item)
    {
        if (MostWorn(item) is not { } slot)
        {
            return false;
        }

        Set(slot, Get(slot), 0);
        return true;
    }

    /// <summary>The stack in a slot.</summary>
    /// <param name="slot">The slot.</param>
    public ItemStack Get(WearSlot slot)
    {
        if (slot.IsWorn)
        {
            return slot.Index >= 0 && slot.Index < _equipment.Data.SlotCount ? _equipment.Get(_equipment.Data.SlotAt(slot.Index)) : ItemStack.Empty;
        }

        return slot.Index >= 0 && slot.Index < _bag.SlotCount ? _bag.Slots[slot.Index] : ItemStack.Empty;
    }

    private WearSlot? MostWorn(ItemId item)
    {
        WearSlot? best = null;
        var bestWear = 0;
        for (var i = 0; i < _bag.SlotCount; i++)
        {
            Consider(new WearSlot(false, i), item, ref best, ref bestWear);
        }

        for (var flat = 0; flat < _equipment.Data.SlotCount; flat++)
        {
            Consider(new WearSlot(true, flat), item, ref best, ref bestWear);
        }

        return best;
    }

    private void Consider(WearSlot slot, ItemId item, ref WearSlot? best, ref int bestWear)
    {
        var stack = Get(slot);
        if (!stack.IsEmpty && stack.Item == item && stack.Wear > bestWear)
        {
            best = slot;
            bestWear = stack.Wear;
        }
    }

    private void Set(WearSlot slot, ItemStack stack, int wear)
    {
        if (slot.IsWorn)
        {
            _equipment.SetWear(_equipment.Data.SlotAt(slot.Index), wear);
        }
        else
        {
            _bag.SetSlot(slot.Index, stack.WithWear(wear));
        }
    }
}
