using Vocaterra.Game.Core.Light;

namespace Vocaterra.Game.Graphics.World;

/// <summary>
/// The renderer's side of the light round the player (I05b), in plain values: how much daylight there is at the player (the day's sun light and the sky's share at the eye) and the colour the shader adds. The light's reach, strength and
/// fade are Core's (<see cref="PlayerLight.Of"/> from <c>game/light.json</c>); the shader (<c>chunk.frag.glsl</c>) adds the colour to every face within the reach by distance and by how squarely the face turns to the player.
/// </summary>
public static class PlayerLightGlow
{
    /// <summary>
    /// The share of daylight at the player, 0 to 1: the luminance of the day's sun light (<see cref="VoxelRenderer.WaterLight"/>: 1 at noon, about 0.7 at dusk, 0.36 on a night) times the sky's share at the eye
    /// (<see cref="VoxelRenderer.EyeSky"/>: 1 in the open, 0.28 deep under the ground). Night is the first signal and depth the second, and either darkens it: noon in a cave is as dark as a night in the open.
    /// </summary>
    /// <param name="sunLuminance">The luminance of the day's light, 0 to 1.</param>
    /// <param name="eyeSky">The sky's share at the eye, 0 to 1.</param>
    public static double Daylight(float sunLuminance, float eyeSky) => Math.Clamp(sunLuminance, 0f, 1f) * Math.Clamp(eyeSky, 0f, 1f);

    /// <summary>The colour the shader adds next to the player: the light's colour times its strength; black for no light.</summary>
    /// <param name="light">The light.</param>
    public static (float R, float G, float B) Colour(in PlayerLight light) =>
        light.IsOn ? (light.Colour.R * light.Strength, light.Colour.G * light.Strength, light.Colour.B * light.Strength) : (0f, 0f, 0f);
}
