using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Lod;

namespace Vocaterra.Game.Core.World.Far;

/// <summary>
/// The far view's sections made from the generator's own model (B07c): the stand-in for a place that has no real chunk data yet. The columns come from <see cref="LodModelSource"/> (the height model's ground, water and snow, the biome's layers under
/// the surface, the tree pass's own trees, the regions' own blocks) and each is cut into the voxels of the level: a voxel is solid when at least half of its height is solid (or it holds a trunk, or a quarter of it leaves), its block the one
/// with the most of it, water when at least half is water, otherwise air; the stack is then lit from the sky down. Written in the store's own format, so real data replaces it key for key. Pure.
/// </summary>
public sealed class LodModelSections
{
    private readonly LodModelSource _columns;
    private readonly int _minY;
    private readonly int _maxY;

    /// <summary>Creates the maker of a world's model sections.</summary>
    /// <param name="columns">The model's columns.</param>
    /// <param name="minY">The world's lowest y.</param>
    /// <param name="maxY">One above the world's highest y.</param>
    public LodModelSections(LodModelSource columns, int minY, int maxY)
    {
        ArgumentNullException.ThrowIfNull(columns);
        _columns = columns;
        _minY = minY;
        _maxY = maxY;
    }

    /// <summary>The sections of a footprint at a level, from section y <paramref name="syMin"/> to <paramref name="syMax"/>.</summary>
    /// <param name="level">The level.</param>
    /// <param name="sx">The footprint's x in sections of the level.</param>
    /// <param name="sz">The footprint's z.</param>
    /// <param name="syMin">The lowest section y.</param>
    /// <param name="syMax">The highest.</param>
    public LodSection[] Stack(int level, int sx, int sz, int syMin, int syMax)
    {
        var cell = 1 << level;
        var grid = new LodGrid(level, sx * LodSection.Size, sz * LodSection.Size, LodSection.Size, LodSection.Size);
        _columns.Fill(grid);
        var sections = new LodSection[syMax - syMin + 1];
        for (var i = 0; i < sections.Length; i++)
        {
            sections[i] = LodSection.Air();
        }

        var lowest = _minY >> level;
        var highest = (_maxY - 1) >> level;
        for (var j = 0; j < LodSection.Size; j++)
        {
            for (var i = 0; i < LodSection.Size; i++)
            {
                var runs = grid.Column(i, j);
                if (cell == 1)
                {
                    foreach (var run in runs)
                    {
                        for (var y = Math.Max(run.Bottom, _minY); y <= Math.Min(run.Top, _maxY - 1); y++)
                        {
                            sections[(y >> LodSection.Shift) - syMin].SetBlock(LodSection.Index(i, y & 31, j), run.Block);
                        }
                    }

                    continue;
                }

                var top = Math.Min(highest, runs.IsEmpty ? lowest : runs[0].Top >> level);
                for (var vy = lowest; vy <= top; vy++)
                {
                    var block = Cell(runs, vy * cell, ((vy + 1) * cell) - 1, cell);
                    if (block != BlockId.Air)
                    {
                        sections[(vy >> LodSection.Shift) - syMin].SetBlock(LodSection.Index(i, vy & 31, j), block);
                    }
                }
            }
        }

        var sky = new byte[LodSection.Size * LodSection.Size];
        Array.Fill(sky, (byte)15);
        var below = new byte[sky.Length];
        for (var k = sections.Length - 1; k >= 0; k--)
        {
            sections[k].Simplify();
            LodMip.Light(sections[k], sky, below);
            (sky, below) = (below, sky);
        }

        return sections;
    }

    // The block of a voxel whose blocks run from y0 to y1: the runs' share of it decides (see the class's summary).
    private static BlockId Cell(ReadOnlySpan<LodSegment> runs, int y0, int y1, int cell)
    {
        var opaque = 0;
        var water = 0;
        var leaves = 0;
        var trunk = 0;
        var best = BlockId.Air;
        var bestThickness = 0;
        var trunkBlock = BlockId.Air;
        foreach (var run in runs)
        {
            var overlap = Math.Min(run.Top, y1) - Math.Max(run.Bottom, y0) + 1;
            if (overlap <= 0)
            {
                continue;
            }

            if (run.Block == BlockId.Water)
            {
                water += overlap;
                continue;
            }

            opaque += overlap;
            if (BlockDefinition.IsLeaves(run.Block))
            {
                leaves += overlap;
            }

            if (BlockDefinition.IsTrunk(run.Block))
            {
                trunk += overlap;
                trunkBlock = run.Block;
            }

            if (overlap > bestThickness)
            {
                best = run.Block;
                bestThickness = overlap;
            }
        }

        if ((2 * opaque) >= cell || trunk > 0 || (4 * leaves) >= cell)
        {
            return trunk > 0 ? trunkBlock : best;
        }

        return (2 * water) >= cell ? BlockId.Water : BlockId.Air;
    }
}
