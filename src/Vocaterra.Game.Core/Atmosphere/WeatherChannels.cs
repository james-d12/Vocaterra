namespace Vocaterra.Game.Core.Atmosphere;

/// <summary>
/// The numbers a weather kind sets (E04, D-game-31): the channels every consumer reads, never the kind's id. Cloud cover, sunlight, fog and wind run from 0 to 1;
/// the precipitation is a style id (empty for none) and an intensity from 0 to 1. <see cref="Blend"/> makes the channels of a change from one kind to another.
/// </summary>
/// <param name="CloudCover">How much of the sky is cloud, 0 to 1.</param>
/// <param name="Sunlight">How much of the sun's light reaches the ground, 0 to 1.</param>
/// <param name="Fog">How thick the fog is, 0 to 1.</param>
/// <param name="Wind">How strong the wind is, 0 to 1.</param>
/// <param name="PrecipitationKind">The style of what falls (an id a consumer's table knows), or empty for none.</param>
/// <param name="PrecipitationIntensity">How hard it falls, 0 to 1.</param>
public readonly record struct WeatherChannels(
    double CloudCover,
    double Sunlight,
    double Fog,
    double Wind,
    string PrecipitationKind,
    double PrecipitationIntensity)
{
    /// <summary>The channels of a kind with no precipitation.</summary>
    /// <param name="cloudCover">The cloud cover.</param>
    /// <param name="sunlight">The sunlight.</param>
    /// <param name="fog">The fog.</param>
    /// <param name="wind">The wind.</param>
    public static WeatherChannels Dry(double cloudCover, double sunlight, double fog, double wind) => new(cloudCover, sunlight, fog, wind, string.Empty, 0);

    /// <summary>
    /// The channels part of the way from <paramref name="a"/> to <paramref name="b"/>: each number is a straight blend (exactly <paramref name="a"/> at 0 and exactly
    /// <paramref name="b"/> at 1). The precipitation's kind is the one blended towards while the intensity is above zero (a fading rain keeps its kind until it
    /// reaches zero), the previous one when the new kind has none, and empty at zero.
    /// </summary>
    /// <param name="a">The channels being left.</param>
    /// <param name="b">The channels being reached.</param>
    /// <param name="t">How far, 0 to 1 (clamped).</param>
    public static WeatherChannels Blend(WeatherChannels a, WeatherChannels b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        var intensity = Mix(a.PrecipitationIntensity, b.PrecipitationIntensity, t);
        var kind = intensity <= 0 ? string.Empty : (string.IsNullOrEmpty(b.PrecipitationKind) ? a.PrecipitationKind : b.PrecipitationKind);
        return new WeatherChannels(
            Mix(a.CloudCover, b.CloudCover, t),
            Mix(a.Sunlight, b.Sunlight, t),
            Mix(a.Fog, b.Fog, t),
            Mix(a.Wind, b.Wind, t),
            kind,
            intensity);
    }

    private static double Mix(double a, double b, double t) => a == b || t <= 0 ? a : t >= 1 ? b : (a * (1 - t)) + (b * t);
}
