namespace Vocaterra.Game.Core.Items.Gear;

/// <summary>The item must be wearable, and on the part the slot is for: its <see cref="ItemDefinition.Wear"/> equals the slot's (I03a).</summary>
public sealed class WearMatchesRule : IEquipRule
{
    /// <inheritdoc />
    public EquipResult Check(in EquipChange change, Equipment equipment, Inventory bag)
    {
        ArgumentNullException.ThrowIfNull(equipment);
        if (change.Incoming is not { } item)
        {
            return EquipResult.Done;
        }

        if (item.Wear is null)
        {
            return EquipResult.NotWearable;
        }

        return string.Equals(item.Wear, equipment.Data.Spec(change.Slot).Wear, StringComparison.Ordinal) ? EquipResult.Done : EquipResult.WrongSlot;
    }
}
