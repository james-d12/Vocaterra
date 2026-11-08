namespace Vocaterra.Game.Core.Water;

/// <summary>Why the player is hurt (the port <see cref="IPlayerHealth"/> takes it so a front-end can tell the hurts apart).</summary>
public enum DamageCause
{
    /// <summary>The player is out of air under water.</summary>
    Drowning = 0,
}
