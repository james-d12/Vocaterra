namespace Vocaterra.Game.Core.Items.Gear;

/// <summary>
/// The rules of what may be worn where, in one place (I03a). <see cref="Standard"/> is the list the simulation uses: the item must be wearable on the slot's part, the group must take its tags, and
/// an accessory may not be worn twice. I03b appends its own rule here (a worn bag with items in its slots cannot come off), the one change that rule needs.
/// </summary>
public static class EquipRules
{
    /// <summary>The standard rules, in the order they are asked (the first refusal is the reason given).</summary>
    public static IReadOnlyList<IEquipRule> Standard() => [new WearMatchesRule(), new AcceptsTagRule(), new NoDuplicateRule()];

    /// <summary>The first refusal of <paramref name="rules"/> to <paramref name="change"/>, or <see cref="EquipResult.Done"/> when none objects.</summary>
    /// <param name="rules">The rules, in order.</param>
    /// <param name="change">What changes.</param>
    /// <param name="equipment">The worn items before the change.</param>
    /// <param name="bag">The bag.</param>
    public static EquipResult Judge(IEnumerable<IEquipRule> rules, in EquipChange change, Equipment equipment, Inventory bag)
    {
        ArgumentNullException.ThrowIfNull(rules);
        foreach (var rule in rules)
        {
            var result = rule.Check(change, equipment, bag);
            if (result != EquipResult.Done)
            {
                return result;
            }
        }

        return EquipResult.Done;
    }
}
