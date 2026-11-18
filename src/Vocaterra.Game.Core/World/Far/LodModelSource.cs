using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Generation.Procedural;
using Vocaterra.Game.Core.World.Regions;

namespace Vocaterra.Game.Core.World.Far;

/// <summary>
/// The far view's columns made from the generator's own model (B07c, D-game-34 item 25): for each column the height model's ground, its water and its snow, the biome's layers under the surface (the top block, the filler, the stone), the trees the
/// tree pass places (the pure shared query, so every tree stands where the generator puts it, rasterised block by block as the tree pass writes it), and, inside a region, the region's own blocks read down its chunks. A level above 0 merges the
/// four columns of the finer cells it holds (<see cref="LodMerge"/>), the finer columns being taken straight from the model at their centres rather than from the levels beneath (a cell of 128 blocks holds 16,384 columns; four samples
/// stand for them). Where the model makes columns it makes crowns only below <see cref="TreeLevels"/>; above, a forest is a layer of leaves on a short trunk as high as its trees' crowns on average. Pure and immutable: safe from any thread.
/// </summary>
public sealed class LodModelSource : ILodColumnSource
{
    // A forest this thick (the biome's tree density, percent) is drawn as a layer of leaves where single trees are not.
    private const int CanopyFrom = 25;

    // From this level on a column's samples lie on the climate's lattice points (the cell is 8 blocks or more and the samples a quarter and half of it from its edge, the lattice 4), so the climate is the lattice point's own and the neighbours the cliff test reads take the nearest point's (B37).
    private const int CoarseFrom = 4;

    // The columns the tree pass reads for each chunk's 16 candidates (a candidate that survives the spacing and its biome's density reads about a dozen columns, most of them shared with the next candidate's).
    private const int TreeColumnsPerChunk = 96;

    private readonly FarTerrainSampler _sampler;

    internal LodModelSource(FarTerrainSampler sampler, int treeLevels, int mergeLevels)
    {
        _sampler = sampler;
        TreeLevels = treeLevels;
        MergeLevels = mergeLevels;
    }

    /// <summary>The levels below this get every tree as its blocks; the levels from it up get forests as layers.</summary>
    public int TreeLevels { get; }

    /// <summary>The highest level that is a merge of four finer columns; the levels above it take one column at the cell's centre, a quarter of the work (the default merges every level).</summary>
    public int MergeLevels { get; }

