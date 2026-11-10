using Vocaterra.Game.Core.Atmosphere;

namespace Vocaterra.Game.Graphics.World;

/// <summary>
/// What each <see cref="LightBand"/> bakes into a mesh and when a band is the one to be in (E02). Plain values, no engine type, so the rules are tested without a device.
/// </summary>
public static class LightBands
{
    /// <summary>The daylight (0 to 1) of a dawn or a dusk at or above which the Day band's baked light is the nearer one.</summary>
    public const double DayFrom = 0.85;

    /// <summary>The daylight (0 to 1) of a dawn or a dusk at or below which the Night band's baked light is the nearer one.</summary>
    public const double NightUpTo = 0.12;

    /// <summary>How many bands there are.</summary>
    public const int Count = 3;

    /// <summary>What the sun's light is multiplied by in the vertices of a band: nothing by day, <see cref="ChunkMesher.DuskLight"/> at dusk, <see cref="ChunkMesher.NightLight"/> by night.</summary>
    /// <param name="band">The band.</param>
    public static (float R, float G, float B) BakedSun(LightBand band) => band switch
    {
        LightBand.Dusk => ChunkMesher.DuskLight,
        LightBand.Night => ChunkMesher.NightLight,
        _ => (1f, 1f, 1f),
    };

    /// <summary>How strongly the lanterns glow in the vertices of a band: half by day, fully at dusk and by night.</summary>
    /// <param name="band">The band.</param>
    public static float LanternStrength(LightBand band) => band == LightBand.Day ? 0.5f : 1f;

    /// <summary>
    /// The band to build meshes in at a moment: the day's by day, the night's by night, and across a dawn or a dusk the Dusk band, except where the light is close to the
    /// day's or the night's own (the first and last stretch of the change), where the nearer of those two is used, so no band is drawn far from the light it baked.
    /// </summary>
    /// <param name="phase">The phase of the day.</param>
    /// <param name="daylight">The daylight, 0 to 1.</param>
    public static LightBand For(DayPhase phase, double daylight) => phase switch
    {
        DayPhase.Day => LightBand.Day,
        DayPhase.Night => LightBand.Night,
        _ => daylight >= DayFrom ? LightBand.Day : daylight <= NightUpTo ? LightBand.Night : LightBand.Dusk,
    };

    /// <summary>The colour a chunk of a band is drawn with so that its baked sun light shows as <paramref name="sun"/>: the light of the moment divided by the band's baked light.</summary>
    /// <param name="sun">The sun's light on the world (<see cref="Sky.SkyLook.SunLight"/>).</param>
    /// <param name="band">The band the chunk's mesh was built in.</param>
    public static (float R, float G, float B) Tint((float R, float G, float B) sun, LightBand band)
    {
        var baked = BakedSun(band);
        return (sun.R / baked.R, sun.G / baked.G, sun.B / baked.B);
    }
}
