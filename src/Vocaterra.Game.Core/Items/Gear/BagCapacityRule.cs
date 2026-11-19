namespace Vocaterra.Game.Core.Items.Gear;

/// <summary>
/// A bag cannot come off, or be swapped for a smaller one, while a slot it would drop holds an item (I03b, inventory-plan 3.3). The rule reads the items' <see cref="ItemDefinition.Capacity"/>, never a slot or a group:
/// a change that leaves the inventory with fewer slots needs every slot from the new count up to the old one empty, and a swap for a bag at least as large is always allowed. A refusal is
/// <see cref="EquipResult.BagNotEmpty"/> and changes nothing.
/// </summary>
public sealed class BagCapacityRule : IEquipRule
{
    /// <inheritdoc />
    public EquipResult Check(in EquipChange change, Equipment equipment, Inventory bag)
    {
        ArgumentNullException.ThrowIfNull(equipment);
        ArgumentNullException.ThrowIfNull(bag);
        var outgoing = change.Outgoing?.Capacity ?? 0;
        var incoming = change.Incoming?.Capacity ?? 0;
        if (incoming >= outgoing)
        {
            return EquipResult.Done;
        }

        return bag.IsEmptyFrom(equipment.ActiveSlotsAfter(bag, change)) ? EquipResult.Done : EquipResult.BagNotEmpty;
    }
}
