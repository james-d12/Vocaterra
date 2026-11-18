using Vocaterra.Game.Core.Atmosphere;

namespace Vocaterra.Game.Graphics.World;

/// <summary>
/// What each <see cref="LightBand"/> bakes into a mesh (the sun's multiplier) and when a band is the one to be in (E02). Plain values, no engine type, so the rules are tested without a device.
/// </summary>
public static class LightBands
{
    /// <summary>The daylight (0 to 1) of a dawn or a dusk at or above which the Day band's baked light is the nearer one.</summary>
    public const double DayFrom = 0.85;

    /// <summary>The daylight (0 to 1) of a dawn or a dusk at or below which the Night band's baked light is the nearer one.</summary>
    public const double NightUpTo = 0.12;

    /// <summary>How many bands there are.</summary>
    public const int Count = 4;

    /// <summary>What the sun's light is multiplied by in the vertices of a band: nothing by day, <see cref="ChunkMesher.DuskLight"/> at dusk, <see cref="ChunkMesher.NightLight"/> by night.</summary>
    /// <param name="band">The band.</param>
    public static (float R, float G, float B) BakedSun(LightBand band) => band switch
    {
        LightBand.Dusk or LightBand.Dawn => ChunkMesher.DuskLight,
        LightBand.Night => ChunkMesher.NightLight,
        _ => (1f, 1f, 1f),
    };

    /// <summary>The hour whose sun a Dusk band's faces are shaded by (B22): a little before the sun sets, when it stands low in the west.</summary>
    public const double DuskSunHour = 20.2;

    /// <summary>The hour whose sun a Dawn band's faces are shaded by (B22): a little after the sun rises, low in the east.</summary>
    public const double DawnSunHour = 5.8;

    /// <summary>
    /// The direction towards the sun that a band's faces are shaded by (B22; a unit vector, x east, y up, z south): the afternoon's of <see cref="ChunkMesher.SunDirection"/> by day, the low sun of the evening in the west at dusk
    /// and of the morning in the east at dawn (both from <see cref="Sky.SkyArc"/>, so the faces are lit from where the sky's sun stands), the moon's at midnight by night.
    /// </summary>
    /// <param name="band">The band.</param>
    public static (float X, float Y, float Z) SunDirectionOf(LightBand band) => band switch
    {
        LightBand.Dusk => Sky.SkyArc.SunAt(DuskSunHour),
        LightBand.Dawn => Sky.SkyArc.SunAt(DawnSunHour),
        LightBand.Night => Sky.SkyArc.MoonAt(0),
        _ => ChunkMesher.SunDirection,
    };

    /// <summary>How strongly the lanterns glow at the keyframe of a band: half by day, fully at dusk and by night. Nothing is baked with it (E02a): <see cref="Sky.SkyKeyframe"/> blends these into the look's continuous <see cref="Sky.SkyLook.LanternStrength"/>, which the glow pass is drawn with.</summary>
    /// <param name="band">The band.</param>
    public static float LanternStrength(LightBand band) => band == LightBand.Day ? 0.5f : 1f;

    /// <summary>
    /// The band to build meshes in at a moment: the day's by day, the night's by night, and across a dawn the Dawn band and across a dusk the Dusk band (B22: the sun on the faces is in the east and in the west), except where the light is close to the
    /// day's or the night's own (the first and last stretch of the change), where the nearer of those two is used, so no band is drawn far from the light it baked.
    /// </summary>
    /// <param name="phase">The phase of the day.</param>
    /// <param name="daylight">The daylight, 0 to 1.</param>
    public static LightBand For(DayPhase phase, double daylight) => phase switch
    {
        DayPhase.Day => LightBand.Day,
        DayPhase.Night => LightBand.Night,
        _ => daylight >= DayFrom ? LightBand.Day : daylight <= NightUpTo ? LightBand.Night : phase == DayPhase.Dawn ? LightBand.Dawn : LightBand.Dusk,
    };

    /// <summary>
    /// The band meshes are built in (B17c): the look's own for the effects, which bake the band's sun into the vertex colours and build a chunk again when the band changes; <see cref="LightBand.Day"/> always for a mesh whose light is data (the program takes the
    /// sun as a uniform, so the hour never changes a mesh) and for the effects when the sun does not follow the hour (one sun, so one band: the light of the moment is the tint alone).
    /// </summary>
    /// <param name="lookBand">The band of the sky's look at this moment (<see cref="For"/>).</param>
    /// <param name="lightAsData">Whether the meshes carry their light as data (the program draws).</param>
    /// <param name="sunFollowsHour">Whether the sun follows the hour (the setting).</param>
    public static LightBand MeshBand(LightBand lookBand, bool lightAsData, bool sunFollowsHour) => lightAsData || !sunFollowsHour ? LightBand.Day : lookBand;

    /// <summary>The colour a chunk of a band is drawn with so that its baked sun light shows as <paramref name="sun"/>: the light of the moment divided by the band's baked light.</summary>
    /// <param name="sun">The sun's light on the world (<see cref="Sky.SkyLook.SunLight"/>).</param>
    /// <param name="band">The band the chunk's mesh was built in.</param>
    public static (float R, float G, float B) Tint((float R, float G, float B) sun, LightBand band)
    {
        var baked = BakedSun(band);
        return (sun.R / baked.R, sun.G / baked.G, sun.B / baked.B);
    }
}
