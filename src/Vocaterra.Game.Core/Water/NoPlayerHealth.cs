namespace Vocaterra.Game.Core.Water;

/// <summary>
/// The null object for <see cref="IPlayerHealth"/> on this branch: it records nothing and the player is never dead. <c>feature/bread-pilot</c>'s
/// health replaces it in H11.
/// </summary>
public sealed class NoPlayerHealth : IPlayerHealth
{
    /// <inheritdoc />
    public bool IsDead => false;

    /// <inheritdoc />
    public void Damage(int amount, DamageCause cause)
    {
    }
}
