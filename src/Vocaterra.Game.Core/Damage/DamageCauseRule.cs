namespace Vocaterra.Game.Core.Damage;

/// <summary>One row of <c>game/damage.json</c> (I05a): a cause and the modifiers that apply to it, in order.</summary>
/// <param name="Id">The cause's id (<see cref="Water.DamageCause.Id"/>).</param>
/// <param name="Modifiers">The ids of the modifiers that apply, in the order they run; empty when none does.</param>
public sealed record DamageCauseRule(string Id, IReadOnlyList<string> Modifiers);
