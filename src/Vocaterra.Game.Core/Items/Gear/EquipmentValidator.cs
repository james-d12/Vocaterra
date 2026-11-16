using System.Text.RegularExpressions;

namespace Vocaterra.Game.Core.Items.Gear;

/// <summary>
/// The checks of <c>game/equipment.json</c> and of the gear in <c>game/items.json</c> that need more than a shape (I03a): group ids that are unique keys, groups that have slots with unique names and
/// wear kinds that are keys, accepted tags and labels that are well formed, layers from 1 to 9; and every item that has a <c>wear</c> has a slot of that kind in a group that accepts one of its tags and a stack
/// limit of 1, an inventory base from the hotbar's size up, and an item with <c>stats</c> or a <c>look</c> can be worn. <see cref="Content.GameDataValidator"/> runs it with the other files' checks.
/// </summary>
internal static partial class EquipmentValidator
{
    private const string File = EquipmentData.RelativePath;

    /// <summary>The most slots the inventory may have with no bag (a sanity limit).</summary>
    private const int MaxBagSlots = 100;

    /// <summary>Every problem of the groups and of the items' gear fields, in a fixed order; empty when they hold together.</summary>
    /// <param name="data">The slot groups.</param>
    /// <param name="items">The item catalogue.</param>
    public static IReadOnlyList<string> Validate(EquipmentData data, IItemCatalogue items)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(items);
        var problems = new List<string>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (data.BagSlots is < Inventory.HotbarSize or > MaxBagSlots)
        {
            problems.Add($"{File}: bagSlots is {data.BagSlots}; the inventory with no bag is from the hotbar's {Inventory.HotbarSize} to {MaxBagSlots} slots.");
        }

        for (var g = 0; g < data.Groups.Count; g++)
        {
            CheckGroup(problems, data.Groups[g], g, ids);
        }

        foreach (var item in items.All)
        {
            CheckItem(problems, data, item);
        }

        return problems;
    }

    private static void CheckGroup(List<string> problems, EquipmentGroup group, int index, HashSet<string> ids)
    {
        var name = $"the group '{group.Id}'";
        if (!KeyPattern().IsMatch(group.Id))
        {
            problems.Add($"{File}: group {index + 1} has the id '{group.Id}', which is not a lowercase key (letters, digits and hyphens).");
        }
        else if (!ids.Add(group.Id))
        {
            problems.Add($"{File}: the group id '{group.Id}' is used twice.");
        }

        if (group.Layer is < 1 or > 9)
        {
            problems.Add($"{File}: {name} has the layer {group.Layer}; a layer is from 1 to 9 (or left out).");
        }

        if (group.Slots.Count == 0)
        {
            problems.Add($"{File}: {name} has no slots.");
        }

        var slotNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var slot in group.Slots)
        {
            if (!KeyPattern().IsMatch(slot.Wear) || !slotNames.Add(slot.Name))
            {
                problems.Add($"{File}: {name} has the slot '{slot.Name}' ({(KeyPattern().IsMatch(slot.Wear) ? "named twice" : $"wear '{slot.Wear}' is not a lowercase key")}).");
            }

            if (slot.Label is not null && !LabelPattern().IsMatch(slot.Label))
            {
                problems.Add($"{File}: the slot '{slot.Name}' of {name} has the label '{slot.Label}', which is not a lowercase noun.");
            }
        }

        if (group.Accepts.Count == 0)
        {
            problems.Add($"{File}: {name} accepts no tag, so nothing can be worn in it.");
        }

        foreach (var tag in group.Accepts.Where(static t => !KeyPattern().IsMatch(t)))
        {
            problems.Add($"{File}: {name} accepts the tag '{tag}', which is not a lowercase key (letters, digits and hyphens).");
        }
    }

    private static void CheckItem(List<string> problems, EquipmentData data, ItemDefinition item)
    {
        if (item.Wear is null)
        {
            if (item.Stats is { Count: > 0 } || item.Look is not null)
            {
                problems.Add($"game/items.json: '{item.Name}' has stats or a look but no wear, so it can never be worn.");
            }

            return;
        }

        var slotGroups = data.Groups.Where(g => g.Slots.Any(s => string.Equals(s.Wear, item.Wear, StringComparison.Ordinal))).ToList();
        if (slotGroups.Count == 0)
        {
            problems.Add($"game/items.json: '{item.Name}' is worn on '{item.Wear}', and no slot of {File} is for that.");
        }
        else if (!slotGroups.Exists(g => item.Tags.Any(g.AcceptsTag)))
        {
            var accepted = string.Join(", ", slotGroups.SelectMany(static g => g.Accepts).Distinct(StringComparer.Ordinal));
            problems.Add($"game/items.json: '{item.Name}' is worn on '{item.Wear}' but carries none of the tags the groups with such a slot accept ({accepted}).");
        }

        if (item.MaxStack != 1)
        {
            problems.Add($"game/items.json: '{item.Name}' can be worn, so its stack limit must be 1 (it is {item.MaxStack}).");
        }
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex KeyPattern();

    [GeneratedRegex(@"^[a-zñáéíóúü]+( [a-zñáéíóúü]+)*$")]
    private static partial Regex LabelPattern();
}
