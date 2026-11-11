using Vocaterra.Game.Graphics.World;

namespace Vocaterra.Game.Graphics.Sky;

/// <summary>
/// One look of the sky held for a moment of the cycle (E02, E11): the noon's (<see cref="Day"/>), the golden hour's, the twilight's (dusk, and dawn mirrors it), the night's and the
/// deep night's. <see cref="SkyLook"/> blends them by the phase, the daylight and the sun's height. Plain values. The noon and twilight sets are <see cref="SkyPalette"/>'s own, so noon
/// is the sky the game has always had and a held dusk hour is the <c>--dusk</c> sky.
/// </summary>
/// <param name="DomeHorizon">The dome at the horizon, which is also the colour of the fog.</param>
/// <param name="DomeMiddle">The dome half way up.</param>
/// <param name="DomeZenith">The dome overhead.</param>
/// <param name="CloudTop">A cloud's lit top.</param>
/// <param name="CloudSide">A cloud's sides.</param>
/// <param name="CloudBottom">A cloud's underside.</param>
/// <param name="SunColour">The sun's disc.</param>
/// <param name="SunLight">The colour of the sun's light on the world, as a multiplier (1 is the full white light of the day).</param>
/// <param name="LanternStrength">How strongly the lanterns glow, 0 to 1.</param>
public sealed record SkyKeyframe(
    (byte R, byte G, byte B) DomeHorizon,
    (byte R, byte G, byte B) DomeMiddle,
    (byte R, byte G, byte B) DomeZenith,
    (byte R, byte G, byte B) CloudTop,
    (byte R, byte G, byte B) CloudSide,
    (byte R, byte G, byte B) CloudBottom,
    (byte R, byte G, byte B) SunColour,
    (float R, float G, float B) SunLight,
    float LanternStrength)
{
    /// <summary>Noon: <see cref="SkyPalette"/>'s day set, the white sun, the lanterns at half.</summary>
    public static SkyKeyframe Day { get; } = new(
        SkyPalette.Horizon,
        SkyPalette.Middle,
        SkyPalette.Zenith,
        SkyPalette.CloudTop,
        SkyPalette.CloudSide,
        SkyPalette.CloudBottom,
        (255, 252, 232),
        (1f, 1f, 1f),
        LightBands.LanternStrength(LightBand.Day));

    /// <summary>
    /// A low sun (E11), the look of the early morning and the late afternoon: a paler, warmer horizon, a softer and slightly dimmer dome (so the sky brightens from the morning to noon and dims again to the evening), a creamy sun and a warm light a little under the noon's (no channel
    /// above 1, so the Day band's tint stays valid), the lanterns a little above the noon's. <see cref="SkyLook"/> blends it with <see cref="Day"/> by the sun's elevation.
    /// </summary>
    public static SkyKeyframe Golden { get; } = new(
        (246, 208, 160),
        (126, 164, 214),
        (60, 108, 190),
        (255, 236, 208),
        (246, 214, 196),
        (214, 190, 196),
        (255, 236, 190),
        (1.0f, 0.9f, 0.76f),
        0.6f);

    /// <summary>Dusk (and dawn): <see cref="SkyPalette"/>'s dusk set, the low orange sun, the lanterns at full strength.</summary>
    public static SkyKeyframe Twilight { get; } = new(
        SkyPalette.DuskHorizon,
        SkyPalette.DuskMiddle,
        SkyPalette.DuskZenith,
        SkyPalette.DuskCloudTop,
        SkyPalette.DuskCloudSide,
        SkyPalette.DuskCloudBottom,
        (255, 214, 150),
        ChunkMesher.DuskLight,
        LightBands.LanternStrength(LightBand.Dusk));

    /// <summary>Night, the blue hour at the night's edges: a dark, readable blue, a faint cold light on the world, the lanterns at full strength. <see cref="DeepNight"/> takes over towards the small hours.</summary>
    public static SkyKeyframe Night { get; } = new(
        (30, 44, 92),
        (16, 26, 66),
        (8, 14, 40),
        (78, 90, 136),
        (54, 64, 106),
        (38, 46, 82),
        (255, 214, 150),
        ChunkMesher.NightLight,
        LightBands.LanternStrength(LightBand.Night));

    /// <summary>
    /// The depth of the night (E11), round one in the morning: darker and less blue than <see cref="Night"/>, the light on the world still readable and every channel under
    /// <see cref="Night"/>'s (so the Night band's tint stays valid), the lanterns at full strength. <see cref="SkyLook"/> blends <see cref="Night"/> into it by how far the sun is under the horizon.
    /// </summary>
    public static SkyKeyframe DeepNight { get; } = new(
        (19, 27, 54),
        (11, 17, 42),
        (5, 9, 26),
        (48, 56, 92),
        (34, 40, 72),
        (24, 29, 54),
        (255, 214, 150),
        (0.21f, 0.26f, 0.40f),
        LightBands.LanternStrength(LightBand.Night));
}
