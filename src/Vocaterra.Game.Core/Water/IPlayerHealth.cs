namespace Vocaterra.Game.Core.Water;

/// <summary>
/// The port through which the water rules hurt the player (water-plan H03). The real health (the needs of <c>feature/bread-pilot</c>) is joined to it by <see cref="NeedsHealth"/>
/// (H11); <see cref="NoPlayerHealth"/> stands in for tests that have no needs.
/// </summary>
public interface IPlayerHealth
{
    /// <summary>Whether the player has died.</summary>
    bool IsDead { get; }

    /// <summary>Takes health from the player.</summary>
    /// <param name="amount">The health lost (a positive number).</param>
    /// <param name="cause">Why.</param>
    void Damage(int amount, DamageCause cause);
}
