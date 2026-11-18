using Microsoft.Xna.Framework;

namespace Vocaterra.Game.Graphics.Gl;

/// <summary>
/// The frame's light as the shader takes it (B17c): where the camera is, the sun's direction and colour, the colour the block light adds, the height fog's shape and the water's movement. The renderer makes one a frame from the sky's look
/// (<c>VoxelRenderer</c>); the pass sets it once at <see cref="GlChunkPass.Begin"/>, so a change of hour, weather or glow costs a few uniforms and no new mesh.
/// </summary>
/// <param name="Eye">The camera's position in blocks.</param>
/// <param name="SunDirection">The unit vector towards the sun the faces are shaded by (<see cref="Sky.SunModel.ShadingDirection"/>).</param>
/// <param name="SunColour">The sun's light on the world, a multiplier (<see cref="Sky.SunModel.Colour"/>).</param>
/// <param name="DayDirection">The sun the far view's land was baked in (<c>ChunkMesher.SunDirection</c>): the far view's face shade is brought from it to <paramref name="SunDirection"/>.</param>
/// <param name="GlowColour">The colour the block light adds at full block light: amber times the glow's strength (<see cref="World.LanternGlow.PassColour"/>).</param>
/// <param name="HeightFogDensity">The height fog's density at its base height, per block; 0 for no height fog.</param>
/// <param name="HeightFogBase">The height at which the density is <paramref name="HeightFogDensity"/>, in blocks.</param>
/// <param name="HeightFogScale">One over the scale height, per block: the density falls to 1/e in this many blocks.</param>
/// <param name="WaterShader">Whether the water takes the depth tint, the surface's movement and the edge at grazing angles.</param>
/// <param name="Time">Game time in seconds (the surface's movement).</param>
/// <param name="WaveAmount">How far the surface moves, in blocks.</param>
public readonly record struct GlLightFrame(
    Vector3 Eye,
    Vector3 SunDirection,
    Vector3 SunColour,
    Vector3 DayDirection,
    Vector3 GlowColour,
    float HeightFogDensity = 0f,
    float HeightFogBase = 64f,
    float HeightFogScale = 1f / 64f,
    bool WaterShader = false,
    float Time = 0f,
    float WaveAmount = 0f)
{
    /// <summary>The light of the midday in clear weather with no extras: the Day's sun, white, no glow, no height fog, no water shader.</summary>
    /// <param name="eye">The camera's position.</param>
    public static GlLightFrame Noon(Vector3 eye) => new(eye, ToVector(World.ChunkMesher.SunDirection), Vector3.One, ToVector(World.ChunkMesher.SunDirection), Vector3.Zero);

    private static Vector3 ToVector((float X, float Y, float Z) v) => new(v.X, v.Y, v.Z);
}
