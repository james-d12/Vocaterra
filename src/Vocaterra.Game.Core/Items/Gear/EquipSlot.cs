namespace Vocaterra.Game.Core.Items.Gear;

/// <summary>
/// One place an item is worn (I03a): the index of its slot group in <c>game/equipment.json</c> and the index of the slot inside that group, both in the file's order. Commands carry the two numbers,
/// so a replay needs no names; the data's order is part of a bundle's identity as the items' ids are.
/// </summary>
/// <param name="Group">The group's index.</param>
/// <param name="Index">The slot's index inside the group.</param>
public readonly record struct EquipSlot(int Group, int Index);
