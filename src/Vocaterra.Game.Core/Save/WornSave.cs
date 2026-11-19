using System.Text.Json.Serialization;
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
/// <param name="Wear">The item's wear (version 11, I07); 0, and left out of the file, for an item that is not worn.</param>
public sealed record WornSave(string Group, string Slot, string Item, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int Wear = 0)
{
    /// <summary>The worn items of an equipment in slot order, or <see langword="null"/> when nothing is worn (the save then leaves the entry out).</summary>
    /// <param name="equipment">The equipment.</param>
    public static IReadOnlyList<WornSave>? Of(Equipment equipment)
    {
        ArgumentNullException.ThrowIfNull(equipment);
        var worn = equipment.Worn().Select(entry => new WornSave(equipment.Data.Groups[entry.Slot.Group].Id, equipment.Data.Spec(entry.Slot).Name, entry.Item.Name, equipment.Get(entry.Slot).Wear)).ToArray();
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
    /// second entry for one slot is too, and so is one whose item its slot does not take (see <see cref="Resolve"/>, which says which).
    /// </summary>
    /// <param name="worn">The save's entries, or <see langword="null"/>.</param>
    /// <param name="data">The slot groups.</param>
    /// <param name="items">The item catalogue.</param>
    public static IReadOnlyList<ItemStack>? ToStacks(IReadOnlyList<WornSave>? worn, EquipmentData data, IItemCatalogue items) => Resolve(worn, data, items).Stacks;

    /// <summary>
    /// What a save's worn entries come to in this data: the stacks that load where they were worn, the entries the data lacks (<see cref="WornLoad.Dropped"/>) and the items worn where their slot does not take them
    /// (<see cref="WornLoad.Misfits"/>, the fit check of a load: the loader puts them in the bag).
    /// </summary>
    /// <param name="worn">The save's entries, or <see langword="null"/>.</param>
    /// <param name="data">The slot groups.</param>
    /// <param name="items">The item catalogue.</param>
    public static WornLoad Resolve(IReadOnlyList<WornSave>? worn, EquipmentData data, IItemCatalogue items)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(items);
        if (worn is null or { Count: 0 })
        {
            return new WornLoad(null, [], []);
        }

        var stacks = new ItemStack[data.SlotCount];
        Array.Fill(stacks, ItemStack.Empty);
        var any = false;
        var dropped = new List<string>();
        var misfits = new List<(ItemStack Stack, string Where)>();
        foreach (var entry in worn)
        {
            var where = $"{entry.Group} {entry.Slot}";
            if (entry.Find(data) is not { } slot)
            {
                dropped.Add($"{entry.Item} worn in {where}: the bundle has no such slot");
            }
            else if (items.Find(entry.Item) is not { } item)
            {
                dropped.Add($"{entry.Item} worn in {where}: the bundle has no such item");
            }
            else if (!stacks[data.FlatIndex(slot)].IsEmpty)
            {
                dropped.Add($"{entry.Item} worn in {where}: the slot was named twice");
            }
            else if (!data.Fits(item, slot))
            {
                misfits.Add((new ItemStack(item.Id, 1, Wears.Clamp(item, entry.Wear)), $"{entry.Item} worn in {where}: that slot does not take it"));
            }
            else
            {
                stacks[data.FlatIndex(slot)] = new ItemStack(item.Id, 1, Wears.Clamp(item, entry.Wear));
                any = true;
            }
        }

        return new WornLoad(any ? stacks : null, misfits, dropped);
    }
}

/// <summary>A save's worn entries as this data reads them (<see cref="WornSave.Resolve"/>).</summary>
/// <param name="Stacks">The worn stacks, one per slot of the data, or <see langword="null"/> when nothing loads as worn.</param>
/// <param name="Misfits">The items worn where their slot does not take them, each with a line saying where.</param>
/// <param name="Dropped">The entries the data lacks or that repeat a slot, each as a line.</param>
public sealed record WornLoad(IReadOnlyList<ItemStack>? Stacks, IReadOnlyList<(ItemStack Stack, string Where)> Misfits, IReadOnlyList<string> Dropped);
