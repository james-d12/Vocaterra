using Vocaterra.Game.Core.Atmosphere;
using Vocaterra.Game.Graphics.World;

namespace Vocaterra.Game.Graphics.Sky;

/// <summary>
/// Everything the sky, the fog and the world's light need at one moment of the cycle (E02), as plain values with no engine type: the dome's three colours, the clouds' three, the sun
/// and the moon (where, and how strongly they show), the stars, the fog's colour (the horizon's, so far land fades into the sky), the colour of the sun's light on the world, how
/// strongly the lanterns glow, and the light band to build meshes in. <see cref="At"/> is a pure function of a <see cref="SkyLookInput"/>: three keyframes (day, twilight, night) blended
/// by the phase and the daylight. Across a dusk the daylight falls from 1 to 0 and the look goes from the day's keyframe to the twilight's at one half and on to the night's; a dawn is
/// the same run backwards, so a dawn mirrors a dusk.
/// </summary>
/// <param name="DomeHorizon">The dome at the horizon.</param>
/// <param name="DomeMiddle">The dome half way up.</param>
/// <param name="DomeZenith">The dome overhead.</param>
/// <param name="CloudTop">A cloud's lit top.</param>
/// <param name="CloudSide">A cloud's sides.</param>
/// <param name="CloudBottom">A cloud's underside.</param>
/// <param name="SunDirection">The unit vector towards the sun (x east, y up, z south).</param>
/// <param name="SunStrength">How much of the sun shows, 0 (below the horizon) to 1.</param>
/// <param name="SunColour">The sun's disc.</param>
/// <param name="MoonDirection">The unit vector towards the moon.</param>
/// <param name="MoonStrength">How much of the moon shows, 0 (below the horizon or in the daylight) to 1.</param>
/// <param name="Stars">How much of the stars show, 0 to 1: none by day, all of them in the depth of the night.</param>
/// <param name="FogColour">The colour of the fog and the clear colour: the dome's colour at the horizon.</param>
/// <param name="SunLight">The colour of the sun's light on the world, as a multiplier: (1, 1, 1) at noon.</param>
/// <param name="LanternStrength">How strongly the lanterns glow, 0.5 by day up to 1 at dusk and by night.</param>
/// <param name="Band">The light band meshes are built in at this moment (<see cref="LightBands.For"/>).</param>
/// <param name="Fog">How thick the fog is, 0 to 1 (the weather's channel): the renderer brings the fog's distances in by it (<see cref="SkyWeather.FogReach"/>).</param>
/// <param name="CloudCover">The weather's cloud cover, 0 to 1, that <see cref="CloudField"/> is generated at.</param>
public readonly record struct SkyLook(
    (byte R, byte G, byte B) DomeHorizon,
    (byte R, byte G, byte B) DomeMiddle,
    (byte R, byte G, byte B) DomeZenith,
    (byte R, byte G, byte B) CloudTop,
    (byte R, byte G, byte B) CloudSide,
    (byte R, byte G, byte B) CloudBottom,
    (float X, float Y, float Z) SunDirection,
    float SunStrength,
    (byte R, byte G, byte B) SunColour,
    (float X, float Y, float Z) MoonDirection,
    float MoonStrength,
    float Stars,
    (byte R, byte G, byte B) FogColour,
    (float R, float G, float B) SunLight,
    float LanternStrength,
    LightBand Band,
    float Fog = 0f,
    double CloudCover = SkyWeather.FairCover)
{
    /// <summary>The hour at which the look is the twilight keyframe itself (the middle of a dusk): what <c>--dusk</c> holds the clock at.</summary>
    public const double DuskHour = 21;

    // The sun shows fully from 2 degrees above the horizon and not at all from 6 degrees below it.
    private const float SunFadeLow = -6f;
    private const float SunFadeHigh = 2f;

    /// <summary>The look at a moment.</summary>
    /// <param name="input">The hour, the daylight and the phase.</param>
    public static SkyLook At(in SkyLookInput input)
    {
        var (day, twilight, night) = Weights(input.Phase, Math.Clamp(input.Daylight, 0, 1));
        var a = SkyKeyframe.Day;
        var b = SkyKeyframe.Twilight;
        var c = SkyKeyframe.Night;
        var sun = SkyArc.SunAt(input.Hour);
        var moon = SkyArc.MoonAt(input.Hour);
        var sunElevation = (float)(Math.Asin(Math.Clamp(sun.Y, -1f, 1f)) * 180 / Math.PI);
        var moonShows = Smooth(0f, 6f, (float)(Math.Asin(Math.Clamp(moon.Y, -1f, 1f)) * 180 / Math.PI));
        var nightness = (float)Math.Clamp((0.6 - input.Daylight) / 0.6, 0, 1);
        var starAmount = (float)Math.Clamp((0.45 - input.Daylight) / 0.45, 0, 1);

        // The weather (E05): a heavy sky greys and darkens the dome (more by night, so an overcast night has no blue glow) and the clouds, hides the sun, the moon and the stars as the
        // cover closes, and dims the world's light. At fair weather every step below returns what it was given.
        var gloom = SkyWeather.Gloom(input.CloudCover, input.Sunlight);
        var showing = SkyWeather.BodyVisibility(input.CloudCover);
        var sunlight = (float)Math.Clamp(input.Sunlight, 0, 1);
        var domeDarken = 0.8f - (0.25f * night); // a day sky greys to 0.8 of its brightness, a night sky to 0.55
        var domeAmount = gloom * 0.9f;
        var horizon = SkyWeather.Grey(Mix(a.DomeHorizon, b.DomeHorizon, c.DomeHorizon, day, twilight, night), domeAmount, domeDarken);
        var light = Mix(a.SunLight, b.SunLight, c.SunLight, day, twilight, night);
        return new SkyLook(
            horizon,
            SkyWeather.Grey(Mix(a.DomeMiddle, b.DomeMiddle, c.DomeMiddle, day, twilight, night), domeAmount, domeDarken),
            SkyWeather.Grey(Mix(a.DomeZenith, b.DomeZenith, c.DomeZenith, day, twilight, night), domeAmount, domeDarken),
            SkyWeather.Grey(Mix(a.CloudTop, b.CloudTop, c.CloudTop, day, twilight, night), gloom, 0.65f),
            SkyWeather.Grey(Mix(a.CloudSide, b.CloudSide, c.CloudSide, day, twilight, night), gloom, 0.58f),
            SkyWeather.Grey(Mix(a.CloudBottom, b.CloudBottom, c.CloudBottom, day, twilight, night), gloom, 0.48f),
            sun,
            Smooth(SunFadeLow, SunFadeHigh, sunElevation) * showing,
            Mix(a.SunColour, b.SunColour, c.SunColour, day, twilight, night),
            moon,
            moonShows * nightness * showing,
            starAmount * starAmount * showing,
            horizon,
            (light.R * sunlight, light.G * sunlight, light.B * sunlight),
            Mix(a.LanternStrength, b.LanternStrength, c.LanternStrength, day, twilight, night),
            LightBands.For(input.Phase, input.Daylight),
            (float)Math.Clamp(input.Fog, 0, 1),
            Math.Clamp(input.CloudCover, 0, 1));
    }

    /// <summary>The look the game has always had by day: noon, the day keyframe itself.</summary>
    public static SkyLook Noon => At(new SkyLookInput(13, 1, DayPhase.Day));

    /// <summary>
    /// The weights of the day, twilight and night keyframes (they add up to 1) in a phase at a daylight: the day's across the Day phase, the night's across the Night phase, and across
    /// a dawn or a dusk the run night, twilight, day by the daylight (twilight exactly at one half).
    /// </summary>
    /// <param name="phase">The phase of the day.</param>
    /// <param name="daylight">The daylight, 0 to 1.</param>
    public static (float Day, float Twilight, float Night) Weights(DayPhase phase, double daylight)
    {
        switch (phase)
        {
            case DayPhase.Day:
                return (1f, 0f, 0f);
            case DayPhase.Night:
                return (0f, 0f, 1f);
        }

        if (daylight >= 0.5)
        {
            var u = (float)((daylight - 0.5) * 2);
            return (u, 1f - u, 0f);
        }

        var v = (float)(daylight * 2);
        return (0f, v, 1f - v);
    }

    private static float Smooth(float from, float to, float x)
    {
        var t = Math.Clamp((x - from) / (to - from), 0f, 1f);
        return t * t * (3f - (2f * t));
    }

    private static (byte R, byte G, byte B) Mix((byte R, byte G, byte B) a, (byte R, byte G, byte B) b, (byte R, byte G, byte B) c, float wa, float wb, float wc) => (
        ToByte((a.R * wa) + (b.R * wb) + (c.R * wc)),
        ToByte((a.G * wa) + (b.G * wb) + (c.G * wc)),
        ToByte((a.B * wa) + (b.B * wb) + (c.B * wc)));

    private static (float R, float G, float B) Mix((float R, float G, float B) a, (float R, float G, float B) b, (float R, float G, float B) c, float wa, float wb, float wc) => (
        (a.R * wa) + (b.R * wb) + (c.R * wc),
        (a.G * wa) + (b.G * wb) + (c.G * wc),
        (a.B * wa) + (b.B * wb) + (c.B * wc));

    private static float Mix(float a, float b, float c, float wa, float wb, float wc) => (a * wa) + (b * wb) + (c * wc);

    private static byte ToByte(float value) => (byte)Math.Clamp(MathF.Round(value), 0f, 255f);
}
