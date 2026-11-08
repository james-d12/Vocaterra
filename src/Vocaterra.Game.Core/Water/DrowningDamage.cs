namespace Vocaterra.Game.Core.Water;

/// <summary>The drowning rule: each <see cref="BreathEvents.OutOfAir"/> costs the player <see cref="DrowningDamagePerSecond"/> health, unless already dead.</summary>
public sealed class DrowningDamage
{
    /// <summary>The health lost at each out-of-air report (one a second, so ten seconds from full health).</summary>
    public const int DrowningDamagePerSecond = 10;

    /// <summary>Makes the rule over a health.</summary>
    /// <param name="health">The player's health.</param>
    public DrowningDamage(IPlayerHealth health) => Health = health ?? throw new ArgumentNullException(nameof(health));

    /// <summary>The health that is hurt; replaceable (H11 puts the real one in).</summary>
    public IPlayerHealth Health { get; set; }

    /// <summary>Applies what a breath tick reported.</summary>
    /// <param name="events">The breath's report.</param>
    public void Apply(BreathEvents events)
    {
        if ((events & BreathEvents.OutOfAir) != 0 && !Health.IsDead)
        {
            Health.Damage(DrowningDamagePerSecond, DamageCause.Drowning);
        }
    }
}
