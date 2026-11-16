using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.World.Far;

/// <summary>
/// One run of a level-of-detail column (B07c, the way the Distant Horizons mod stores its far land): the blocks from <see cref="Bottom"/> to <see cref="Top"/> inclusive are all <see cref="Block"/>, which is what the far view colours them by.
/// A column is a short list of these from the top down: a leaf run over a trunk run over the ground's top layer, its filler and its stone; a water run over a bed; an overhang or a roof with air under it.
/// </summary>
/// <param name="Top">The y of the run's topmost block (its top face is at <c>Top + 1</c>).</param>
/// <param name="Bottom">The y of its lowest block.</param>
/// <param name="Block">The block it is made of.</param>
/// <param name="Sky">The sky light of the air just over its top, 0 to 15: 15 in the open, less under a crown or under water.</param>
/// <param name="Light">The light the block gives itself, 0 to 15 (lava, a lantern): the far view draws it glowing at night.</param>
public readonly record struct LodSegment(int Top, int Bottom, BlockId Block, byte Sky, byte Light)
{
    /// <summary>How many blocks tall the run is.</summary>
    public int Thickness => Top - Bottom + 1;
}

/// <summary>
/// The level-of-detail columns of a rectangle of cells at one level (B07c): cell (i, j) is the column at level cell (<see cref="X0"/> + i, <see cref="Z0"/> + j), a square of <c>1 &lt;&lt; Level</c> blocks whose first block is cell x that
/// many blocks. A column holds at most <see cref="MaxSegments"/> runs, top first, kept in fixed slots so the grid is two flat arrays and a column's data is the same whatever made it (the model, merged finer columns, chunks that unloaded, a
/// file): a key per level and cell, and a list of runs.
/// </summary>
public sealed class LodGrid
{
    /// <summary>The most runs a column keeps (Distant Horizons' "vertical quality"): the ground's three layers, a trunk and a crown or a water run, and room for an overhang or two.</summary>
    public const int MaxSegments = 8;

    private readonly LodSegment[] _segments;
    private readonly byte[] _counts;

    /// <summary>Creates an empty grid of cells.</summary>
    /// <param name="level">The level (the cell's side in blocks is 2 to this power).</param>
    /// <param name="x0">The first cell's x, in cells of the level.</param>
    /// <param name="z0">The first cell's z.</param>
    /// <param name="width">The cells along x.</param>
    /// <param name="depth">The cells along z.</param>
    public LodGrid(int level, int x0, int z0, int width, int depth)
    {
        Level = level;
        X0 = x0;
        Z0 = z0;
        Width = width;
        Depth = depth;
        _segments = new LodSegment[width * depth * MaxSegments];
        _counts = new byte[width * depth];
    }

    /// <summary>The level.</summary>
    public int Level { get; }

    /// <summary>The first cell's x.</summary>
    public int X0 { get; }

    /// <summary>The first cell's z.</summary>
    public int Z0 { get; }

    /// <summary>The cells along x.</summary>
    public int Width { get; }

    /// <summary>The cells along z.</summary>
    public int Depth { get; }

    /// <summary>The cell's side in blocks.</summary>
    public int Cell => 1 << Level;

    /// <summary>The runs of the column at (i, j), top first.</summary>
    /// <param name="i">The column's place along x.</param>
    /// <param name="j">The column's place along z.</param>
    public ReadOnlySpan<LodSegment> Column(int i, int j)
    {
        var at = (j * Width) + i;
        return new ReadOnlySpan<LodSegment>(_segments, at * MaxSegments, _counts[at]);
    }

    /// <summary>Sets the runs of the column at (i, j).</summary>
    /// <param name="i">The column's place along x.</param>
    /// <param name="j">The column's place along z.</param>
    /// <param name="runs">The runs, top first, at most <see cref="MaxSegments"/>.</param>
    public void Set(int i, int j, ReadOnlySpan<LodSegment> runs)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(runs.Length, MaxSegments);
        var at = (j * Width) + i;
        runs.CopyTo(new Span<LodSegment>(_segments, at * MaxSegments, MaxSegments));
        _counts[at] = (byte)runs.Length;
    }
}

