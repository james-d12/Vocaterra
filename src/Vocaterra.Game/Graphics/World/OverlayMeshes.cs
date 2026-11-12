using Vocaterra.Game.Core.Land;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Graphics.Blocks;

namespace Vocaterra.Game.Graphics.World;

/// <summary>
/// The small meshes drawn over the world each frame, in plain values: the frame around the block the crosshair is on, the beacon column and the outline of a plot of land (Y10).
/// They use the atlas's white cell, so their colour comes from the vertex.
/// </summary>
public static class OverlayMeshes
{
    /// <summary>The thickness of the highlight's bars in blocks.</summary>
    public const float HighlightThickness = 0.03f;

    /// <summary>How far the highlight stands off the block's faces, so it is not cut by them.</summary>
    public const float HighlightMargin = 0.004f;

    /// <summary>The height of the beacon column above the block, in blocks.</summary>
    public const float BeaconHeight = 40f;

    /// <summary>The number of bars in the highlight: the twelve edges of a cube.</summary>
    public const int HighlightBars = 12;

    /// <summary>The highlight: a thin dark frame round the block, twelve bars of six faces each.</summary>
    public static MeshBuffer Highlight(BlockPos block)
    {
        var buffer = new MeshBuffer();
        float x0 = block.X - HighlightMargin, y0 = block.Y - HighlightMargin, z0 = block.Z - HighlightMargin;
        float x1 = block.X + 1 + HighlightMargin, y1 = block.Y + 1 + HighlightMargin, z1 = block.Z + 1 + HighlightMargin;
        const float T = HighlightThickness;
        foreach (var y in new[] { y0, y1 - T })
        {
            foreach (var z in new[] { z0, z1 - T })
            {
                AddBox(buffer, new Box(x0, y, z, x1, y + T, z + T), (18, 18, 24, 255)); // along x
            }
        }

        foreach (var x in new[] { x0, x1 - T })
        {
            foreach (var z in new[] { z0, z1 - T })
            {
                AddBox(buffer, new Box(x, y0, z, x + T, y1, z + T), (18, 18, 24, 255)); // along y
            }

            foreach (var y in new[] { y0, y1 - T })
            {
                AddBox(buffer, new Box(x, y, z0, x + T, y + T, z1), (18, 18, 24, 255)); // along z
            }
        }

        return buffer;
    }

    /// <summary>
    /// The beacon column on a block: a translucent gold beam from the top of the block up <see cref="BeaconHeight"/> blocks, with a thinner
    /// brighter core. The alpha swells and falls with the tick (period about two seconds), so it reads as a signal, not as scenery.
    /// </summary>
    public static MeshBuffer Beacon(BlockPos block, long tick)
    {
        var buffer = new MeshBuffer();
        var pulse = 0.5f + (0.5f * MathF.Sin(tick * MathF.Tau / 120f));
        var outer = (byte)(70 + (int)(40 * pulse));
        var core = (byte)(150 + (int)(60 * pulse));
        var baseY = block.Y + 1f;
        AddBox(buffer, new Box(block.X + 0.5f - 0.45f, baseY, block.Z + 0.5f - 0.45f, block.X + 0.5f + 0.45f, baseY + BeaconHeight, block.Z + 0.5f + 0.45f), (255, 214, 64, outer));
        AddBox(buffer, new Box(block.X + 0.5f - 0.12f, baseY, block.Z + 0.5f - 0.12f, block.X + 0.5f + 0.12f, baseY + BeaconHeight, block.Z + 0.5f + 0.12f), (255, 246, 190, core));
        return buffer;
    }

    /// <summary>The height of a plot's corner posts, in blocks.</summary>
    public const float PostHeight = 2.5f;

    /// <summary>The side of a corner post, in blocks.</summary>
    public const float PostSide = 0.3f;

    /// <summary>The width of the edge line, in blocks.</summary>
    public const float EdgeWidth = 0.22f;

    /// <summary>How far the edge line lies above the ground's top, so the ground does not cut it.</summary>
    public const float EdgeLift = 0.03f;

    /// <summary>How near the eye must be to a plot (to its nearest column, in blocks) for its outline to be drawn.</summary>
    public const float PlotOutlineReach = 64f;

    /// <summary>Whether a plot's outline is drawn for an eye at the column: within <see cref="PlotOutlineReach"/> blocks of the plot's nearest column.</summary>
    /// <param name="plot">The plot.</param>
    /// <param name="eyeX">The eye's x in blocks.</param>
    /// <param name="eyeZ">The eye's z in blocks.</param>
    public static bool PlotOutlineVisible(Plot plot, float eyeX, float eyeZ)
    {
        var dx = Math.Max(Math.Max(plot.X - eyeX, eyeX - (plot.X + plot.Width)), 0f);
        var dz = Math.Max(Math.Max(plot.Z - eyeZ, eyeZ - (plot.Z + plot.Depth)), 0f);
        return (dx * dx) + (dz * dz) <= PlotOutlineReach * PlotOutlineReach;
    }

