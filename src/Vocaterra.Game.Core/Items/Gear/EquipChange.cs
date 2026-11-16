namespace Vocaterra.Game.Core.Items.Gear;

/// <summary>
/// A change of the worn items an <see cref="IEquipRule"/> judges (I03a): an item going into a slot, an item leaving it, or both (a swap). Equipping fills <paramref name="Incoming"/> (and
/// <paramref name="Outgoing"/> when the slot is worn already); unequipping fills only <paramref name="Outgoing"/>.
/// </summary>
/// <param name="Slot">The slot that changes.</param>
/// <param name="Incoming">The item put in, or <see langword="null"/> when the change takes one out.</param>
/// <param name="Outgoing">The item taken out of the slot, or <see langword="null"/> when it was empty.</param>
public readonly record struct EquipChange(EquipSlot Slot, ItemDefinition? Incoming, ItemDefinition? Outgoing);