    /// <inheritdoc />
    public void Fill(LodGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        var cell = grid.Cell;
        var exact = grid.Level < TreeLevels;
        var samples = grid.Width * grid.Depth * (grid.Level == 0 || grid.Level > MergeLevels ? 1 : 4);
        if (exact)
        {
            // The tree pass's candidates look at the ground round each trunk: some columns a tree, shared with its neighbours' (a tree row of the scope's tables stands for several columns).
            var reach = _sampler.World.Strategies.MaxCrownReach;
            var chunks = (((grid.Width * cell) + (2 * reach)) / VoxelChunk.Size) + 2;
            samples += chunks * (((grid.Depth * cell) + (2 * reach)) / VoxelChunk.Size + 2) * TreeColumnsPerChunk / 6;
        }

        var scope = _sampler.NewScope(samples, grid.Level >= CoarseFrom);
        if (grid.Level == 0)
        {
            var trees = exact ? Trees(grid.X0, grid.Z0, grid.Width, grid.Depth, scope) : null;
            Span<LodSegment> column = stackalloc LodSegment[LodGrid.MaxSegments];
            for (var j = 0; j < grid.Depth; j++)
            {
                for (var i = 0; i < grid.Width; i++)
                {
                    grid.Set(i, j, column[..Direct(grid.X0 + i, grid.Z0 + j, trees, column, scope)]);
                }
            }

            return;
        }

        if (grid.Level > MergeLevels)
        {
            var single = exact ? Trees(grid.X0 * cell, grid.Z0 * cell, grid.Width * cell, grid.Depth * cell, scope) : null;
            Span<LodSegment> one = stackalloc LodSegment[LodGrid.MaxSegments];
            for (var j = 0; j < grid.Depth; j++)
            {
                for (var i = 0; i < grid.Width; i++)
                {
                    var (x, z) = (((grid.X0 + i) * cell) + (cell / 2), ((grid.Z0 + j) * cell) + (cell / 2));
                    var count = Direct(x, z, single, one, scope);
                    if (PoolsRivers(grid.Level) && _sampler.TryRiver(x, z, cell, scope, out var line) && !HasWater(one[..count]))
                    {
                        count = FromSample(line, 0, 0, null, one);
                    }

                    grid.Set(i, j, one[..count]);
                }
            }

            return;
        }

        // The finer cells of the grid: twice as many each way, each sampled at its centre.
        var half = cell / 2;
        var childLevel = grid.Level - 1;
        var child = new LodGrid(childLevel, grid.X0 * 2, grid.Z0 * 2, grid.Width * 2, grid.Depth * 2);
        var raster = exact ? Trees(grid.X0 * cell, grid.Z0 * cell, grid.Width * cell, grid.Depth * cell, scope) : null;
        Span<LodSegment> scratch = stackalloc LodSegment[LodGrid.MaxSegments];
        for (var j = 0; j < child.Depth; j++)
        {
            for (var i = 0; i < child.Width; i++)
            {
                var x = ((child.X0 + i) * half) + (half / 2);
                var z = ((child.Z0 + j) * half) + (half / 2);
                child.Set(i, j, scratch[..Direct(x, z, raster, scratch, scope)]);
            }
        }

        Span<LodSegment> merged = stackalloc LodSegment[LodGrid.MaxSegments];
        for (var j = 0; j < grid.Depth; j++)
        {
            for (var i = 0; i < grid.Width; i++)
            {
                var count = LodMerge.Merge(child.Column(2 * i, 2 * j), child.Column((2 * i) + 1, 2 * j), child.Column(2 * i, (2 * j) + 1), child.Column((2 * i) + 1, (2 * j) + 1), merged);
                if (PoolsRivers(grid.Level) && _sampler.TryRiver(((grid.X0 + i) * cell) + (cell / 2), ((grid.Z0 + j) * cell) + (cell / 2), cell, scope, out var river) && !HasWater(merged[..count]))
                {
                    count = FromSample(river, 0, 0, null, merged);
                }

                grid.Set(i, j, merged[..count]);
            }
        }
    }

    // The pooling of a river into a cell wide enough to miss it reads the climate's river field's zero line, which the drainage network's rivers (B02c) no longer follow: measured over seeds 42 and 2026 (B37), from level 4 up it
    // fired in 4 cells of 10,240 and found a river in none, while three more lattice corners a cell made it a third of a coarse stack's time. Levels 3 and below keep it (their corners are shared).
    private static bool PoolsRivers(int level) => level is >= 3 and < CoarseFrom;

    private static bool HasWater(ReadOnlySpan<LodSegment> column)
    {
        foreach (var run in column)
        {
            if (run.Block == BlockId.Water)
            {
                return true;
            }
        }

        return false;
    }

    // ---- one column of the model

    private int Direct(int x, int z, TreeRaster? trees, Span<LodSegment> into, FarScope scope)
    {
        if (_sampler.Regions.RegionAt(x, z) is { } region)
        {
            return RegionColumn(region, x, z, into);
        }

        return FromSample(_sampler.At(x, z, scope), x, z, trees, into);
    }

