using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Gear;

namespace Vocaterra.Game.Core.Save;

/// <summary>
/// One worn item in a save (version 10, I03b): the id of its slot group in <c>game/equipment.json</c>, the slot's name inside the group and the item's name (<c>game/items.json</c>). Nothing here is an index, so reordering the
/// groups of <c>equipment.json</c> or the items of <c>items.json</c> does not scramble a save. A group, a slot or an item the data lacks loads as nothing (<see cref="ToStacks"/>), and a save across bundles reports it
/// (<see cref="SaveMigration"/>). A save with nothing worn has no <c>worn</c> entry.
/// </summary>
/// <param name="Group">The group's id (<c>clothing</c>, <c>backpack</c>).</param>
/// <param name="Slot">The slot's name inside the group (<c>body</c>, <c>accessory2</c>).</param>
/// <param name="Item">The item's name.</param>
public sealed record WornSave(string Group, string Slot, string Item)
{
    /// <summary>The worn items of an equipment in slot order, or <see langword="null"/> when nothing is worn (the save then leaves the entry out).</summary>
    /// <param name="equipment">The equipment.</param>
    public static IReadOnlyList<WornSave>? Of(Equipment equipment)
    {
        ArgumentNullException.ThrowIfNull(equipment);
        var worn = equipment.Worn().Select(entry => new WornSave(equipment.Data.Groups[entry.Slot.Group].Id, equipment.Data.Spec(entry.Slot).Name, entry.Item.Name)).ToArray();
        return worn.Length == 0 ? null : worn;
    }

    /// <summary>The slot this entry names in <paramref name="data"/>, or <see langword="null"/> when the data has no such group or slot.</summary>
    /// <param name="data">The slot groups.</param>
    public EquipSlot? Find(EquipmentData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var group = data.IndexOf(Group);
        if (group < 0)
        {
            return null;
        }

        var slots = data.Groups[group].Slots;
        for (var i = 0; i < slots.Count; i++)
        {
            if (string.Equals(slots[i].Name, Slot, StringComparison.Ordinal))
            {
                return new EquipSlot(group, i);
            }
        }

        return null;
    }

    /// <summary>Whether the data has this entry's slot and item, so it loads as something.</summary>
    /// <param name="data">The slot groups.</param>
    /// <param name="items">The item catalogue.</param>
    public bool Resolves(EquipmentData data, IItemCatalogue items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return Find(data) is not null && items.Find(Item) is not null;
    }

    /// <summary>
    /// The worn stacks a state holds (one per slot of the data, in the data's order) for a save's entries, or <see langword="null"/> when nothing resolves. An entry whose group, slot or item the data lacks is left out, and the
    /// second entry for one slot is too.
    /// </summary>
    /// <param name="worn">The save's entries, or <see langword="null"/>.</param>
    /// <param name="data">The slot groups.</param>
    /// <param name="items">The item catalogue.</param>
    public static IReadOnlyList<ItemStack>? ToStacks(IReadOnlyList<WornSave>? worn, EquipmentData data, IItemCatalogue items)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(items);
        if (worn is null or { Count: 0 })
        {
            return null;
        }

        var stacks = new ItemStack[data.SlotCount];
        Array.Fill(stacks, ItemStack.Empty);
        var any = false;
        foreach (var entry in worn)
        {
            if (entry.Find(data) is { } slot && items.Find(entry.Item) is { } item && stacks[data.FlatIndex(slot)].IsEmpty)
            {
                stacks[data.FlatIndex(slot)] = new ItemStack(item.Id, 1);
                any = true;
            }
        }

        return any ? stacks : null;
    }
}
