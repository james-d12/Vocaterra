namespace Vocaterra.Game.Graphics.Water;

/// <summary>
/// The colour a corner of the water's surface is tinted by its depth (B22, G6): the shallows over the sand and the shore are clear and light, the deep water darker and bluer, so a lake reads as a bowl and not a flat sheet. The depth at a corner
/// is the mean of the four cells that meet there, a cell of land counting 0, so the corners on the shore are the lightest and the tint runs smoothly across a surface block into the next. Plain numbers, tested without a device.
/// </summary>
public static class WaterDepthTint
{
    /// <summary>The mean depth, in blocks, below which a corner is as light as water gets (the shore: land counts 0 among the four cells).</summary>
    public const float ShoreDepth = 0.5f;

    /// <summary>The mean depth, in blocks, from which the water is at its deepest colour.</summary>
    public const float DeepDepth = 6f;

    /// <summary>How many blocks down a column of water is counted (the tint stops changing at <see cref="DeepDepth"/>, so more is never needed).</summary>
    public const int CountDepth = 8;

    /// <summary>The multiplier of the deep water's colour (the water picture is multiplied by it).</summary>
    public static readonly (float R, float G, float B) Deep = (0.56f, 0.70f, 0.90f);

    /// <summary>How deep the water is at a corner: the mean of the depths of the four cells that meet there (0 for a cell that is not water).</summary>
    /// <param name="a">The depth of the first cell, in blocks of water from its top down (0 for land).</param>
    /// <param name="b">The second cell's depth.</param>
    /// <param name="c">The third cell's depth.</param>
    /// <param name="d">The fourth cell's depth.</param>
    public static float CornerDepth(int a, int b, int c, int d) => (Math.Min(a, CountDepth) + Math.Min(b, CountDepth) + Math.Min(c, CountDepth) + Math.Min(d, CountDepth)) / 4f;

    /// <summary>How far from the shore's light colour to the deep one a corner is, 0 to 1 (smooth in the depth).</summary>
    /// <param name="depth">The corner's depth (<see cref="CornerDepth"/>).</param>
    public static float Share(float depth)
    {
        var t = Math.Clamp((depth - ShoreDepth) / (DeepDepth - ShoreDepth), 0f, 1f);
        return t * t * (3f - (2f * t));
    }

    /// <summary>The multiplier of the water picture at a corner of the given depth: white at the shore, <see cref="Deep"/> in deep water.</summary>
    /// <param name="depth">The corner's depth (<see cref="CornerDepth"/>).</param>
    public static (float R, float G, float B) At(float depth)
    {
        var t = Share(depth);
        return (1f + ((Deep.R - 1f) * t), 1f + ((Deep.G - 1f) * t), 1f + ((Deep.B - 1f) * t));
    }
}
