using Vocaterra.Game.Core.Stats;
using Vocaterra.Game.Core.Water;

namespace Vocaterra.Game.Core.Damage;

/// <summary>
/// Something that changes the damage the player takes before health sees it (I05a): armour first, later a shield's block or a buff. The modifiers a cause names in <c>game/damage.json</c> run in
/// that order over <see cref="ModifiedHealth"/>; a modifier is registered by id in a <see cref="DamageModifiers"/> set, so letting it apply to another cause is a data row, never code.
/// </summary>
public interface IDamageModifier
{
    /// <summary>The id <c>game/damage.json</c> names it by.</summary>
    string Id { get; }

    /// <summary>The damage after this modifier. It reads only the amount, the cause and the sheet (no clock, no random numbers).</summary>
    /// <param name="amount">The damage so far (a positive number).</param>
    /// <param name="cause">Why the player is hurt.</param>
    /// <param name="stats">The player's stats now.</param>
    int Modify(int amount, DamageCause cause, StatSheet stats);
}
