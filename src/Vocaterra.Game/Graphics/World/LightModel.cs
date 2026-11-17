using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Graphics.World;

/// <summary>The frame's light for the shader (B17c): where the sun stands for the faces' shade and what colour its light is. Plain values, so the maths is tested without a device.</summary>
/// <param name="Direction">The unit vector towards the sun (x east, y up, z south) the faces are shaded by (<see cref="SunModel.ShadingDirection"/>).</param>
/// <param name="Colour">The colour of the sun's light on the world, as a multiplier (<see cref="Sky.SkyLook.SunLight"/>).</param>
public readonly record struct SunLightState((float X, float Y, float Z) Direction, (float R, float G, float B) Colour)
{
    /// <summary>The light of the midday in clear weather: the afternoon's fixed sun and the white light (what the Day band baked).</summary>
    public static SunLightState Noon => new(ChunkMesher.SunDirection, (1f, 1f, 1f));
}

/// <summary>
/// The light model of the voxel shader in plain C# (B17c): what <c>chunk.vert.glsl</c> computes for a corner from its light word (<see cref="LightData"/>) and the frame's sun, so a test can hold the shader's numbers to the mesher's baked ones
/// (the effects' fallback bakes a colour with these very rules) without a graphics card. The multiplier of a corner is the face's shade by the sun (<see cref="ChunkMesher.SunTintAt"/>) times the corner occlusion's keep times the sky light times
/// the sun's colour; the colour added by the block light is amber times the glow's strength times the block light (<see cref="LanternGlow.Amber"/>); the vertex colour (the material: the rock's tint, the biome's green) multiplies the first and not the second.
/// </summary>
public static class LightModel
{
    /// <summary>The factor a light word multiplies its material colour by: the face's shade, the occlusion's keep, the sky light and the sun's colour.</summary>
    /// <param name="word">The corner's light word.</param>
    /// <param name="sun">The frame's sun.</param>
    public static (float R, float G, float B) Lit(uint word, in SunLightState sun)
    {
        var face = LightData.Face(word);
        if (face == LightFace.Fixed)
        {
            return (1f, 1f, 1f);
        }

        (float R, float G, float B) shade;
        if (face == LightFace.Flat)
        {
            var grey = LightData.Extra(word) / 255f;
            shade = (grey, grey, grey);
        }
        else
        {
            var (nx, ny, nz) = ChunkMesher.NormalOf((BlockFace)(byte)face);
            shade = ChunkMesher.SunTintAt(nx, ny, nz, sun.Direction);
        }

        var keep = ChunkMesher.AoKeepOf(LightData.Occlusion(word)) * (LightData.Sky(word) / 255f);
        return (shade.R * keep * sun.Colour.R, shade.G * keep * sun.Colour.G, shade.B * keep * sun.Colour.B);
    }

    /// <summary>The colour the block light adds to a corner's picture (amber times the glow's strength times the block light); nothing for a fixed corner.</summary>
    /// <param name="word">The corner's light word.</param>
    /// <param name="glowStrength">How strongly the lights glow now (<see cref="Sky.SkyLook.LanternStrength"/>, up to 1 underground).</param>
    public static (float R, float G, float B) Glow(uint word, float glowStrength)
    {
        if (LightData.Face(word) == LightFace.Fixed)
        {
            return (0f, 0f, 0f);
        }

        var reach = LightData.Block(word) / 255f * Math.Clamp(glowStrength, 0f, 1f);
        return (LanternGlow.Amber.R * reach, LanternGlow.Amber.G * reach, LanternGlow.Amber.B * reach);
    }

    /// <summary>The colour of a corner in the picture before the texel: the material times <see cref="Lit"/> plus <see cref="Glow"/>, each channel 0 to 1 (the fragment's texel multiplies it).</summary>
    /// <param name="material">The vertex colour: red in the lowest byte (<see cref="MeshVertex.Pack"/>).</param>
    /// <param name="word">The corner's light word.</param>
    /// <param name="sun">The frame's sun.</param>
    /// <param name="glowStrength">How strongly the lights glow now.</param>
    public static (float R, float G, float B) Corner(uint material, uint word, in SunLightState sun, float glowStrength)
    {
        var lit = Lit(word, sun);
        var glow = Glow(word, glowStrength);
        return (((material & 0xFF) / 255f * lit.R) + glow.R, (((material >> 8) & 0xFF) / 255f * lit.G) + glow.G, (((material >> 16) & 0xFF) / 255f * lit.B) + glow.B);
    }

    /// <summary>
    /// The factor the far view's baked colour is multiplied by to bring it from the Day's sun (<see cref="ChunkMesher.SunDirection"/>, which its tiles are baked in) to the sun of the moment, for a face whose normal is given: the face's tint by the sun over its tint
    /// by the Day's. 1 for every face when the sun is the Day's, so the far view is what it was at midday; the same shade the chunks' light word gives the same face, so the near and far land meet in one colour.
    /// </summary>
    /// <param name="nx">The normal's east part.</param>
    /// <param name="ny">The normal's up part.</param>
    /// <param name="nz">The normal's south part.</param>
    /// <param name="sun">The sun the face is shaded by now.</param>
    public static (float R, float G, float B) Relight(float nx, float ny, float nz, (float X, float Y, float Z) sun)
    {
        var now = ChunkMesher.SunTintAt(nx, ny, nz, sun);
        var baked = ChunkMesher.SunTintAt(nx, ny, nz, ChunkMesher.SunDirection);
        return (now.R / baked.R, now.G / baked.G, now.B / baked.B);
    }
}
