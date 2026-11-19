namespace Vocaterra.Game.Core.Items.Gear;

/// <summary>In a group that allows one of a kind (<see cref="EquipmentGroup.NoDuplicates"/>, the accessories) the item must not be worn in another slot of the group already (I03a). Replacing the item in its own slot is not a duplicate.</summary>
public sealed class NoDuplicateRule : IEquipRule
{
    /// <inheritdoc />
    public EquipResult Check(in EquipChange change, Equipment equipment, Inventory bag)
    {
        ArgumentNullException.ThrowIfNull(equipment);
        if (change.Incoming is not { } item || !equipment.Data.Groups[change.Slot.Group].NoDuplicates)
        {
            return EquipResult.Done;
        }

        var count = equipment.Data.Groups[change.Slot.Group].Slots.Count;
        for (var i = 0; i < count; i++)
        {
            var other = new EquipSlot(change.Slot.Group, i);
            if (i != change.Slot.Index && equipment.Get(other) is { IsEmpty: false } worn && worn.Item == item.Id)
            {
                return EquipResult.Duplicate;
            }
        }

        return EquipResult.Done;
    }
}
