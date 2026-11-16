namespace Vocaterra.Game.Core.Items.Gear;

/// <summary>
/// The rules of what may be worn where, in one place (I03a). <see cref="Standard"/> is the list the simulation uses: the item must be wearable on the slot's part, the group must take its tags, and
/// an accessory may not be worn twice, and a bag may not come off, or be swapped for a smaller one, while a slot it would drop holds an item (<see cref="BagCapacityRule"/>, I03b).
/// </summary>
public static class EquipRules
{
    /// <summary>The standard rules, in the order they are asked (the first refusal is the reason given).</summary>
    public static IReadOnlyList<IEquipRule> Standard() => [new WearMatchesRule(), new AcceptsTagRule(), new NoDuplicateRule(), new BagCapacityRule()];

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
