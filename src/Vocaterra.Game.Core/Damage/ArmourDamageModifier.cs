using Vocaterra.Game.Core.Stats;
using Vocaterra.Game.Core.Water;

namespace Vocaterra.Game.Core.Damage;

/// <summary>
/// The armour modifier (I05a): each point of the sheet's <see cref="StatKeys.Armour"/> takes <see cref="ReductionPerPoint"/> off the damage, never more than <see cref="MaxReduction"/> in all
/// (so armour never makes the player immune), rounded to a whole point and never below 1 for a hit that was above 0. 10 armour is 40%, 20 or more the cap of 80%.
/// </summary>
public sealed class ArmourDamageModifier : IDamageModifier
{
    /// <summary>The id <c>game/damage.json</c> names it by.</summary>
    public const string ModifierId = "armour";

    /// <summary>The share of the damage one armour point takes off (4%).</summary>
    public const double ReductionPerPoint = 0.04;

    /// <summary>The most armour ever takes off (80%).</summary>
    public const double MaxReduction = 0.8;

    /// <inheritdoc />
    public string Id => ModifierId;

    /// <inheritdoc />
    public int Modify(int amount, DamageCause cause, StatSheet stats)
    {
        ArgumentNullException.ThrowIfNull(stats);
        var armour = Math.Max(0, stats.Sum(StatKeys.Armour));
        if (amount <= 0 || armour <= 0)
        {
            return amount;
        }

        var reduction = Math.Min(MaxReduction, armour * ReductionPerPoint);
        return Math.Max(1, (int)Math.Round(amount * (1 - reduction), MidpointRounding.AwayFromZero));
    }
}