/// <summary>Where the far view gets its columns (B07c): a level's cells by key. The model is one source; the chunks that unloaded and a saved file can be others, because a column is only a key and a list of runs.</summary>
public interface ILodColumnSource
{
    /// <summary>Fills a grid with the columns of its cells.</summary>
    /// <param name="grid">The grid (its level and cells say what to fill).</param>
    void Fill(LodGrid grid);
}

/// <summary>How runs of columns are made coarser and tidied (B07c).</summary>
public static class LodMerge
{
    /// <summary>
    /// The column of a cell made from the four columns of its finer cells (a 2 x 2 square of the level below), keeping the silhouette that is seen from outside: where any of the four holds water the cell is that water over its bed (the column of the
    /// water child with the lowest bed: a river or a lake is not lost to its banks, and shows as wide as a cell); otherwise a height is solid when any of the four is solid there, as the most common solid block of them, so a peak keeps its height,
    /// a trunk its place and a crown its crown. The runs are then joined and the thinnest over the ground dropped until there are few enough. Light is the brightest.
    /// </summary>
    /// <param name="a">One child's runs.</param>
    /// <param name="b">The second.</param>
    /// <param name="c">The third.</param>
    /// <param name="d">The fourth.</param>
    /// <param name="into">The merged runs, <see cref="LodGrid.MaxSegments"/> slots at least.</param>
    /// <returns>How many runs there are.</returns>
    public static int Merge(ReadOnlySpan<LodSegment> a, ReadOnlySpan<LodSegment> b, ReadOnlySpan<LodSegment> c, ReadOnlySpan<LodSegment> d, Span<LodSegment> into)
    {
        var water = LowestWater(a, b, c, d);
        if (!water.IsEmpty)
        {
            return Cap(water, into);
        }

        Span<int> cuts = stackalloc int[(4 * LodGrid.MaxSegments * 2) + 2];
        var cutCount = 0;
        AddCuts(a, cuts, ref cutCount);
        AddCuts(b, cuts, ref cutCount);
        AddCuts(c, cuts, ref cutCount);
        AddCuts(d, cuts, ref cutCount);
        if (cutCount == 0)
        {
            return 0;
        }

        cuts[..cutCount].Sort();
        Span<LodSegment> found = stackalloc LodSegment[(4 * LodGrid.MaxSegments * 2) + 2];
        var count = 0;
        Span<BlockId> blocks = stackalloc BlockId[4];
        Span<byte> skies = stackalloc byte[4];
        Span<byte> lights = stackalloc byte[4];
        // Elementary intervals from the top down: [cut k, cut k+1).
        for (var k = cutCount - 1; k > 0; k--)
        {
            var top = cuts[k];
            var bottom = cuts[k - 1];
            if (top == bottom)
            {
                continue;
            }

            var y = bottom;
            blocks[0] = At(a, y, out skies[0], out lights[0]);
            blocks[1] = At(b, y, out skies[1], out lights[1]);
            blocks[2] = At(c, y, out skies[2], out lights[2]);
            blocks[3] = At(d, y, out skies[3], out lights[3]);
            var winner = BlockId.Air;
            var winnerCount = 0;
            for (var i = 0; i < 4; i++)
            {
                if (blocks[i] == BlockId.Air)
                {
                    continue;
                }

                var n = 0;
                for (var m = 0; m < 4; m++)
                {
                    n += blocks[m] == blocks[i] ? 1 : 0;
                }

                if (n > winnerCount)
                {
                    winner = blocks[i];
                    winnerCount = n;
                }
            }

            if (winner == BlockId.Air)
            {
                continue;
            }

            byte winnerSky = 0;
            byte winnerLight = 0;
            for (var i = 0; i < 4; i++)
            {
                if (blocks[i] == winner)
                {
                    winnerSky = Math.Max(winnerSky, skies[i]);
                    winnerLight = Math.Max(winnerLight, lights[i]);
                }
            }

            if (count > 0 && found[count - 1].Block == winner && found[count - 1].Bottom == top)
            {
                found[count - 1] = found[count - 1] with { Bottom = bottom };
            }
            else
            {
                found[count++] = new LodSegment(top - 1, bottom, winner, winnerSky, winnerLight);
            }
        }

        return Cap(found[..count], into);
    }

