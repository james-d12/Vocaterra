namespace Vocaterra.Game.Core.Water;

/// <summary>
/// The null object for <see cref="IPlayerHealth"/> for tests with no needs: it records nothing and the player is never dead. The session uses <see cref="NeedsHealth"/>.
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
