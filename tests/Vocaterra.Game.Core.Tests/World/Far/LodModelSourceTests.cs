using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Tests.World.Regions;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Far;
using Vocaterra.Game.Core.World.Regions;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Far;

/// <summary>The far view's columns made from the generator's model (B07c): the trees are the tree pass's own, the columns are the generated blocks, a coarser level is the merge of finer ones, and the same cell is the same column from any thread.</summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class LodModelSourceTests(ITestOutputHelper output)
{
    private static FarTerrainSampler SamplerOf(ulong seed) => new(ProceduralTestWorld.Source(seed).World, RegionCatalog.Empty);

    // The chunk columns round the nearest place with a feature: three by three of them.
    private static IEnumerable<(ulong Seed, int Cx, int Cz, Feature Feature)> Samples()
    {
        foreach (var seed in ProceduralTestWorld.Seeds)
        {
            foreach (var feature in new[] { Feature.Forest, Feature.Plain, Feature.Mountain, Feature.Coast })
            {
                var (cx, cz) = ProceduralTestWorld.Find(seed, feature);
                yield return (seed, cx, cz, feature);
            }
        }
    }

    [Fact]
    public void Given_ChunksOverThreeSeeds_When_TheTreeQueryAndTheGeneratorAreCompared_Then_TheTreesAreTheSameInPlaceSpeciesAndHeight()
    {
        var generated = 0;
        var same = 0;
        var queried = 0;
        var baseWithin = 0;
        foreach (var (seed, cx0, cz0, feature) in Samples())
        {
            var source = ProceduralTestWorld.Source(seed);
            var sampler = SamplerOf(seed);
            for (var dz = -1; dz <= 1; dz++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var (cx, cz) = (cx0 + dx, cz0 + dz);
                    var truth = source.ShapedColumn(cx, cz).Trees.Where(t => (t.X >> 4) == cx && (t.Z >> 4) == cz).ToList();
                    var query = sampler.TreesIn(cx << 4, cz << 4, 16, 16);
                    generated += truth.Count;
                    queried += query.Count;
                    foreach (var tree in truth)
                    {
                        if (query.FirstOrDefault(q => q.X == tree.X && q.Z == tree.Z) is { Length: > 0 } found && found.Length == tree.Length && found.Trunk == tree.Species.Trunk && found.Leaves == tree.Species.Leaves)
                        {
                            same++;
                            baseWithin += Math.Abs(found.BaseY - tree.BaseY) <= 4 ? 1 : 0;
                        }
                    }
                }
            }
        }

        output.WriteLine($"{generated} trees generated, {queried} in the query, {same} the same in place, trunk length and species, {baseWithin} of them with the base within 4 blocks");
        Assert.True(generated > 150, $"{generated} trees in the sample");
        Assert.True(same >= generated * 0.97, $"{same} of {generated} generated trees are in the query");
        Assert.True(queried <= generated * 1.03, $"{queried} trees in the query for {generated} generated");
        Assert.True(baseWithin >= same * 0.97, $"{baseWithin} of {same}");
    }

    [Fact]
    public void Given_AWoodlandAwayFromWater_When_TheTreeQueryAndTheGeneratorAreCompared_Then_TheyAreExactlyEqual()
    {
        foreach (var seed in ProceduralTestWorld.Seeds)
        {
            var (cx0, cz0) = ProceduralTestWorld.Find(seed, Feature.Forest);
            var source = ProceduralTestWorld.Source(seed);
            var sampler = SamplerOf(seed);
            var truth = new List<(int, int, int, int, BlockId)>();
            var query = new List<(int, int, int, int, BlockId)>();
            for (var cz = cz0 - 1; cz <= cz0 + 1; cz++)
            {
                for (var cx = cx0 - 1; cx <= cx0 + 1; cx++)
                {
                    truth.AddRange(source.ShapedColumn(cx, cz).Trees.Where(t => (t.X >> 4) == cx && (t.Z >> 4) == cz).Select(t => (t.X, t.Z, t.BaseY, t.Length, t.Species.Leaves)));
                    query.AddRange(sampler.TreesIn(cx << 4, cz << 4, 16, 16).Select(t => (t.X, t.Z, t.BaseY, t.Length, t.Leaves)));
                }
            }

            Assert.True(truth.Count > 30, $"seed {seed}: {truth.Count} trees");
            var missing = truth.Except(query).Count();
            output.WriteLine($"seed {seed}: {truth.Count} generated, {query.Count} queried, {missing} generated trees not in the query exactly");
            Assert.True(missing <= truth.Count / 20, $"seed {seed}: {missing} of {truth.Count}");
        }
    }

    [Fact]
    public void Given_ColumnsOfAWoodland_When_TheModelsColumnsAreComparedWithTheGeneratedChunks_Then_TheCrownsAndTrunksStandInTheSameBlocks()
    {
        const ulong Seed = 42;
        var (cx0, cz0) = ProceduralTestWorld.Find(Seed, Feature.Forest);
        var source = ProceduralTestWorld.Source(Seed);
        var lod = SamplerOf(Seed).Lod(1);
        var grid = new LodGrid(0, cx0 << 4, cz0 << 4, 16, 16);
        lod.Fill(grid);
        var column = source.GenerateColumn(cx0, cz0);
        var equal = 0;
        var checkedColumns = 0;
        for (var j = 0; j < 16; j++)
        {
            for (var i = 0; i < 16; i++)
            {
                var runs = grid.Column(i, j);
                var ground = LodMerge.GroundTop(runs);
                var expected = new List<int>();
                var got = new List<int>();
                for (var k = column.Count - 1; k >= 0; k--)
                {
                    if (column[k].IsEmpty)
                    {
                        continue;
                    }

                    for (var ly = 15; ly >= 0; ly--)
                    {
                        var y = ((source.MinChunkY + k) << 4) + ly;
                        var block = column[k][i, ly, j];
                        if (y > ground && (BlockDefinition.IsLeaves(block) || BlockDefinition.IsTrunk(block)))
                        {
                            expected.Add((y * 4) + (BlockDefinition.IsLeaves(block) ? 1 : 2));
                        }
                    }
                }

                foreach (var run in runs)
                {
                    if (run.Top > ground && (BlockDefinition.IsLeaves(run.Block) || BlockDefinition.IsTrunk(run.Block)))
                    {
                        for (var y = run.Top; y >= run.Bottom; y--)
                        {
                            got.Add((y * 4) + (BlockDefinition.IsLeaves(run.Block) ? 1 : 2));
                        }
                    }
                }

                checkedColumns++;
                equal += expected.Order().SequenceEqual(got.Order()) ? 1 : 0;
            }
        }

        output.WriteLine($"{equal} of {checkedColumns} columns hold the same tree blocks as the generated chunks");
        Assert.True(equal >= checkedColumns * 0.9, $"{equal} of {checkedColumns}");
    }

    [Fact]
    public void Given_ALevelOne_When_ItIsFilled_Then_EachColumnIsTheMergeOfTheFourBlocksItHolds()
    {
        var lod = SamplerOf(42).Lod(4);
        var (cx, cz) = ProceduralTestWorld.Find(42, Feature.Forest);
        var fine = new LodGrid(0, (cx << 4) - 2, (cz << 4) - 2, 12, 12);
        var coarse = new LodGrid(1, (cx << 3) - 1, (cz << 3) - 1, 6, 6);
        lod.Fill(fine);
        lod.Fill(coarse);

        var into = new LodSegment[LodGrid.MaxSegments];
        for (var j = 0; j < 6; j++)
        {
            for (var i = 0; i < 6; i++)
            {
                var count = LodMerge.Merge(fine.Column(2 * i, 2 * j), fine.Column((2 * i) + 1, 2 * j), fine.Column(2 * i, (2 * j) + 1), fine.Column((2 * i) + 1, (2 * j) + 1), into);
                Assert.Equal(into[..count].ToArray(), coarse.Column(i, j).ToArray());
            }
        }
    }

    [Fact]
    public void Given_TheSameCellsAskedTwiceAndFromManyThreads_When_TheyAreFilled_Then_TheColumnsAreIdentical()
    {
        var lod = SamplerOf(7).Lod(3);
        LodGrid Make(int level)
        {
            var grid = new LodGrid(level, -20, 33, 10, 10);
            lod.Fill(grid);
            return grid;
        }

        foreach (var level in new[] { 0, 2, 5 })
        {
            var first = Make(level);
            var results = new LodGrid[4];
            Parallel.For(0, 4, new ParallelOptions { MaxDegreeOfParallelism = 3 }, t => results[t] = Make(level));
            foreach (var other in results)
            {
                for (var j = 0; j < 10; j++)
                {
                    for (var i = 0; i < 10; i++)
                    {
                        Assert.Equal(first.Column(i, j).ToArray(), other.Column(i, j).ToArray());
                    }
                }
            }
        }
    }

    [Fact]
    public void Given_ColumnsOfEveryLevel_When_TheyAreFilled_Then_EachIsASortedStackOfWholeBlockRunsFromTheWorldFloor()
    {
        var sampler = SamplerOf(2026);
        var lod = sampler.Lod(3);
        foreach (var level in new[] { 0, 1, 3, 4, 6 })
        {
            var grid = new LodGrid(level, 60, -40, 12, 12);
            lod.Fill(grid);
            for (var j = 0; j < 12; j++)
            {
                for (var i = 0; i < 12; i++)
                {
                    var runs = grid.Column(i, j);
                    Assert.False(runs.IsEmpty);
                    Assert.True(runs.Length <= LodGrid.MaxSegments);
                    Assert.Equal(sampler.MinY, runs[^1].Bottom);
                    for (var k = 0; k < runs.Length; k++)
                    {
                        Assert.True(runs[k].Top >= runs[k].Bottom);
                        Assert.InRange(runs[k].Sky, 0, 15);
                        Assert.NotEqual(BlockId.Air, runs[k].Block);
                        if (k > 0)
                        {
                            Assert.True(runs[k - 1].Bottom > runs[k].Top, $"level {level}: run {k - 1} lies over run {k}");
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void Given_ColumnsOverASeaAndLand_When_TheyAreFilled_Then_WaterIsARunOverTheBedWithTheBedsTopBlockUnderIt()
    {
        var sampler = SamplerOf(42);
        var lod = sampler.Lod(2);
        var water = 0;
        for (var z = -4000; z <= 4000 && water < 5; z += 211)
        {
            for (var x = -4000; x <= 4000 && water < 5; x += 223)
            {
                var sample = sampler.At(x, z);
                if (!sample.IsWater)
                {
                    continue;
                }

                var grid = new LodGrid(0, x, z, 1, 1);
                lod.Fill(grid);
                var runs = grid.Column(0, 0);
                Assert.Equal(BlockId.Water, runs[0].Block);
                Assert.Equal(sample.WaterLevel, runs[0].Top);
                Assert.Equal(sample.Height + 1, runs[0].Bottom);
                Assert.Equal(sample.Top, runs[1].Block);
                Assert.Equal(sample.Height, runs[1].Top);
                water++;
            }
        }

        Assert.True(water >= 3, $"{water} water columns found");
    }

    [Fact]
    public void Given_AColumnInsideTheValley_When_ItIsFilled_Then_ItHoldsTheRegionsOwnBlocks()
    {
        var composition = RegionTestWorld.Composition();
        var lod = composition.World.Far.Lod(2);
        var region = RegionTestWorld.Valley();
        var matched = 0;
        var total = 0;
        for (var z = 24; z < 360; z += 31)
        {
            for (var x = 24; x < 360; x += 29)
            {
                var grid = new LodGrid(0, x, z, 1, 1);
                lod.Fill(grid);
                var runs = grid.Column(0, 0);
                total++;
                matched += runs[0].Top >= region.EdgeHeight(x, z) - 1 ? 1 : 0;
            }
        }

        Assert.True(matched >= total * 0.9, $"{matched} of {total}");
    }

    [Fact]
    public void Given_CellsOfTheCoarsestLevels_When_ARiverRunsThroughThem_Then_TheRiverShowsAtLeastOneCellWide()
    {
        var sampler = SamplerOf(42);
        var lod = sampler.Lod(0);
        var waterCells = 0;
        var cells = 0;
        foreach (var level in new[] { 3, 4 })
        {
            var cell = 1 << level;
            var grid = new LodGrid(level, -3200 / cell, -3200 / cell, 160, 160);
            lod.Fill(grid);
            for (var j = 0; j < grid.Depth; j++)
            {
                for (var i = 0; i < grid.Width; i++)
                {
                    cells++;
                    var runs = grid.Column(i, j);
                    waterCells += runs[0].Block == BlockId.Water ? 1 : 0;
                }
            }
        }

        output.WriteLine($"{waterCells} of {cells} cells at levels 3 and 4 are water");
        Assert.True(waterCells > 50);
    }
}
