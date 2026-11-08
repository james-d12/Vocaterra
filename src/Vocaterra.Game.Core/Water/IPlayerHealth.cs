namespace Vocaterra.Game.Core.Water;

/// <summary>
/// The port through which the water rules hurt the player (water-plan H03). The real health (the needs of <c>feature/bread-pilot</c>) is joined to it
/// in H11; until then <see cref="NoPlayerHealth"/> stands in.
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
