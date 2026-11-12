using Vocaterra.Game.Core.Atmosphere;

namespace Vocaterra.Game.Graphics.Sky;

/// <summary>
/// How much of each <see cref="SkyKeyframe"/> the look is made of at one moment (E11): the five weights add up to 1. The phase and the daylight say how far the day side, the twilight and the
/// night side share the moment (<see cref="SkyLook.Weights"/>); the sun's height then splits the day side into the golden hour's low sun and the noon's high one, and how far the sun
/// is under the horizon splits the night side into the blue hour of the night's edges and the deep night. Both splits are smoothsteps, so no weight steps as the hour moves.
/// </summary>
/// <param name="Noon">The noon keyframe's weight (<see cref="SkyKeyframe.Day"/>).</param>
/// <param name="Golden">The golden hour's weight (<see cref="SkyKeyframe.Golden"/>).</param>
/// <param name="Twilight">The twilight's weight (<see cref="SkyKeyframe.Twilight"/>).</param>
/// <param name="Night">The blue hour's weight (<see cref="SkyKeyframe.Night"/>).</param>
/// <param name="Deep">The deep night's weight (<see cref="SkyKeyframe.DeepNight"/>).</param>
public readonly record struct SkyBlend(float Noon, float Golden, float Twilight, float Night, float Deep)
{
    /// <summary>The sun's elevation, in degrees, at and below which the day side is wholly the golden hour's look.</summary>
    public const float GoldenBelow = 10f;

    /// <summary>The sun's elevation, in degrees, at and above which the day side is wholly the noon's look.</summary>
    public const float NoonAbove = 42f;

    /// <summary>How far under the horizon, in degrees, the sun is at and below which the night side is wholly the blue hour's look.</summary>
    public const float DeepFrom = 18f;

    /// <summary>How far under the horizon, in degrees, the sun is at and beyond which the night side is wholly the deep night's look.</summary>
    public const float DeepUntil = 56f;

    /// <summary>The weight of the day side (noon and golden together): what <see cref="SkyLook.Weights"/> calls the day.</summary>
    public float DaySide => Noon + Golden;

    /// <summary>The weight of the night side (the blue hour and the deep night together): what <see cref="SkyLook.Weights"/> calls the night.</summary>
    public float NightSide => Night + Deep;

    /// <summary>The weights at a moment.</summary>
    /// <param name="phase">The phase of the day.</param>
    /// <param name="daylight">The daylight, 0 to 1.</param>
    /// <param name="sunElevation">The sun's elevation in degrees, negative below the horizon.</param>
    public static SkyBlend At(DayPhase phase, double daylight, float sunElevation)
    {
        var (day, twilight, night) = SkyLook.Weights(phase, Math.Clamp(daylight, 0, 1));
        var noon = day * Smooth(GoldenBelow, NoonAbove, sunElevation);
        var deep = night * Smooth(DeepFrom, DeepUntil, -sunElevation);
        return new SkyBlend(noon, day - noon, twilight, night - deep, deep);
    }

    /// <summary>A colour of the keyframes mixed by the weights.</summary>
    /// <param name="pick">Which colour of a keyframe.</param>
    public (byte R, byte G, byte B) Mix(Func<SkyKeyframe, (byte R, byte G, byte B)> pick)
    {
        var (a, b, c, d, e) = (pick(SkyKeyframe.Day), pick(SkyKeyframe.Golden), pick(SkyKeyframe.Twilight), pick(SkyKeyframe.Night), pick(SkyKeyframe.DeepNight));
        return (
            ToByte((a.R * Noon) + (b.R * Golden) + (c.R * Twilight) + (d.R * Night) + (e.R * Deep)),
            ToByte((a.G * Noon) + (b.G * Golden) + (c.G * Twilight) + (d.G * Night) + (e.G * Deep)),
            ToByte((a.B * Noon) + (b.B * Golden) + (c.B * Twilight) + (d.B * Night) + (e.B * Deep)));
    }

    /// <summary>The sun's light colour of the keyframes mixed by the weights.</summary>
    public (float R, float G, float B) MixLight()
    {
        var (a, b, c, d, e) = (SkyKeyframe.Day.SunLight, SkyKeyframe.Golden.SunLight, SkyKeyframe.Twilight.SunLight, SkyKeyframe.Night.SunLight, SkyKeyframe.DeepNight.SunLight);
        return (
            (a.R * Noon) + (b.R * Golden) + (c.R * Twilight) + (d.R * Night) + (e.R * Deep),
            (a.G * Noon) + (b.G * Golden) + (c.G * Twilight) + (d.G * Night) + (e.G * Deep),
            (a.B * Noon) + (b.B * Golden) + (c.B * Twilight) + (d.B * Night) + (e.B * Deep));
    }

    /// <summary>The lanterns' strength of the keyframes mixed by the weights.</summary>
    public float MixLanterns() =>
        (SkyKeyframe.Day.LanternStrength * Noon) + (SkyKeyframe.Golden.LanternStrength * Golden) + (SkyKeyframe.Twilight.LanternStrength * Twilight) +
        (SkyKeyframe.Night.LanternStrength * Night) + (SkyKeyframe.DeepNight.LanternStrength * Deep);

    private static float Smooth(float from, float to, float x)
    {
        var t = Math.Clamp((x - from) / (to - from), 0f, 1f);
        return t * t * (3f - (2f * t));
    }

    private static byte ToByte(float value) => (byte)Math.Clamp(MathF.Round(value), 0f, 255f);
}