    private int FromSample(FarSample s, int x, int z, TreeRaster? trees, Span<LodSegment> into)
    {
        Span<LodSegment> runs = stackalloc LodSegment[32];
        var n = 0;
        var surface = s.SurfaceY;
        if (trees is not null)
        {
            n = trees.Runs(x, z, surface, runs);
        }
        else if (s.Forest >= CanopyFrom && s.Leaves != BlockId.Air && !s.IsWater)
        {
            var bump = (int)(Hash(x, z) % 3) - 1;
            var height = Math.Max(3, (Math.Max(3, s.Canopy) * (60 + (40 * Math.Min(100, s.Forest) / 100)) / 100) + (bump * 2));
            var gap = height / 3;
            runs[n++] = new LodSegment(surface + height, surface + gap + 1, s.Leaves, 15, 0);
            if (gap > 0)
            {
                runs[n++] = new LodSegment(surface + gap, surface + 1, TrunkOf(s.Leaves), 15, 0);
            }
        }

        if (s.IsWater)
        {
            runs[n++] = new LodSegment(s.WaterLevel, s.Height + 1, BlockId.Water, 15, 0);
        }

        var minY = _sampler.MinY;
        var y = s.Height;
        runs[n++] = new LodSegment(y, y, s.Top, 15, 0);
        y--;
        if (s.FillerDepth > 0 && y >= minY)
        {
            runs[n++] = new LodSegment(y, Math.Max(minY, y - s.FillerDepth + 1), s.Filler, 15, 0);
            y -= s.FillerDepth;
        }

        if (y >= minY)
        {
            runs[n++] = new LodSegment(y, minY, BlockId.Stone, 15, 0);
        }

        return Finish(runs[..n], into);
    }

    private static BlockId TrunkOf(BlockId leaves) => leaves switch
    {
        BlockId.SpruceLeaves => BlockId.SpruceTrunk,
        BlockId.BirchLeaves => BlockId.BirchTrunk,
        _ => BlockId.Trunk,
    };

    private static uint Hash(int x, int z)
    {
        var h = ((uint)x * 0x9E3779B1u) ^ ((uint)z * 0x85EBCA6Bu);
        h ^= h >> 15;
        h *= 0x2C1B3C6Du;
        h ^= h >> 13;
        return h;
    }

