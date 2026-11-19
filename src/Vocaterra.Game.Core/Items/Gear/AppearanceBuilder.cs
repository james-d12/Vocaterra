namespace Vocaterra.Game.Core.Items.Gear;

/// <summary>
/// Turns the worn items into an <see cref="Appearance"/> (I09). The groups of <c>game/equipment.json</c> that carry a <c>layer</c> are asked from the highest layer down (cosmetic 3, armour 2, clothing 1 today), and for each
/// part key the first worn item whose <see cref="ItemDefinition.Look"/> covers it gives the colour: a fourth layer is a data row with a higher number, with no code edit. The back and the off-hand are the slots whose
/// <c>wear</c> kind is the roles' (<see cref="AppearanceRoles"/>), never a group id. Cheap enough to run on every change of <see cref="Equipment.Revision"/>.
/// </summary>
public static class AppearanceBuilder
{
    /// <summary>The appearance of what is worn now.</summary>
    /// <param name="equipment">The equipment.</param>
    /// <param name="roles">The words the appearance looks for, or <see langword="null"/> for the shipped ones.</param>
    public static Appearance Of(Equipment equipment, AppearanceRoles? roles = null)
    {
        ArgumentNullException.ThrowIfNull(equipment);
        roles ??= AppearanceRoles.Default;
        var data = equipment.Data;
        var parts = new SortedDictionary<string, LookColour>(StringComparer.Ordinal);
        foreach (var group in LayersTopDown(data))
        {
            var groupIndex = data.IndexOf(group.Id);
            for (var i = 0; i < group.Slots.Count; i++)
            {
                var stack = equipment.Get(new EquipSlot(groupIndex, i));
                if (stack.IsEmpty || equipment.Items.Get(stack.Item).Look is not { } look)
                {
                    continue;
                }

                foreach (var (key, colour) in look.Parts)
                {
                    parts.TryAdd(key, colour); // the layers come top first: the first to cover a part keeps it
                }
            }
        }

        ItemDefinition? back = null;
        ItemDefinition? offHand = null;
        foreach (var (slot, item) in equipment.Worn())
        {
            var wear = data.Spec(slot).Wear;
            if (back is null && string.Equals(wear, roles.BackWear, StringComparison.Ordinal))
            {
                back = item;
            }
            else if (offHand is null && string.Equals(wear, roles.OffHandWear, StringComparison.Ordinal))
            {
                offHand = item;
            }
        }

        LookColour? sleeve = parts.TryGetValue(roles.SleeveKey, out var own) ? own : parts.TryGetValue(roles.SleeveFallbackKey, out var fallback) ? fallback : null;
        var result = new Appearance(parts, sleeve, back, offHand);
        return result.IsNone ? Appearance.None : result;
    }

    // The groups that are layers, the highest layer first; groups of one layer keep the file's order.
    private static IEnumerable<EquipmentGroup> LayersTopDown(EquipmentData data) =>
        data.Groups.Where(static g => g.Layer is not null).OrderByDescending(static g => g.Layer);
}
