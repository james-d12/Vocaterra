using Vocaterra.Game.Core.Atmosphere;
using Vocaterra.Game.Graphics.World;

namespace Vocaterra.Game.Graphics.Sky;

/// <summary>
/// Everything the sky, the fog and the world's light need at one moment of the cycle (E02), as plain values with no engine type: the dome's three colours, the clouds' three, the sun
/// and the moon (where, and how strongly they show), the stars, the fog's colour (the horizon's, so far land fades into the sky), the colour of the sun's light on the world, how
/// strongly the lanterns glow, and the light band to build meshes in. <see cref="At"/> is a pure function of a <see cref="SkyLookInput"/>: five keyframes blended (<see cref="SkyBlend"/>).
/// The phase and the daylight share the moment between a day side, the twilight and a night side. The day side is the golden
/// hour's look (a low, warm sun) below 10 degrees of sun elevation and the noon's (today's sky) from 42, a smoothstep between, so the morning and the evening are warm and the middle of the day is the noon; the night side is the blue hour at the night's edges
/// and the deep night when the sun is far under the world. Across a dusk the daylight falls from 1 to 0 and the look goes from the day side's to the twilight's at one half and on to the
/// night's; a dawn is the same run backwards, so a dawn mirrors a dusk (E11).
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
/// <param name="TwilightLight">How much of <see cref="SunLight"/> is the twilight keyframe's (the twilight's weight times the sunlight): B17c's <see cref="SunModel.Colour"/> swaps the dusk's deeper light for the older one by it.</param>
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
    double CloudCover = SkyWeather.FairCover,
    float TwilightLight = 0f)
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
        var sun = SkyArc.SunAt(input.Hour);
        var moon = SkyArc.MoonAt(input.Hour);
        var sunElevation = (float)(Math.Asin(Math.Clamp(sun.Y, -1f, 1f)) * 180 / Math.PI);
        var moonShows = Smooth(0f, 6f, (float)(Math.Asin(Math.Clamp(moon.Y, -1f, 1f)) * 180 / Math.PI));
        var blend = SkyBlend.At(input.Phase, input.Daylight, sunElevation);
        var night = blend.NightSide;
        var nightness = (float)Math.Clamp((0.6 - input.Daylight) / 0.6, 0, 1);
        var starAmount = (float)Math.Clamp((0.45 - input.Daylight) / 0.45, 0, 1);

        // The weather (E05): a heavy sky greys and darkens the dome (more by night, so an overcast night has no blue glow) and the clouds, hides the sun, the moon and the stars as the
        // cover closes, and dims the world's light. At fair weather every step below returns what it was given.
        var gloom = SkyWeather.Gloom(input.CloudCover, input.Sunlight);
        var showing = SkyWeather.BodyVisibility(input.CloudCover);
        var sunlight = (float)Math.Clamp(input.Sunlight, 0, 1);
        var domeDarken = 0.8f - (0.25f * night); // a day sky greys to 0.8 of its brightness, a night sky to 0.55
        var domeAmount = gloom;
        var horizon = SkyWeather.Grey(blend.Mix(static k => k.DomeHorizon), domeAmount, domeDarken);
        var domeMiddle = SkyWeather.Grey(blend.Mix(static k => k.DomeMiddle), domeAmount, domeDarken);
        var light = blend.MixLight();

        // A cloud under cover at night follows the dome's middle colour (E08), so an overcast night's clouds are dark like the sky behind them, not light slabs on it.
        var follow = SkyWeather.CloudFollow(input.CloudCover, night);
        return new SkyLook(
            horizon,
            domeMiddle,
            SkyWeather.Grey(blend.Mix(static k => k.DomeZenith), domeAmount, domeDarken),
            SkyWeather.CloudColour(blend.Mix(static k => k.CloudTop), gloom, 0.65f, domeMiddle, follow),
            SkyWeather.CloudColour(blend.Mix(static k => k.CloudSide), gloom, 0.58f, domeMiddle, follow),
            SkyWeather.CloudColour(blend.Mix(static k => k.CloudBottom), gloom, 0.48f, domeMiddle, follow),
            sun,
            Smooth(SunFadeLow, SunFadeHigh, sunElevation) * showing,
            blend.Mix(static k => k.SunColour),
            moon,
            moonShows * nightness * showing,
            starAmount * starAmount * showing,
            horizon,
            (light.R * sunlight, light.G * sunlight, light.B * sunlight),
            blend.MixLanterns(),
            LightBands.For(input.Phase, input.Daylight),
            (float)Math.Clamp(input.Fog, 0, 1),
            Math.Clamp(input.CloudCover, 0, 1),
            blend.Twilight * sunlight);
    }

    /// <summary>The look the game has always had by day: noon, the day keyframe itself (the sun at 56 degrees is high enough for the whole of the noon's look).</summary>
    public static SkyLook Noon => At(new SkyLookInput(13, 1, DayPhase.Day));

    /// <summary>
    /// The weights of the day side, the twilight and the night side (they add up to 1) in a phase at a daylight: the day side's across the Day phase, the night side's across the Night
    /// phase, and across a dawn or a dusk the run night, twilight, day by the daylight (twilight exactly at one half). <see cref="SkyBlend.At"/> splits the day side into the noon's
    /// and the golden hour's by the sun's height, and the night side into the blue hour's and the deep night's.
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
}
