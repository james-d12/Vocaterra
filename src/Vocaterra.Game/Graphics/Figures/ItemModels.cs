using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Graphics.Blocks;
using Vocaterra.Game.Graphics.World;

namespace Vocaterra.Game.Graphics.Figures;

/// <summary>
/// The 3D model of a small item (issue #44): its 16 x 16 icon extruded into voxels, one small cube per opaque pixel, the way a voxel game draws the item in the hand. The
/// picture is the item's own icon (<see cref="BlockArt"/>, drawn in code, so the model is CC0 by construction and always matches the hotbar). Each voxel's faces sample the
/// single texel of its pixel, so the colours are exact; the front and the back are drawn for every pixel, a side only where the neighbouring pixel is clear, so the
/// outline of the picture becomes the item's edge.
/// </summary>
public static class ItemModels
{
    /// <summary>The alpha from which a pixel of an icon counts as solid.</summary>
    public const byte SolidAlpha = 64;

    // The shade of each face: the front (towards the eye) and the top full, the sides a little darker, the back and the underside darker still.
    private static float Shade(BlockFace face) => face switch
    {
        BlockFace.South => 1.0f,
        BlockFace.Up => 0.95f,
        BlockFace.East or BlockFace.West => 0.8f,
        BlockFace.North => 0.75f,
        _ => 0.62f,
    };

    /// <summary>Whether pixel (<paramref name="x"/>, <paramref name="y"/>) of a 16 x 16 RGBA picture (<see cref="BlockArt.Pixels"/>) is solid; outside the picture is clear.</summary>
    /// <param name="pixels">The picture's RGBA bytes, row by row.</param>
    /// <param name="x">The pixel's column.</param>
    /// <param name="y">The pixel's row, 0 at the top.</param>
    public static bool IsSolid(byte[] pixels, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        const int Size = BlockAtlasLayout.CellSize;
        return x is >= 0 and < Size && y is >= 0 and < Size && pixels[(((y * Size) + x) * 4) + 3] >= SolidAlpha;
    }

    /// <summary>
    /// Appends the extruded icon of <paramref name="cell"/> to <paramref name="buffer"/>: standing in the x-y plane facing +z (the eye), one pixel thick, its solid
    /// pixels centred on x = 0 and their lowest row resting on <paramref name="baseY"/>, centred on <paramref name="z"/> in depth.
    /// </summary>
    /// <param name="buffer">The mesh to append to.</param>
    /// <param name="cell">The icon.</param>
    /// <param name="pixel">The size of one pixel in blocks.</param>
    /// <param name="baseY">The height the item rests on.</param>
    /// <param name="z">The depth of the item's middle.</param>
    /// <returns>The number of voxels (solid pixels).</returns>
    public static int AppendExtruded(MeshBuffer buffer, BlockCell cell, float pixel, float baseY, float z)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        const int Size = BlockAtlasLayout.CellSize;
        var pixels = BlockArt.Pixels(cell);
        int minX = Size, maxX = -1, maxY = -1;
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                if (IsSolid(pixels, x, y))
                {
                    minX = Math.Min(minX, x);
                    maxX = Math.Max(maxX, x);
                    maxY = Math.Max(maxY, y);
                }
            }
        }

        if (maxY < 0)
        {
            return 0;
        }

        var (cellX, cellY, _, _) = BlockAtlasLayout.PixelRectangle(cell);
        var left = -(maxX - minX + 1) * pixel / 2f;
        var half = pixel / 2f;
        var voxels = 0;
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                if (!IsSolid(pixels, x, y))
                {
                    continue;
                }

                voxels++;
                var x0 = left + ((x - minX) * pixel);
                var y0 = baseY + ((maxY - y) * pixel);
                var box = new Box(x0, y0, z - half, x0 + pixel, y0 + pixel, z + half);

                // The texel's middle: every corner of every face samples this one pixel.
                var u = (cellX + x + 0.5f) / BlockAtlasLayout.Width;
                var v = (cellY + y + 0.5f) / BlockAtlasLayout.Height;
                var uv = (u, v, u, v);
                AddFace(buffer, BlockFace.South, box, uv);
                AddFace(buffer, BlockFace.North, box, uv);
                if (!IsSolid(pixels, x, y - 1))
                {
                    AddFace(buffer, BlockFace.Up, box, uv);
                }

                if (!IsSolid(pixels, x, y + 1))
                {
                    AddFace(buffer, BlockFace.Down, box, uv);
                }

                if (!IsSolid(pixels, x - 1, y))
                {
                    AddFace(buffer, BlockFace.West, box, uv);
                }

                if (!IsSolid(pixels, x + 1, y))
                {
                    AddFace(buffer, BlockFace.East, box, uv);
                }
            }
        }

        return voxels;
    }

    private static void AddFace(MeshBuffer buffer, BlockFace face, in Box box, (float, float, float, float) uv)
    {
        var level = (byte)(255 * Shade(face));
        FigureMesher.AddFace(buffer, face, box, MeshVertex.Pack(level, level, level), uv);
    }
}
