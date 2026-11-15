using System.Collections.Concurrent;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Graphics.Blocks;

namespace Vocaterra.Game.Graphics.Far;

/// <summary>
/// The colours the far land is drawn in (B07): the average colour of each block's top face in the block atlas's own art, so a far meadow is the green of the near one. A block with no art, or a
/// transparent one, falls back to a grey. Made once for a block and shared by every worker thread.
/// </summary>
public static class FarPalette
{
    private static readonly (byte R, byte G, byte B) Grey = (128, 128, 128);
    private static readonly ConcurrentDictionary<BlockId, (byte R, byte G, byte B)> Cache = new();

    /// <summary>Makes the colour of every block now, on the calling thread (the game loop's), so that the worker threads only ever read it.</summary>
    public static void Warm()
    {
        foreach (var block in Enum.GetValues<BlockId>())
        {
            _ = Of(block);
        }
    }

    /// <summary>The colour of a block's top face.</summary>
    /// <param name="block">The block.</param>
    public static (byte R, byte G, byte B) Of(BlockId block) => Cache.GetOrAdd(block, static id => Average(id));

    /// <summary>The colour of the sea: the water art's average, deepened.</summary>
    public static (byte R, byte G, byte B) Water => Of(BlockId.Water);

    /// <summary>The colour of leaves, for the forest tint.</summary>
    public static (byte R, byte G, byte B) Leaves => Of(BlockId.Leaves);

    private static (byte R, byte G, byte B) Average(BlockId block)
    {
        try
        {
            var pixels = BlockArt.Pixels(BlockAtlasLayout.CellFor(block, BlockFace.Up));
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