    // The runs of a column of a region: its own blocks from the top down, a run for each block kind that stands in the open (solid blocks, leaves, water, lights; plants and the like are left out).
    private int RegionColumn(IRegion region, int x, int z, Span<LodSegment> into)
    {
        var bounds = region.Bounds;
        var cx = x >> VoxelChunk.Shift;
        var cz = z >> VoxelChunk.Shift;
        var lx = x & (VoxelChunk.Size - 1);
        var lz = z & (VoxelChunk.Size - 1);
        var runs = new List<LodSegment>(16);
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
                if (block == BlockId.Air || !(block == BlockId.Water || BlockDefinition.IsSolid(block) || BlockDefinition.IsLight(block)))
                {
                    continue;
                }

                if (runs.Count > 0 && runs[^1].Block == block && runs[^1].Bottom == y + 1)
                {
                    runs[^1] = runs[^1] with { Bottom = y };
                }
                else
                {
                    runs.Add(new LodSegment(y, y, block, 15, 0));
                }
            }
        }

        if (runs.Count == 0)
        {
            runs.Add(new LodSegment(bounds.MinY, bounds.MinY, BlockId.Stone, 15, 0));
        }

        return Finish(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(runs), into);
    }

    // The runs capped, then lit: the sky light of each is what the runs above it let through, a run that glows gives its own.
    private static int Finish(ReadOnlySpan<LodSegment> runs, Span<LodSegment> into)
    {
        var count = LodMerge.Cap(runs, into);
        var loss = 0;
        for (var i = 0; i < count; i++)
        {
            var run = into[i];
            into[i] = run with { Sky = (byte)Math.Max(0, 15 - loss), Light = (byte)(BlockDefinition.IsLight(run.Block) ? 15 : 0) };
            loss += run.Block == BlockId.Water || BlockDefinition.IsLeaves(run.Block) ? Math.Min(run.Thickness, 8) : 15;
        }

        return count;
    }

    // ---- the trees of a rectangle of columns, block by block, as the tree pass writes them

    private TreeRaster Trees(int minX, int minZ, int width, int depth, FarScope scope)
    {
        var world = _sampler.World;
        var reach = world.Strategies.MaxCrownReach;
        var planted = _sampler.PlantTrees(minX - reach, minZ - reach, width + (2 * reach), depth + (2 * reach), scope);
        var raster = new TreeRaster(minX, minZ, width, depth);
        raster.Add(planted);
        return raster;
    }

    private sealed class TreeRaster
    {
        private readonly int _minX;
        private readonly int _minZ;
        private readonly int _width;
        private readonly int _depth;
        private readonly List<(int Y, BlockId Block)>?[] _columns;

        public TreeRaster(int minX, int minZ, int width, int depth)
        {
            _minX = minX;
            _minZ = minZ;
            _width = width;
            _depth = depth;
            _columns = new List<(int, BlockId)>?[width * depth];
        }

        private List<(int Y, BlockId Block)>? At(int x, int z, bool create)
        {
            var i = x - _minX;
            var j = z - _minZ;
            if ((uint)i >= (uint)_width || (uint)j >= (uint)_depth)
            {
                return null;
            }

            var at = (j * _width) + i;
            return _columns[at] ??= create ? new List<(int, BlockId)>(8) : null;
        }

        // The trunks first, then the leaves in the order of the priorities, each only where nothing stands yet: the tree pass's own rule.
        public void Add(List<PlantedTree> trees)
        {
            foreach (var tree in trees)
            {
                if (At(tree.X, tree.Z, true) is { } column)
                {
                    for (var y = tree.BaseY + 1; y <= tree.BaseY + tree.Length; y++)
                    {
                        column.Add((y, tree.Species.Trunk));
                    }
                }
            }

            foreach (var tree in trees)
            {
                var top = tree.BaseY + tree.Length;
                var profile = tree.Crown.Profile(tree.Length, tree.Shape);
                var first = top + profile.TopOffset;
                for (var k = 0; k < profile.Limits.Count; k++)
                {
                    var y = first - k;
                    var limit = profile.Limits[k];
                    var reach = StandardTrees.ReachOf(limit);
                    for (var dz = -reach; dz <= reach; dz++)
                    {
                        for (var dx = -reach; dx <= reach; dx++)
                        {
                            var d2 = (dx * dx) + (dz * dz);
                            if (d2 > limit || StandardTrees.DropsRimLeaf(tree.Shape, dx, dz, k, limit, d2) || At(tree.X + dx, tree.Z + dz, true) is not { } column || Has(column, y))
                            {
                                continue;
                            }

                            column.Add((y, tree.Species.Leaves));
                        }
                    }
                }
            }
        }

        private static bool Has(List<(int Y, BlockId Block)> column, int y)
        {
            foreach (var (at, _) in column)
            {
                if (at == y)
                {
                    return true;
                }
            }

            return false;
        }

        // The runs of the tree blocks over the ground of a column, top first: the blocks above the surface, sorted, joined where they touch with the same block.
        public int Runs(int x, int z, int surface, Span<LodSegment> into)
        {
            var column = At(x, z, false);
            if (column is null || column.Count == 0)
            {
                return 0;
            }

            var count = 0;
            var blocks = new List<(int Y, BlockId Block)>(column.Count);
            foreach (var entry in column)
            {
                if (entry.Y > surface)
                {
                    blocks.Add(entry);
                }
            }

            blocks.Sort(static (a, b) => b.Y.CompareTo(a.Y));
            foreach (var (y, block) in blocks)
            {
                if (count > 0 && into[count - 1].Block == block && into[count - 1].Bottom == y + 1)
                {
                    into[count - 1] = into[count - 1] with { Bottom = y };
                }
                else if (count < into.Length - 8)
                {
                    into[count++] = new LodSegment(y, y, block, 15, 0);
                }
            }

            return count;
        }
    }
}