    /// <summary>
    /// The outline of a plot of land (Y10): a post in each corner column and an edge line on the ground round the plot, each piece standing at the ground's height of its own
    /// column (<paramref name="surface"/>), so the outline follows the terrain. The pending plot (<paramref name="owned"/> false) is amber with a broken edge (every second column of a
    /// side) and bare posts; the owned plot is green with a solid edge and a flag on each post, so the two differ by shape and not by colour alone. The meshes are lit by the sun's
    /// tint at draw time like the figures (the renderer's diffuse colour), so they dim at night.
    /// </summary>
    /// <param name="plot">The plot.</param>
    /// <param name="surface">The y of the top of the ground in a column: the plane the outline stands on.</param>
    /// <param name="owned">Whether the plot is the owned one.</param>
    public static MeshBuffer PlotOutline(Plot plot, Func<int, int, float> surface, bool owned)
    {
        ArgumentNullException.ThrowIfNull(surface);
        var buffer = new MeshBuffer();
        var edge = owned ? ((byte)90, (byte)210, (byte)120, (byte)255) : ((byte)255, (byte)196, (byte)56, (byte)255);
        var post = owned ? ((byte)238, (byte)238, (byte)232, (byte)255) : ((byte)200, (byte)140, (byte)60, (byte)255);
        var flag = ((byte)64, (byte)190, (byte)96, (byte)255);
        int x0 = plot.X, z0 = plot.Z, x1 = plot.X + plot.Width - 1, z1 = plot.Z + plot.Depth - 1;

        // The edge: along the north and south sides then the west and east ones (the corners belong to the first two); a broken edge keeps every second column.
        for (var x = x0; x <= x1; x++)
        {
            if (owned || (x - x0) % 2 == 0)
            {
                AddStrip(buffer, new Box(x, 0, z0, x + 1, 0, z0 + EdgeWidth), surface(x, z0), edge);
                AddStrip(buffer, new Box(x, 0, z1 + 1 - EdgeWidth, x + 1, 0, z1 + 1), surface(x, z1), edge);
            }
        }

        for (var z = z0 + 1; z < z1; z++)
        {
            if (owned || (z - z0) % 2 == 0)
            {
                AddStrip(buffer, new Box(x0, 0, z, x0 + EdgeWidth, 0, z + 1), surface(x0, z), edge);
                AddStrip(buffer, new Box(x1 + 1 - EdgeWidth, 0, z, x1 + 1, 0, z + 1), surface(x1, z), edge);
            }
        }

        // The posts in the four corner columns, at the outer corner of the corner block; the owned plot's carry a flag that flies toward the plot's inside.
        foreach (var (cx, cz) in new[] { (x0, z0), (x1, z0), (x0, z1), (x1, z1) })
        {
            var y = surface(cx, cz);
            var west = cx == x0;
            var north = cz == z0;
            var px = west ? cx : cx + 1 - PostSide;
            var pz = north ? cz : cz + 1 - PostSide;
            AddBox(buffer, new Box(px, y, pz, px + PostSide, y + PostHeight, pz + PostSide), post);
            if (owned)
            {
                var fx0 = west ? px + PostSide : px - 0.9f;
                AddBox(buffer, new Box(fx0, y + PostHeight - 0.65f, pz + (PostSide / 2f) - 0.03f, fx0 + 0.9f, y + PostHeight - 0.05f, pz + (PostSide / 2f) + 0.03f), flag);
            }
        }

        return buffer;
    }

    // A flat strip on the ground: only its top face (the line is seen from above), lifted a little off the ground's top at the given height.
    private static void AddStrip(MeshBuffer buffer, in Box footprint, float ground, (byte R, byte G, byte B, byte A) tint)
    {
        var y = ground + EdgeLift;
        ChunkMesher.AddFace(buffer, BlockFace.Up, new Box(footprint.X0, y, footprint.Z0, footprint.X1, y + 0.01f, footprint.Z1), (0f, 0f, 0f), BlockCell.White, tint, Turn.None);
    }

    private static void AddBox(MeshBuffer buffer, in Box box, (byte R, byte G, byte B, byte A) tint)
    {
        foreach (var face in new[] { BlockFace.East, BlockFace.West, BlockFace.Up, BlockFace.Down, BlockFace.South, BlockFace.North })
        {
            ChunkMesher.AddFace(buffer, face, box, (0f, 0f, 0f), BlockCell.White, tint, Turn.None);
        }
    }
}
