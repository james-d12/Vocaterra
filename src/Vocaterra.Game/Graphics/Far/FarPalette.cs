using System.Collections.Concurrent;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Graphics.Blocks;

namespace Vocaterra.Game.Graphics.Far;

/// <summary>
/// The colours the far land is drawn in (B07, B07c): the average colour of each block's art in the block atlas, read once at load, so a far meadow is the green of the near one: the top face's for a top, the side face's for a step's
/// side (a grass block's side is the dirt with its green rim). A block with no art, or a transparent one, falls back to a grey. Made once for a block and shared by every worker thread.
/// </summary>
public static class FarPalette
{
    private static readonly (byte R, byte G, byte B) Grey = (128, 128, 128);
    private static readonly ConcurrentDictionary<(BlockId Block, bool Side), (byte R, byte G, byte B)> Cache = new();

    /// <summary>Makes the colour of every block now, on the calling thread (the game loop's), so that the worker threads only ever read it.</summary>
    public static void Warm()
    {
        foreach (var block in Enum.GetValues<BlockId>())
        {
            _ = Of(block);
            _ = SideOf(block);
        }
    }

    /// <summary>The colour of a block's top face.</summary>
    /// <param name="block">The block.</param>
    public static (byte R, byte G, byte B) Of(BlockId block) => Cache.GetOrAdd((block, false), static key => Average(key.Block, BlockFace.Up));

    /// <summary>
    /// The colour of a block's side face. A grass block's side art is mostly dirt under a thin green rim; from afar the rim and the blades hide most of it, so a far wall of grass is mostly green, as Distant Horizons colours a
    /// column by one colour for all its faces: the grass side is the top's colour with a third of the side art's.
    /// </summary>
    /// <param name="block">The block.</param>
    public static (byte R, byte G, byte B) SideOf(BlockId block) => Cache.GetOrAdd((block, true), static key => key.Block == BlockId.Grass ? Mix(Average(key.Block, BlockFace.Up), Average(key.Block, BlockFace.East), 0.3f) : Average(key.Block, BlockFace.East));

    private static (byte R, byte G, byte B) Mix((byte R, byte G, byte B) a, (byte R, byte G, byte B) b, float share) =>
        ((byte)(a.R + ((b.R - a.R) * share)), (byte)(a.G + ((b.G - a.G) * share)), (byte)(a.B + ((b.B - a.B) * share)));

    /// <summary>The colour of the sea: the water art's average.</summary>
    public static (byte R, byte G, byte B) Water => Of(BlockId.Water);

    /// <summary>The colour of leaves.</summary>
    public static (byte R, byte G, byte B) Leaves => Of(BlockId.Leaves);

    private static (byte R, byte G, byte B) Average(BlockId block, BlockFace face)
    {
        try
        {
            var pixels = BlockArt.Pixels(BlockAtlasLayout.CellFor(block, face));
            long r = 0;
            long g = 0;
            long b = 0;
            long n = 0;
            for (var i = 0; i + 3 < pixels.Length; i += 4)
            {
                if (pixels[i + 3] < 128)
                {
                    continue;
                }

                r += pixels[i];
                g += pixels[i + 1];
                b += pixels[i + 2];
                n++;
            }

            return n == 0 ? Grey : ((byte)(r / n), (byte)(g / n), (byte)(b / n));
        }
        catch (Exception ex) when (ex is ArgumentOutOfRangeException or InvalidOperationException or KeyNotFoundException)
        {
            return Grey;
        }
    }
}
