using Vocaterra.Game.Core.Needs;

namespace Vocaterra.Game.Core.Water;

/// <summary>
/// The <see cref="IPlayerHealth"/> over the player's <see cref="PlayerNeeds"/> (water-plan H11): damage comes off the health meter, and death is the needs' own (the session
/// fades to the death screen when health reaches zero). While the needs are not running (<c>--needs off</c>, a menu or a conversation) it takes nothing, as hunger and thirst take
/// nothing then.
/// </summary>
public sealed class NeedsHealth : IPlayerHealth
{
    private readonly PlayerNeeds _needs;
    private readonly Func<bool> _active;

    /// <summary>Makes the adapter.</summary>
    /// <param name="needs">The needs whose health is hurt.</param>
    /// <param name="active">Whether the needs are running now; when false, damage is ignored. Always on when omitted.</param>
    public NeedsHealth(PlayerNeeds needs, Func<bool>? active = null)
    {
        _needs = needs ?? throw new ArgumentNullException(nameof(needs));
        _active = active ?? (static () => true);
    }

    /// <inheritdoc />
    public bool IsDead => _needs.IsDead;

    /// <inheritdoc />
    public void Damage(int amount, DamageCause cause)
    {
        if (amount > 0 && _active())
        {
            _needs.Hurt(amount);
        }
    }
}
