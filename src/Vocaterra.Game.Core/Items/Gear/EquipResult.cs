namespace Vocaterra.Game.Core.Items.Gear;

/// <summary>
/// What an equip or unequip came to (I03a): <see cref="Done"/>, or the reason it was refused, which leaves the bag and the worn items exactly as they were. The panel (I04) maps each reason to a line.
/// The numbers are carried in <see cref="Event.SimulationEvent.Amount"/> and are never changed or reused; a new reason is appended.
/// </summary>
public enum EquipResult
{
    /// <summary>The change was made.</summary>
    Done = 0,

    /// <summary>The slot named does not exist (a group or slot index outside the data).</summary>
    NoSuchSlot = 1,

    /// <summary>The bag slot named does not exist.</summary>
    NoSuchBagSlot = 2,

    /// <summary>Equip: the bag slot holds nothing.</summary>
    NothingToEquip = 3,

    /// <summary>Unequip: the slot holds nothing.</summary>
    NothingWorn = 4,

    /// <summary>The item has no <c>wear</c>: it cannot be worn anywhere.</summary>
    NotWearable = 5,

    /// <summary>The item is worn on another part than the slot's (a hat in the legs slot).</summary>
    WrongSlot = 6,

    /// <summary>The slot's group takes none of the item's tags (clothing in the armour group).</summary>
    WrongKind = 7,

    /// <summary>The group allows one of a kind and the item is worn in another slot of it already.</summary>
    Duplicate = 8,

    /// <summary>The bag slot holds more than one of the item: a slot of worn gear holds one.</summary>
    StackTooLarge = 9,

    /// <summary>Unequip: the bag slot asked for is not empty.</summary>
    BagSlotOccupied = 10,

    /// <summary>Unequip: the bag has no empty slot.</summary>
    BagFull = 11,

    /// <summary>A bag (an item with a capacity) would come off, or be swapped for a smaller one, while a slot it would drop holds an item (I03b). Empty those slots first.</summary>
    BagNotEmpty = 12,
}
