using Vocaterra.Game.Graphics.World;

namespace Vocaterra.Game.Graphics.Sky;

/// <summary>
/// One look of the sky held for a moment of the cycle (E02): the day's, the twilight's (dusk, and dawn mirrors it) and the night's. <see cref="SkyLook"/> blends the three by the
/// phase and the daylight. Plain values. The day and twilight sets are <see cref="SkyPalette"/>'s own, so noon is the sky the game has always had and a held dusk hour is the
/// <c>--dusk</c> sky.
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
    /// <summary>Full day: <see cref="SkyPalette"/>'s day set, the white sun, the lanterns at half.</summary>
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

    /// <summary>Night: a dark, readable blue, a faint cold light on the world, the lanterns at full strength.</summary>
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
}
