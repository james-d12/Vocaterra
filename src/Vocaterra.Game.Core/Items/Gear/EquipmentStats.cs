using Vocaterra.Game.Core.Stats;

namespace Vocaterra.Game.Core.Items.Gear;

/// <summary>
/// The worn equipment as the first <see cref="IStatContributor"/> (I03a): every worn item's <see cref="ItemDefinition.Stats"/> are added to the sheet, but only for the groups whose
/// <see cref="EquipmentGroup.StatsApply"/> is true (so the cosmetic layer gives nothing). It names no group and no stat key. A worn-out item gives nothing (I07).
/// </summary>
public sealed class EquipmentStats : IStatContributor
{
    private readonly Equipment _equipment;

    /// <summary>The contributor of an equipment.</summary>
    /// <param name="equipment">The worn items.</param>
    public EquipmentStats(Equipment equipment)
    {
        ArgumentNullException.ThrowIfNull(equipment);
        _equipment = equipment;
    }

    /// <inheritdoc />
    public void Contribute(StatBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        foreach (var (slot, item) in _equipment.Worn())
        {
            if (!_equipment.Data.Groups[slot.Group].StatsApply || item.Stats is null || item.IsWornOut(_equipment.Get(slot).Wear))
            {
                continue;
            }

            foreach (var (key, amount) in item.Stats)
            {
                StatRules.Contribute(builder, key, amount); // the key's rule: a sum or a factor
            }
        }
    }
}
