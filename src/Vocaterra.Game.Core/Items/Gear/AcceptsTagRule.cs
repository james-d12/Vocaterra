namespace Vocaterra.Game.Core.Items.Gear;

/// <summary>The slot's group must accept at least one of the item's tags (I03a): clothing in the clothing group, an armour piece in the armour group, either in the cosmetic one.</summary>
public sealed class AcceptsTagRule : IEquipRule
{
    /// <inheritdoc />
    public EquipResult Check(in EquipChange change, Equipment equipment, Inventory bag)
    {
        ArgumentNullException.ThrowIfNull(equipment);
        if (change.Incoming is not { } item)
        {
            return EquipResult.Done;
        }

        var group = equipment.Data.Groups[change.Slot.Group];
        return item.Tags.Any(group.AcceptsTag) ? EquipResult.Done : EquipResult.WrongKind;
    }
}
