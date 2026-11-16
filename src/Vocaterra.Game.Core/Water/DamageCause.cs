namespace Vocaterra.Game.Core.Water;

/// <summary>
/// Why the player is hurt (the port <see cref="IPlayerHealth"/> takes it so a front-end can tell the hurts apart): a data id (I05a), the id of a row of <c>game/damage.json</c>. A new cause is a
/// constant here, where the code raises it, and a row there; which modifiers (armour) apply to it is that row's, never code.
/// </summary>
/// <param name="Id">The cause's id in <c>game/damage.json</c>.</param>
public readonly record struct DamageCause(string Id)
{
    /// <summary>The player is out of air under water.</summary>
    public static DamageCause Drowning { get; } = new("drowning");

    /// <summary>The causes the code raises, which <c>game/damage.json</c> must list.</summary>
    public static IReadOnlyList<DamageCause> Raised { get; } = [Drowning];

    /// <inheritdoc />
    public override string ToString() => Id ?? string.Empty;
}
