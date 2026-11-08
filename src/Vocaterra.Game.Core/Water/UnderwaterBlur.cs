namespace Vocaterra.Game.Core.Water;

/// <summary>The setting for how much the view softens under water (owner, 2026-11-07): off, or one of three strengths. <see cref="Low"/> is the default.</summary>
public enum UnderwaterBlur
{
    /// <summary>No blur: only the fog and the tint show that the view is under water.</summary>
    Off = 0,

    /// <summary>A mild softening (the default): a half-size copy blended in.</summary>
    Low = 1,

    /// <summary>A stronger one: a quarter-size copy blended in.</summary>
    Medium = 2,

    /// <summary>The strongest: a quarter-size copy mostly replacing the picture.</summary>
    High = 3,
}
