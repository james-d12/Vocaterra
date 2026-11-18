using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Biomes;
using Vocaterra.Game.Core.World.Climate;
using Vocaterra.Game.Core.World.Generation.Procedural;
using Vocaterra.Game.Core.World.Lod;
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
    // From this cell size on, a river narrower than the cell is pooled into it (see AtCell): the cell's own centre would miss it.
    private const int RiverPoolFrom = 6;
    private const int RiverGradientStep = 4;

    private readonly GenerationWorld _world;
    private readonly RegionCatalog _regions;
    private readonly (BlockId Leaves, int Canopy)[] _crowns;

    internal FarTerrainSampler(GenerationWorld world, RegionCatalog regions)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(regions);
        _world = world;
        _regions = regions;
        _crowns = [.. world.Strategies.Trees.Select(CrownOf)];
    }

    // The leaf block of a row's heaviest species and the height of its crowns over the ground on average (the mean trunk and the rise of the crown, by the species' weights): what a far forest is made of.
    private static (BlockId Leaves, int Canopy) CrownOf(ResolvedTrees trees)
    {
        if (trees.Density <= 0 || trees.Entries.Length == 0)
        {
            return (BlockId.Air, 0);
        }

        var heaviest = trees.Entries[0];
        long height = 0;
        foreach (var entry in trees.Entries)
        {
            heaviest = entry.Weight > heaviest.Weight ? entry : heaviest;
            height += (long)entry.Weight * (((entry.Species.MinTrunk + entry.Species.MaxTrunk) / 2) + entry.Crown.Rise);
        }

        return (heaviest.Species.Leaves, (int)(height / trees.TotalWeight));
    }

    internal GenerationWorld World => _world;

    internal RegionCatalog Regions => _regions;

    /// <summary>The far view's columns made from this model (B07c): the ground, water, snow, trees and a region's own blocks, merged coarser by level.</summary>
    /// <param name="treeLevels">The levels below this get every tree as its blocks, the levels from it up a forest as a layer of leaves.</param>
    /// <param name="mergeLevels">The highest level that is a merge of four finer columns; above it a cell is one column at its centre.</param>
    public LodModelSource Lod(int treeLevels, int mergeLevels = 7) => new(this, treeLevels, mergeLevels);

    /// <summary>The far world over this model (B07c): a store of three-dimensional sections that the model fills where there is no real data, and real chunks replace.</summary>
    /// <param name="store">The store.</param>
    /// <param name="treeLevels">The levels below this get every tree as its blocks.</param>
    public LodWorld FarWorld(LodStore store, int treeLevels) => new(store, new LodModelSections(Lod(treeLevels), MinY, MaxY), MinY, MaxY);

    /// <summary>The water's surface y.</summary>
    public int SeaLevel => _world.SeaLevel;

    /// <summary>The lowest y of the world.</summary>
    public int MinY => _world.MinY;

    /// <summary>One above the highest y of the world.</summary>
    public int MaxY => _world.MaxY;

    /// <summary>The column (x, z).</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    public FarSample At(int x, int z) => At(x, z, null);

    // The column, its climate and shape taken from the call's scope when there is one (B36): the same numbers either way.
    internal FarSample At(int x, int z, FarScope? scope)
    {
        if (_regions.RegionAt(x, z) is { } region)
        {
            return InRegion(region, x, z);
        }

        var shape = scope is null ? default : scope.Shape(x, z);
        var climate = scope is null ? _world.Climate.At(x, z) : scope.Climate(x, z);
        if (scope is null)
        {
            shape = _world.Height.Shape(x, z, climate);
        }

        var column = _world.Table.Classify(climate, shape.Height, x, z, shape.Water, shape.RiverWeight);
        var blocks = _world.Table.Surface(column.Row);
        var top = shape.Water ? blocks.Underwater : column.Snow ? BlockId.Snow : blocks.Top;
        var surface = new ColumnSurface(shape.Height, top, blocks.Filler, blocks.FillerDepth, shape.Water);
        if (_world.SurfaceRock is { } rock)
        {
            surface = rock.Adjust(new FarGround(_world, scope), x, z, surface, foot: false); // the cliffs' bands and the steep scree show from afar (B11b); the foot's scree is too fine for it
        }

        var grows = !shape.Water && column.TreesGrow && !surface.Bare;
        var forest = grows ? column.Row.Trees.Density : 0;
        var (leaves, canopy) = grows ? _crowns[column.Row.Index] : (BlockId.Air, 0);
        return new FarSample(shape.Height, shape.Water ? Even(x, z, shape.WaterLevel, scope) : -1, surface.Top, column.Snow, forest, surface.Filler, surface.FillerDepth, forest > 0 ? leaves : BlockId.Air, forest > 0 ? canopy : 0);
    }

    /// <summary>
    /// The land of a cell of the far view (B07c): the column at (<paramref name="x"/>, <paramref name="z"/>), the cell's centre, and, for a cell wide enough to miss it, the river that runs through the cell. A river is a few blocks wide, so
    /// beyond the chunks no cell's centre would fall in it; the river field's zero line is found from its gradient at the centre and, when it lies within half a cell of it, the cell takes the water on that line. A river shows at least one cell wide.
    /// </summary>
    /// <param name="x">The block's x at the cell's centre.</param>
    /// <param name="z">The block's z.</param>
    /// <param name="cell">The cell's side in blocks.</param>
    public FarSample AtCell(int x, int z, int cell) => AtCell(x, z, cell, null);

    internal FarSample AtCell(int x, int z, int cell, FarScope? scope) => TryRiver(x, z, cell, scope, out var river) ? river : At(x, z, scope);

    internal FarScope NewScope(int samples, bool coarse = false) => new(_world, samples, coarse);

    // The river that runs through a cell too wide to catch it at its centre: the sample on the river field's zero line when that lies within half a cell of the centre and holds water there.
    internal bool TryRiver(int x, int z, int cell, out FarSample river) => TryRiver(x, z, cell, null, out river);

    internal bool TryRiver(int x, int z, int cell, FarScope? scope, out FarSample river)
    {
        river = default;
        // A dry column's sample has no water, a wet one's has (the levelling only raises it): the shape alone says which, so the sample itself is not made here.
        if (cell < RiverPoolFrom || _regions.RegionAt(x, z) is not null || (scope is null ? At(x, z, null).IsWater : scope.Shape(x, z).Water))
        {
            return false;
        }

        // The river field's zero line, found from its gradient (integers, differences over a few blocks): the line passes within half a cell of the centre when 8 |field| <= |gradient| x cell, and the nearest point of it is the centre
        // less field x gradient / |gradient| squared (the gradient in field units over RiverGradientStep blocks, so 4 x).
        var climate = _world.Climate;
        long here = (scope is null ? climate.At(x, z) : scope.Climate(x, z)).River;
        long gx = (scope is null ? climate.At(x + RiverGradientStep, z) : scope.Climate(x + RiverGradientStep, z)).River - here;
        long gz = (scope is null ? climate.At(x, z + RiverGradientStep) : scope.Climate(x, z + RiverGradientStep)).River - here;
        var g2 = (gx * gx) + (gz * gz);
        if (g2 == 0 || 4L * RiverGradientStep * RiverGradientStep * here * here > g2 * cell * cell)
        {
            return false;
        }

        var (px, pz) = (x - (int)(RiverGradientStep * here * gx / g2), z - (int)(RiverGradientStep * here * gz / g2));
        var line = At(px, pz, scope);
        if (!line.IsWater)
        {
            return false;
        }

        river = line;
        return true;
    }

    internal List<PlantedTree> PlantTrees(int minX, int minZ, int sizeX, int sizeZ, FarScope? scope = null)
    {
        var maxX = minX + sizeX;
        var maxZ = minZ + sizeZ;
        var cx0 = minX >> VoxelChunk.Shift;
        var cz0 = minZ >> VoxelChunk.Shift;
        return TreePlacement.Plan(_world, new FarGround(_world, scope), cx0, cz0, ((maxX - 1) >> VoxelChunk.Shift) - cx0 + 1, ((maxZ - 1) >> VoxelChunk.Shift) - cz0 + 1, minX, minZ, maxX, maxZ);
    }

    /// <summary>
    /// The trees of a rectangle of the world (B07c), the tree pass's own placement asked of the height model's ground: the same candidates, the same spacing, density, tree line, species and trunk, and no chunk generated. A tree beside a bank
    /// the generator cuts stands where the model's own ground does (its base may differ by the cut), nothing more; the trees of a region are the region's own and are left out. Pure.
    /// </summary>
    /// <param name="minX">The rectangle's first x.</param>
    /// <param name="minZ">The rectangle's first z.</param>
    /// <param name="sizeX">The blocks along x.</param>
    /// <param name="sizeZ">The blocks along z.</param>
    public IReadOnlyList<FarTree> TreesIn(int minX, int minZ, int sizeX, int sizeZ)
    {
        var planted = PlantTrees(minX, minZ, sizeX, sizeZ);
        var trees = new FarTree[planted.Count];
        for (var i = 0; i < trees.Length; i++)
        {
            trees[i] = CrownTiers(planted[i]);
        }

        return trees;
    }

    // The two tiers of a crown: the upper half of its layers and the lower, each as wide as its widest layer.
    private static FarTree CrownTiers(PlantedTree tree)
    {
        var profile = tree.Crown.Profile(tree.Length, tree.Shape);
        var layers = profile.Limits.Count;
        var upper = layers / 2;
        var upperReach = 0;
        var lowerReach = 0;
        for (var k = 0; k < layers; k++)
        {
            var reach = StandardTrees.ReachOf(profile.Limits[k]);
            if (k < upper)
            {
                upperReach = Math.Max(upperReach, reach);
            }
            else
            {
                lowerReach = Math.Max(lowerReach, reach);
            }
        }

        return new FarTree(tree.X, tree.Z, tree.BaseY, tree.Length, tree.Species.Trunk, tree.Species.Leaves, tree.BaseY + tree.Length + profile.TopOffset, upper, upperReach, layers - upper, lowerReach);
    }

    // The height model's ground for the tree placement: the biome and the layers of a column as the model and the table give them, no pass having shaped them. It holds the last column it read in small arrays (a candidate asks the biome, then the surface, of the same column).
    private sealed class FarGround : ITreeGround, IRockGround
    {
        private readonly GenerationWorld _world;
        private readonly int[] _at = [int.MinValue, int.MinValue];
        private readonly ClimateSample[] _climate = new ClimateSample[1];
        private readonly ColumnShape[] _shape = new ColumnShape[1];
        private readonly FarScope? _scope;

        public FarGround(GenerationWorld world, FarScope? scope = null)
        {
            _world = world;
            _scope = scope;
        }

        public BiomeColumn BiomeAt(int x, int z)
        {
            Read(x, z);
            return _world.Table.Classify(_climate[0], _shape[0].Height, x, z, _shape[0].Water, _shape[0].RiverWeight);
        }

        public ColumnSurface SurfaceAt(int x, int z)
        {
            Read(x, z);
            var shape = _shape[0];
            var biome = _world.Table.Classify(_climate[0], shape.Height, x, z, shape.Water, shape.RiverWeight);
            var blocks = _world.Table.Surface(biome.Row);
            var top = shape.Water ? blocks.Underwater : biome.Snow ? BlockId.Snow : blocks.Top;
            var surface = new ColumnSurface(shape.Height, top, blocks.Filler, blocks.FillerDepth, shape.Water);
            return _world.SurfaceRock is { } rock ? rock.Adjust(this, x, z, surface) : surface; // the trees skip the columns the surface rock bares, as the generator's do (B11b)
        }

        public int HeightAt(int x, int z)
        {
            Read(x, z);
            return _shape[0].Height;
        }

        public bool InWindow(int x, int z) => true;

        public ColumnShape ShapeAt(int x, int z)
        {
            Read(x, z);
            return _shape[0];
        }

        public int WaterLevelAt(int x, int z)
        {
            Read(x, z);
            return _shape[0].Water ? _shape[0].WaterLevel : -1;
        }

        // A cave entrance's passage and mouth keep trees off, as in the generator: the entrance sites are planned from the height model and the cave field alone, so the far view finds the same ones (B11b).
        public bool ClearsEntrance(int x, int z)
        {
            if (_world.Entrances is not { } entrances)
            {
                return false;
            }

            foreach (var ramp in entrances.Near(x, z, x, z))
            {
                if (ramp.Clears(x, z))
                {
                    return true;
                }
            }

            return false;
        }

        private void Read(int x, int z)
        {
            if (_at[0] == x && _at[1] == z)
            {
                return;
            }

            if (_scope is null)
            {
                _climate[0] = _world.Climate.At(x, z);
                _shape[0] = _world.Height.Shape(x, z, _climate[0]);
            }
            else
            {
                _shape[0] = _scope.Shape(x, z);
                _climate[0] = _scope.Climate(x, z);
            }

            _at[0] = x;
            _at[1] = z;
        }
    }

    // The water's top of a column as the generator leaves it (B02b round 3): the height model's own level (the sea's for the sea, a river's or a lake's own), and for water above the sea the highest level within
    // the generator's few columns (`WaterEdgePass.EvenLevels`), so a river steps down across itself and not along its length. At a far tile's spacing the columns round it are not samples, so the eight points at that
    // reach stand for them. The bank turned to water beside two levels and the falling cells are left out: they are a column wide.
    private int Even(int x, int z, int own, FarScope? scope)
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

                var shape = scope is null ? _world.Height.Shape(x + dx, z + dz, _world.Climate.At(x + dx, z + dz)) : scope.Shape(x + dx, z + dz);
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
        var scope = NewScope(countX * countZ * 4);
        for (var j = 0; j < countZ; j++)
        {
            for (var i = 0; i < countX; i++)
            {
                into[(j * countX) + i] = At(x0 + (i * step), z0 + (j * step), scope);
            }
        }
    }

    /// <summary>The cells of a regular grid of cells, row by row (x fastest), each sampled at its centre with <see cref="AtCell"/>: cell (i, j) covers the blocks from (<paramref name="x0"/> + i x <paramref name="cell"/>, <paramref name="z0"/> + j x <paramref name="cell"/>) one cell on.</summary>
    /// <param name="x0">The first cell's first x.</param>
    /// <param name="z0">The first row's first z.</param>
    /// <param name="cell">The cell's side in blocks.</param>
    /// <param name="countX">The cells along x.</param>
    /// <param name="countZ">The cells along z.</param>
    /// <param name="into">The samples, at least <paramref name="countX"/> times <paramref name="countZ"/> of them.</param>
    public void FillCells(int x0, int z0, int cell, int countX, int countZ, Span<FarSample> into)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cell);
        ArgumentOutOfRangeException.ThrowIfLessThan(into.Length, countX * countZ);
        var half = cell / 2;
        var scope = NewScope(countX * countZ * 4);
        for (var j = 0; j < countZ; j++)
        {
            for (var i = 0; i < countX; i++)
            {
                into[(j * countX) + i] = AtCell(x0 + (i * cell) + half, z0 + (j * cell) + half, cell, scope);
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
        var crownTop = int.MinValue;
        var leaves = BlockId.Air;
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
                    crownTop = Math.Max(crownTop, y);
                    leaves = leaves == BlockId.Air && BlockDefinition.IsLeaves(block) ? block : leaves;
                }
                else if (BlockDefinition.IsSolid(block))
                {
                    var forested = crownTop > int.MinValue && waterTop < 0;
                    return new FarSample(y, waterTop, block, block == BlockId.Snow, forested ? 100 : 0, block == BlockId.Grass ? BlockId.Dirt : block, 3, forested ? leaves : BlockId.Air, forested ? Math.Max(1, crownTop - y) : 0);
                }
            }
        }

        return new FarSample(bounds.MinY, waterTop, BlockId.Stone, false, 0, BlockId.Stone);
    }
}
