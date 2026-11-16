namespace Vocaterra.Game.Graphics.World;

/// <summary>
/// The darkening of the ground by depth (B12, biomes plan section 3.5, "Light underground"): a cell below its column's surface sees less of the sky, so the light on a face
/// falls with the depth of the cell in front of it, from the whole sun at the open surface down to <see cref="CaveMinimum"/> at <see cref="FalloffBlocks"/> blocks under it. There is
/// no light propagation (the plan lists it as later work): sky exposure is a function of the column's depth alone, which keeps a mesh a function of its blocks and the pure height model.
/// The numbers are constants here, beside the renderer's other light numbers (<see cref="LightBands"/>, <see cref="LanternGlow"/>).
/// </summary>
public static class SkyDepthLight
{
    /// <summary>How many blocks under the open sky the light reaches its minimum.</summary>
    public const int FalloffBlocks = 6;

    /// <summary>The share of the sun's light a face keeps in the dark of a cave, 0 to 1 (the lava, the torches and the lanterns light it from there).</summary>
    public const float CaveMinimum = 0.18f;

    /// <summary>The share of the sun's light that reaches a cell <paramref name="depth"/> blocks under the sky: 1 at or above the surface, falling in a straight line to <see cref="CaveMinimum"/> at <see cref="FalloffBlocks"/> blocks.</summary>
    /// <param name="depth">The column's surface y less the cell's y (zero or less for a cell open to the sky).</param>
    public static float Factor(int depth)
    {
        if (depth <= 0)
        {
            return 1f;
        }

        if (depth >= FalloffBlocks)
        {
            return CaveMinimum;
        }

        return 1f - ((1f - CaveMinimum) * depth / FalloffBlocks);
    }
}
