namespace Vocaterra.WorldPreview;

/// <summary>Thin outlines drawn over a map (B06): the footprint of a region, the edge of its blend margin and the radius of its land bias, so the owner can see where the valley's border and its blend lie.</summary>
internal static class Overlay
{
    /// <summary>The footprint's outline.</summary>
    public static readonly Rgb Footprint = new(235, 40, 40);

    /// <summary>The margin's outline.</summary>
    public static readonly Rgb Margin = new(250, 150, 20);

    /// <summary>The land bias radius's outline.</summary>
    public static readonly Rgb Bias = new(240, 230, 60);

    /// <summary>
    /// Draws the outline of the area that lies within <paramref name="reach"/> blocks of the box (minX, minZ) to (maxX, maxZ), both inclusive: the box itself for a reach of 0, a box with round corners beyond.
    /// A dashed outline takes every other eight pixels.
    /// </summary>
    public static void Outline(Image image, WorldMap map, int minX, int minZ, int maxX, int maxZ, int reach, Rgb colour, bool dashed)
    {
        for (var py = 0; py < image.Height; py++)
        {
            for (var px = 0; px < image.Width; px++)
            {
                if (dashed && ((px + py) / 8 % 2 == 1))
                {
                    continue;
                }

                if (Inside(map.BlockX(px), map.BlockZ(py), minX, minZ, maxX, maxZ, reach) != Inside(map.BlockX(px + 1), map.BlockZ(py), minX, minZ, maxX, maxZ, reach)
                    || Inside(map.BlockX(px), map.BlockZ(py), minX, minZ, maxX, maxZ, reach) != Inside(map.BlockX(px), map.BlockZ(py + 1), minX, minZ, maxX, maxZ, reach))
                {
                    image.Set(px, py, colour);
                }
            }
        }
    }

    private static bool Inside(int x, int z, int minX, int minZ, int maxX, int maxZ, int reach)
    {
        var dx = x < minX ? minX - x : x > maxX ? x - maxX : 0;
        var dz = z < minZ ? minZ - z : z > maxZ ? z - maxZ : 0;
        return ((long)dx * dx) + ((long)dz * dz) <= (long)reach * reach;
    }
}
