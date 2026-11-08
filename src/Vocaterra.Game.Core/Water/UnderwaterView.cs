namespace Vocaterra.Game.Core.Water;

/// <summary>
/// How far under water the picture is, from 0 (above) to 1 (fully under), easing in over <see cref="EaseInSeconds"/> as the eye goes under and out over
/// <see cref="EaseOutSeconds"/> as it surfaces, so the fog, the sky and the tint fade rather than snap. Presentation, not simulation: it is driven by
/// the frame's time, and nothing in the simulation reads it. Pure, with no graphics types.
/// </summary>
public sealed class UnderwaterView
{
    /// <summary>The seconds the view takes to go from above to fully under.</summary>
    public const double EaseInSeconds = 0.15;

    /// <summary>The seconds the view takes to go from fully under to above.</summary>
    public const double EaseOutSeconds = 0.3;

    /// <summary>The strength of the blue-green tint over the picture at <see cref="Amount"/> 1 (0 to 1).</summary>
    public const float TintStrength = 0.25f;

    /// <summary>How far under water the view is, 0 to 1.</summary>
    public double Amount { get; private set; }

    /// <summary>Whether the view is under water at all (<see cref="Amount"/> above 0): the renderer does the extra work only then.</summary>
    public bool IsUnder => Amount > 0;

    /// <summary>The opacity of the tint now: <see cref="TintStrength"/> times <see cref="Amount"/>.</summary>
    public float TintOpacity => TintStrength * (float)Amount;

    /// <summary>The water's own fog: its colour as plain numbers, and where it starts and ends.</summary>
    public static FogParameters WaterFog => UnderwaterFog.Water;

    /// <summary>
    /// Moves <see cref="Amount"/> towards 1 when the eye is in the water and towards 0 when it is not, by the time the frame took. A long frame
    /// cannot overshoot: the amount stops at 0 or 1. A negative time counts as none.
    /// </summary>
    /// <param name="eyeInWater">Whether the eye point is inside the water (<see cref="WaterVolume.Contains"/>).</param>
    /// <param name="seconds">The frame's time in seconds.</param>
    public void Advance(bool eyeInWater, double seconds)
    {
        var step = Math.Max(0, seconds);
        Amount = eyeInWater
            ? Math.Min(1, Amount + (step / EaseInSeconds))
            : Math.Max(0, Amount - (step / EaseOutSeconds));
    }
}
