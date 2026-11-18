using System.Collections.Concurrent;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Graphics.Blocks;
using Vocaterra.Game.Graphics.Colour;

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
    /// The colour of a block's side face. A snow block's side art is darker than its top, which made a far snow-capped mountain (all its sides to a low eye) read as grey stone: its side is its top's white with a sixth of the side art's. A grass block's side art is mostly dirt under a thin green rim; from afar the rim and the blades hide most of it, so a far wall of grass is mostly green, as Distant Horizons colours a
    /// column by one colour for all its faces: the grass side is the top's colour with a third of the side art's.
    /// </summary>
    /// <param name="block">The block.</param>
    public static (byte R, byte G, byte B) SideOf(BlockId block) => Cache.GetOrAdd((block, true), static key => key.Block == BlockId.Grass ? Mix(Average(key.Block, BlockFace.Up), Average(key.Block, BlockFace.East), 0.3f) : key.Block == BlockId.Snow ? Mix(Average(key.Block, BlockFace.Up), Average(key.Block, BlockFace.East), 0.15f) : Average(key.Block, BlockFace.East));

    private static (byte R, byte G, byte B) Mix((byte R, byte G, byte B) a, (byte R, byte G, byte B) b, float share) =>
        ((byte)(a.R + ((b.R - a.R) * share)), (byte)(a.G + ((b.G - a.G) * share)), (byte)(a.B + ((b.B - a.B) * share)));

    /// <summary>The climate step the far view's colours are looked up at (B22): the map is smooth, and a far run of faces merges while its colour is one, so the tint changes in patches of this many climate units (about one colour level apart).</summary>
    public const int ClimateStep = 128;

    /// <summary>
    /// What a block's colour is multiplied by at a column (B22, G2): the grass and the oak leaves take the colour of <see cref="LandColours"/> at the column's ground climate, the same map the chunk mesher reads, so the far land is the green of the near;
    /// a grass block's side takes part of it, as in the near view. 1 for every other block.
    /// </summary>
    /// <param name="block">The block.</param>
    /// <param name="side">Whether the face is a side.</param>
    /// <param name="ground">The ground temperature and humidity at a block (x, y, z), or <see langword="null"/> for the map's middle.</param>
    /// <param name="x">The block's x.</param>
    /// <param name="y">The block's y.</param>
    /// <param name="z">The block's z.</param>
    public static (float R, float G, float B) LandTint(BlockId block, bool side, Func<int, int, int, (int Temperature, int Humidity)>? ground, int x, int y, int z)
    {
        if (block is not (BlockId.Grass or BlockId.Leaves))
        {
            return (1f, 1f, 1f);
        }

        var (t, h) = ground is null ? (LandColours.NeutralTemperature, LandColours.NeutralHumidity) : Climate(ground, x, y, z);
        t = (int)Math.Round(t / (double)ClimateStep) * ClimateStep;
        h = (int)Math.Round(h / (double)ClimateStep) * ClimateStep;
        var map = LandColours.Default;
        if (block == BlockId.Leaves)
        {
            return map.LeafTint(t, h);
        }

        var tint = map.GrassTint(t, h);
        return side ? LandColours.Partly(tint, LandColours.SideShare) : tint;
    }

    // The climate of a 16-block cell, once per worker thread (a far tile asks for it for every face of grass or leaves, and the climate does not change within a few blocks): a small cache per thread, dropped when the world's function changes or it grows.
    [ThreadStatic]
    private static Dictionary<(int X, int Y, int Z), (int Temperature, int Humidity)>? _cells;

    [ThreadStatic]
    private static object? _cellsFor;

    private static (int Temperature, int Humidity) Climate(Func<int, int, int, (int Temperature, int Humidity)> ground, int x, int y, int z)
    {
        if (_cells is null || !ReferenceEquals(_cellsFor, ground) || _cells.Count > 8192)
        {
            _cells = [];
            _cellsFor = ground;
        }

        var key = (x >> 4, y >> 4, z >> 4);
        if (!_cells.TryGetValue(key, out var found))
        {
            found = ground((key.Item1 << 4) + 8, (key.Item2 << 4) + 8, (key.Item3 << 4) + 8);
            _cells[key] = found;
        }

        return found;
    }

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
