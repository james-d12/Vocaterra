namespace Vocaterra.Game.Graphics.World;

/// <summary>
/// The lanterns' light on the faces round them as its own layer (E02a), in plain values. The mesher builds, for every cube face a lantern reaches, the same quad as the opaque mesh's with the
/// glow at each corner as a grey level (<see cref="VertexColour"/>: 1 next to the lantern, 0 at <see cref="ChunkMesher.GlowRadius"/>), the renderer draws those quads after the opaque pass,
/// additively, over the very same pixels, with the amber of <see cref="Amber"/> times the strength of the moment (<see cref="PassColour"/>, the look's
/// <see cref="Sky.SkyLook.LanternStrength"/>), so the glow follows the look smoothly and no light band, no hour and no weather is baked into it.
/// </summary>
public static class LanternGlow
{
    /// <summary>
    /// What the glow adds to a face at full strength and next to a lantern, as a multiplier of the face's picture: red, green and blue (a warm amber). It is added, never multiplied, so it
    /// shows as strongly on a face in the dark as on one in the sun (the face's own colour only sets what it is added to).
    /// </summary>
    public static readonly (float R, float G, float B) Amber = (0.55f, 0.27f, 0.02f);

    /// <summary>The vertex colour of a corner no lantern reaches: black, which adds nothing.</summary>
    public static readonly uint None = MeshVertex.Shade(0f);

    /// <summary>The vertex colour of a corner the lanterns light by <paramref name="reach"/>, 0 to 1: a grey level.</summary>
    /// <param name="reach">The glow at the corner (<c>Glow.At</c> in the mesher), 0 when no lantern reaches it, 1 next to one.</param>
    public static uint VertexColour(float reach) => MeshVertex.Shade(Math.Clamp(reach, 0f, 1f));

    /// <summary>The colour the glow pass is drawn with at a strength: <see cref="Amber"/> times the strength, the look's <see cref="Sky.SkyLook.LanternStrength"/> (0.5 by day, 1 at night).</summary>
    /// <param name="strength">How strongly the lanterns glow at this moment, 0 to 1.</param>
    public static (float R, float G, float B) PassColour(float strength)
    {
        var s = Math.Clamp(strength, 0f, 1f);
        return (Amber.R * s, Amber.G * s, Amber.B * s);
    }
}
