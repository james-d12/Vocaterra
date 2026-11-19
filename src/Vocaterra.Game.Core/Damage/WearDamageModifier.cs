using Vocaterra.Game.Core.Items.Wearing;
using Vocaterra.Game.Core.Stats;
using Vocaterra.Game.Core.Water;

namespace Vocaterra.Game.Core.Damage;

/// <summary>
/// The wear a hit puts on what the player wears (I07): it gives <see cref="DamageWearSource"/> the points of the hit it sees, so the armour and the shield (items whose durability names <c>damage</c>) wear by the damage taken, and
/// returns the damage unchanged. List it before <c>armour</c> in a cause's modifiers to wear by the whole hit, after it to wear by what got through. A cause that lists no <c>wear</c> wears nothing (drowning today).
/// </summary>
public sealed class WearDamageModifier : IDamageModifier
{
    /// <summary>The id <c>game/damage.json</c> names it by.</summary>
    public const string ModifierId = "wear";

    private readonly Func<ItemWear>? _wear;

    /// <summary>The modifier.</summary>
    /// <param name="wear">The wear engine, read at each hit (the simulation is replaced by a load), or <see langword="null"/> for none: the modifier then changes nothing.</param>
    public WearDamageModifier(Func<ItemWear>? wear) => _wear = wear;

    /// <inheritdoc />
    public string Id => ModifierId;

    /// <inheritdoc />
    public int Modify(int amount, DamageCause cause, StatSheet stats)
    {
        if (amount > 0)
        {
            _ = _wear?.Invoke().Apply(DamageWearSource.SourceId, amount);
        }

        return amount;
    }
}
