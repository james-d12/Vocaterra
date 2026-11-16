namespace Vocaterra.Game.Core.Items.Gear;

/// <summary>
/// One rule of what may be worn where (I03a). <see cref="EquipRules"/> lists them in the one place that decides; <see cref="Equipment"/> asks each before it changes anything, and the first
/// refusal stops the change with its reason. The bag's rule (I03b, <see cref="BagCapacityRule"/>: a worn bag with items in its slots cannot come off) is one more class in that list, with no edit of the equip rules before it.
/// </summary>
public interface IEquipRule
{
    /// <summary>Judges a change. Reads only; the equipment and the bag are exactly as they will be if the change goes ahead.</summary>
    /// <param name="change">What changes.</param>
    /// <param name="equipment">The worn items before the change.</param>
    /// <param name="bag">The bag the item comes from or goes to.</param>
    /// <returns><see cref="EquipResult.Done"/> when the rule has no objection, else the reason.</returns>
    EquipResult Check(in EquipChange change, Equipment equipment, Inventory bag);
}
