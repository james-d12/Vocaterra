using Vocaterra.Game.Core.Navigation;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Graphics;

/// <summary>
/// The small meshes drawn over the world each frame, in plain values: the frame around the block the crosshair is on, and the beacon column.
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

    private static void AddBox(MeshBuffer buffer, in Box box, (byte R, byte G, byte B, byte A) tint)
    {
        foreach (var face in new[] { BlockFace.East, BlockFace.West, BlockFace.Up, BlockFace.Down, BlockFace.South, BlockFace.North })
        {
            ChunkMesher.AddFace(buffer, face, box, (0f, 0f, 0f), BlockCell.White, tint, Turn.None);
        }
    }
}
