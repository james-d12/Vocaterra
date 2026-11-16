using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Lod;

namespace Vocaterra.Game.Core.World.Far;

/// <summary>
/// The far view's sections made from the generator's own model (B07c): the stand-in for a place that has no real chunk data yet. The columns come from <see cref="LodModelSource"/> (the height model's ground, water and snow, the biome's layers under
/// the surface, the tree pass's own trees, the regions' own blocks) and each is cut into the voxels of the level: the surfaces are quantised and not the volumes: the voxel holding the top of a solid run is solid and takes the exposed block, the voxel holding the water's top is water, the others by half; the stack is then lit from the sky down. Written in the store's own format, so real data replaces it key for key. Pure.
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

    // The block of a voxel whose blocks run from y0 to y1, quantising surfaces and not volumes: the voxel that holds the top of a solid run is solid and takes the exposed block (the highest run in it: the snow, the grass), the voxel
    // that holds the top of the water is water (one voxel at least, over its bed), the others by the share of them that is solid or water (half).
    private static BlockId Cell(ReadOnlySpan<LodSegment> runs, int y0, int y1, int cell)
    {
        var opaque = 0;
        var water = 0;
        var exposed = BlockId.Air;
        var exposedTop = int.MinValue;
        var opaqueTopIn = false;
        var opaqueTopY = int.MinValue;
        var waterTopIn = false;
        var waterTopY = int.MinValue;
        foreach (var run in runs)
        {
            var overlap = Math.Min(run.Top, y1) - Math.Max(run.Bottom, y0) + 1;
            if (overlap <= 0)
            {
                continue;
            }

            var topHere = run.Top <= y1;
            if (run.Block == BlockId.Water)
            {
                water += overlap;
                if (topHere)
                {
                    waterTopIn = true;
                    waterTopY = Math.Max(waterTopY, run.Top);
                }

                continue;
            }

            opaque += overlap;
            if (run.Top > exposedTop)
            {
                exposedTop = run.Top;
                exposed = run.Block;
            }

            if (topHere)
            {
                opaqueTopIn = true;
                opaqueTopY = Math.Max(opaqueTopY, run.Top);
            }
        }

        if (waterTopIn && (!opaqueTopIn || waterTopY >= opaqueTopY))
        {
            return BlockId.Water;
        }

        if ((2 * opaque) >= cell || opaqueTopIn)
        {
            return exposed;
        }

        return (2 * water) >= cell ? BlockId.Water : BlockId.Air;
    }
}
