using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Generation.Procedural;
using Vocaterra.Game.Core.World.Regions;

namespace Vocaterra.Game.Core.World.Far;

/// <summary>
/// The land as the far view sees it (B07, D-game-34 item 21, like the Distant Horizons mod): the height, the top block, the water, the snow and a forest tint of a column read straight from the
/// pure height model and the biome table, with the regions' own ground inside a region, and no chunk generated. It is the same climate, the same blended height and the same biome choice
/// the generator makes a chunk from, so the far land is the near land seen from afar; what it leaves out is what only the passes know (the bank cut beside water, a biome's own surface
/// strategy, trees and the plants), which the voxel zone shows and the far view only tints. Pure and immutable: every answer is a function of the seed, the data, the regions and the column, whenever and
/// from whichever thread it is asked, so a worker thread may build tiles from it.
/// </summary>
public sealed class FarTerrainSampler
{
    private readonly GenerationWorld _world;
    private readonly RegionCatalog _regions;

    internal FarTerrainSampler(GenerationWorld world, RegionCatalog regions)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(regions);
        _world = world;
        _regions = regions;
    }

    /// <summary>The water's surface y.</summary>
    public int SeaLevel => _world.SeaLevel;

    /// <summary>The lowest y of the world.</summary>
    public int MinY => _world.MinY;

    /// <summary>One above the highest y of the world.</summary>
    public int MaxY => _world.MaxY;

    /// <summary>The column (x, z).</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    public FarSample At(int x, int z)
    {
        if (_regions.RegionAt(x, z) is { } region)
        {
            return InRegion(region, x, z);
        }

        var climate = _world.Climate.At(x, z);
        var shape = _world.Height.Shape(x, z, climate);
        var column = _world.Table.Classify(climate, shape.Height, x, z, shape.Water);
        var blocks = _world.Table.Surface(column.Row);
        var top = shape.Water ? blocks.Underwater : column.Snow ? BlockId.Snow : blocks.Top;
        var forest = !shape.Water && column.TreesGrow ? column.Row.Trees.Density : 0;
        return new FarSample(shape.Height, shape.Water ? Even(x, z, shape.WaterLevel) : -1, top, column.Snow, forest);
    }

    // The water's top of a column as the generator leaves it (B02b round 3): the height model's own level (the sea's for the sea, a river's or a lake's own), and for water above the sea the highest level within
    // the generator's few columns (`WaterEdgePass.EvenLevels`), so a river steps down across itself and not along its length. At a far tile's spacing the columns round it are not samples, so the eight points at that
    // reach stand for them. The bank turned to water beside two levels and the falling cells are left out: they are a column wide.
    private int Even(int x, int z, int own)
    {
        if (own <= _world.SeaLevel)
        {
            return own;
        }

        var best = own;
        var reach = GenerationWorld.EvenReach;
        for (var dz = -reach; dz <= reach; dz += reach)
        {
            for (var dx = -reach; dx <= reach; dx += reach)
            {
                if (dx == 0 && dz == 0)
                {
                    continue;
                }

                var shape = _world.Height.Shape(x + dx, z + dz, _world.Climate.At(x + dx, z + dz));
                if (shape.Water)
                {
                    best = Math.Max(best, shape.WaterLevel);
                }
            }
        }

        return best;
    }

    /// <summary>The columns of a regular grid, row by row (x fastest): the point (<paramref name="x0"/> + i x <paramref name="step"/>, <paramref name="z0"/> + j x <paramref name="step"/>) is sample j x <paramref name="countX"/> + i.</summary>
    /// <param name="x0">The first column's x.</param>
    /// <param name="z0">The first row's z.</param>
    /// <param name="step">The distance between neighbouring samples, in blocks.</param>
    /// <param name="countX">The samples along x.</param>
    /// <param name="countZ">The samples along z.</param>
    /// <param name="into">The samples, at least <paramref name="countX"/> times <paramref name="countZ"/> of them.</param>
    public void Fill(int x0, int z0, int step, int countX, int countZ, Span<FarSample> into)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(step);
        ArgumentOutOfRangeException.ThrowIfLessThan(into.Length, countX * countZ);
        for (var j = 0; j < countZ; j++)
        {
            for (var i = 0; i < countX; i++)
            {
                into[(j * countX) + i] = At(x0 + (i * step), z0 + (j * step));
            }
        }
    }

    // A column of a region: read down its own chunks from the top, past the air, the plants and the crowns (a crown makes the column a forest), the water's top noted, to the first solid block.
    private FarSample InRegion(IRegion region, int x, int z)
    {
        var bounds = region.Bounds;
        var cx = x >> VoxelChunk.Shift;
        var cz = z >> VoxelChunk.Shift;
        var lx = x & (VoxelChunk.Size - 1);
        var lz = z & (VoxelChunk.Size - 1);
        var waterTop = -1;
        var crowns = false;
        for (var cy = bounds.EndChunkY - 1; cy >= bounds.MinChunkY; cy--)
        {
            var data = region.Copy(new VoxelChunkCoord(cx, cy, cz));
            if (data.IsEmpty)
            {
                continue;
            }

            for (var ly = VoxelChunk.Size - 1; ly >= 0; ly--)
            {
                var block = data[lx, ly, lz];
                var y = (cy << VoxelChunk.Shift) + ly;
                if (block == BlockId.Water)
                {
                    waterTop = Math.Max(waterTop, y);
                }
                else if (BlockDefinition.IsLeaves(block) || BlockDefinition.IsTrunk(block))
                {
                    crowns = true;
                }
                else if (BlockDefinition.IsSolid(block))
                {
                    return new FarSample(y, waterTop, block, block == BlockId.Snow, crowns && waterTop < 0 ? 100 : 0);
                }
            }
        }

        return new FarSample(bounds.MinY, waterTop, BlockId.Stone, false, 0);
    }
}