    // The column of the child that holds water over the lowest bed, or an empty span when none does.
    private static ReadOnlySpan<LodSegment> LowestWater(ReadOnlySpan<LodSegment> a, ReadOnlySpan<LodSegment> b, ReadOnlySpan<LodSegment> c, ReadOnlySpan<LodSegment> d)
    {
        var best = default(ReadOnlySpan<LodSegment>);
        var bed = int.MaxValue;
        Consider(a, ref best, ref bed);
        Consider(b, ref best, ref bed);
        Consider(c, ref best, ref bed);
        Consider(d, ref best, ref bed);
        return best;
    }

    private static void Consider(ReadOnlySpan<LodSegment> column, ref ReadOnlySpan<LodSegment> best, ref int bed)
    {
        foreach (var run in column)
        {
            if (run.Block == BlockId.Water)
            {
                var ground = GroundTop(column);
                if (ground < bed)
                {
                    best = column;
                    bed = ground;
                }

                return;
            }
        }
    }

    private static void AddCuts(ReadOnlySpan<LodSegment> column, Span<int> cuts, ref int count)
    {
        foreach (var s in column)
        {
            cuts[count++] = s.Top + 1;
            cuts[count++] = s.Bottom;
        }
    }

    // The run of a column that holds the block at y, with its lights; air (and no run) otherwise.
    private static BlockId At(ReadOnlySpan<LodSegment> column, int y, out byte sky, out byte light)
    {
        foreach (var s in column)
        {
            if (y <= s.Top && y >= s.Bottom)
            {
                sky = s.Sky;
                light = s.Light;
                return s.Block;
            }
        }

        sky = 15;
        light = 0;
        return BlockId.Air;
    }

    /// <summary>Copies runs into slots, dropping the thinnest run (never the lowest) until at most <see cref="LodGrid.MaxSegments"/> remain.</summary>
    /// <param name="runs">The runs, top first.</param>
    /// <param name="into">The slots.</param>
    /// <returns>How many runs were kept.</returns>
    public static int Cap(ReadOnlySpan<LodSegment> runs, Span<LodSegment> into)
    {
        if (runs.Length <= LodGrid.MaxSegments)
        {
            runs.CopyTo(into);
            return runs.Length;
        }

        var work = runs.ToArray();
        var count = work.Length;
        while (count > LodGrid.MaxSegments)
        {
            var thinnest = 0;
            var found = false;
            var chain = ChainStart(work.AsSpan(0, count));
            for (var i = 1; i < count - 1; i++)
            {
                // What stands over the ground goes before the ground's own layers (the grass on top is one block thick, and the thinnest).
                if (i >= chain && chain > 1)
                {
                    break;
                }

                if (!found || work[i].Thickness < work[thinnest].Thickness)
                {
                    thinnest = i;
                    found = true;
                }
            }

            for (var i = thinnest; i < count - 1; i++)
            {
                work[i] = work[i + 1];
            }

            count--;
        }

        work.AsSpan(0, count).CopyTo(into);
        return count;
    }

    private static int ChainStart(ReadOnlySpan<LodSegment> column)
    {
        var i = column.Length - 1;
        while (i > 0 && column[i - 1].Bottom == column[i].Top + 1 && column[i - 1].Block != BlockId.Water && !BlockDefinition.IsLeaves(column[i - 1].Block) && !BlockDefinition.IsTrunk(column[i - 1].Block))
        {
            i--;
        }

        return i;
    }

    /// <summary>The y of the top of the run of touching runs that stands on the lowest one: the ground a column's trees and water rise from.</summary>
    /// <param name="column">The column's runs, top first.</param>
    /// <returns>The y of the ground's topmost block, or <see cref="int.MinValue"/> for an empty column.</returns>
    public static int GroundTop(ReadOnlySpan<LodSegment> column)
    {
        return column.IsEmpty ? int.MinValue : column[ChainStart(column)].Top;
    }
}
