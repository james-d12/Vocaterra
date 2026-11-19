using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Far;
using Vocaterra.Game.Core.World.Regions;
using Vocaterra.Game.Core.World.Underground;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Far;

/// <summary>
/// The far view's coarse levels after B37 (levels 3 to 7 made cheaper: the trees read through the scope and a lattice-line column reads one corner, which change nothing; from level 4 the cliff test's neighbours take the nearest lattice
/// point's climate and no river is pooled from the climate's river field, which the drainage network's rivers no longer follow): the picture's invariants still hold over a sample of places in two worlds. The exact columns are the same
/// source with the shortcuts off (<c>coarseFrom</c> past the last level), which is the sampler's own answer at every column the cells are made from (the model's columns are checked against the generator's chunks by LodModelSourceTests).
/// No cell has a hole; level 3 is unchanged to the block; from level 4 up the cells are the exact ones but for a few percent; water is where the exact cells have it; the ground is as high as the exact ground within a stated tolerance; the cells
/// the exact sampler tops with a cliff's rock still are for the share said in the test.
/// </summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class FarCoarseLevelsTests(ITestOutputHelper output)
{
    private const int Side = 24; // cells along a side of a sampled grid

    // Land with a mountain (three places), a forest, a coast and a river in each of two worlds.
    private static IEnumerable<(ulong Seed, string Place, FarTerrainSampler Sampler, int Cx, int Cz)> Places()
    {
        foreach (var seed in new ulong[] { 42, 2026 })
        {
            var sampler = new FarTerrainSampler(ProceduralTestWorld.Source(seed).World, RegionCatalog.Empty);
            foreach (var (feature, skip) in new[] { (Feature.Mountain, 0), (Feature.Mountain, 3), (Feature.Mountain, 8), (Feature.Forest, 0), (Feature.Coast, 0), (Feature.River, 0) })
            {
                var (cx, cz) = ProceduralTestWorld.Find(seed, feature, skip);
                yield return (seed, $"{feature}#{skip}", sampler, cx, cz);
            }
        }
    }

    // The grid of a level over a place (Side x Side cells with the place near their middle), made by the model with the coarse shortcuts from the level given.
    private static LodGrid Grid(FarTerrainSampler sampler, int level, int cx, int cz, int mergeLevels, int coarseFrom)
    {
        var cell = 1 << level;
        var grid = new LodGrid(level, ((cx << VoxelChunk.Shift) / cell) - (Side / 2), ((cz << VoxelChunk.Shift) / cell) - (Side / 2), Side, Side);
        sampler.Lod(4, mergeLevels, coarseFrom).Fill(grid);
        return grid;
    }

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

    [Fact]
    public void Given_PlacesAtLevels3To7_When_TheModelFillsTheirCells_Then_NoColumnHasAHoleAndEveryColumnReachesTheBottom()
    {
        foreach (var (seed, place, sampler, cx, cz) in Places())
        {
            for (var level = 3; level <= 7; level++)
            {
                var grid = Grid(sampler, level, cx, cz, LodModelSource.DefaultMergeLevels, LodModelSource.DefaultCoarseFrom);
                for (var j = 0; j < Side; j++)
                {
                    for (var i = 0; i < Side; i++)
                    {
                        var runs = grid.Column(i, j);
                        Assert.False(runs.IsEmpty, $"seed {seed} {place} L{level} cell {i},{j} is empty");
                        Assert.Equal(sampler.MinY, runs[^1].Bottom);
                        Assert.NotEqual(BlockId.Air, runs[0].Block);
                    }
                }
            }
        }
    }

    [Fact]
    public void Given_PlacesAtLevel3_When_TheShortcutsAreOnAndOff_Then_EveryColumnIsTheSame()
    {
        foreach (var (seed, place, sampler, cx, cz) in Places())
        {
            var fast = Grid(sampler, 3, cx, cz, LodModelSource.DefaultMergeLevels, LodModelSource.DefaultCoarseFrom);
            var exact = Grid(sampler, 3, cx, cz, 7, 99);
            for (var j = 0; j < Side; j++)
            {
                for (var i = 0; i < Side; i++)
                {
                    Assert.True(fast.Column(i, j).SequenceEqual(exact.Column(i, j)), $"seed {seed} {place} L3 cell {i},{j}");
                }
            }
        }
    }

    private sealed record Shares(int Cells, int Same, int HeightSame, int HeightWithin8, int HeightWithin24, int Water, int WaterKept, int WaterNew, int Rock, int RockKept)
    {
        public override string ToString() => $"{Cells} cells at levels 4 to 7: {Same} identical columns, top at the same height in {HeightSame}, within 8 blocks in {HeightWithin8}, within 24 in {HeightWithin24}; water in {Water} exact cells, kept in {WaterKept}, new in {WaterNew}; rock-topped {Rock} exact cells, still in {RockKept}";
    }

    // The cells of levels 4 to 7 over every place, made with the given merge level and the shortcuts on, against the exact ones (every level merged, no shortcut).
    private static Shares Compare(int mergeLevels, int firstLevel = 4, int lastLevel = 7)
    {
        int cells = 0, same = 0, heightSame = 0, within8 = 0, within24 = 0, water = 0, waterKept = 0, waterNew = 0, rock = 0, rockKept = 0;
        var bands = TestGame.Data.Underground!.SurfaceRock!.Cliff.Bands.Select(static b => UndergroundWorld.BlockNamed(b.Block)).ToHashSet();
        foreach (var (_, _, sampler, cx, cz) in Places())
        {
            for (var level = firstLevel; level <= lastLevel; level++)
            {
                var fast = Grid(sampler, level, cx, cz, mergeLevels, LodModelSource.DefaultCoarseFrom);
                var exact = Grid(sampler, level, cx, cz, 7, 99);
                for (var j = 0; j < Side; j++)
                {
                    for (var i = 0; i < Side; i++)
                    {
                        var a = exact.Column(i, j);
                        var b = fast.Column(i, j);
                        cells++;
                        same += a.SequenceEqual(b) ? 1 : 0;
                        var diff = Math.Abs(a[0].Top - b[0].Top);
                        heightSame += diff == 0 ? 1 : 0;
                        within8 += diff <= 8 ? 1 : 0;
                        within24 += diff <= 24 ? 1 : 0;
                        water += HasWater(a) ? 1 : 0;
                        waterKept += HasWater(a) && HasWater(b) ? 1 : 0;
                        waterNew += !HasWater(a) && HasWater(b) ? 1 : 0;
                        rock += bands.Contains(a[0].Block) ? 1 : 0;
                        rockKept += bands.Contains(a[0].Block) && bands.Contains(b[0].Block) ? 1 : 0;
                    }
                }
            }
        }

        return new Shares(cells, same, heightSame, within8, within24, water, waterKept, waterNew, rock, rockKept);
    }

    [Fact]
    public void Given_PlacesAtLevels4To7_When_TheShortcutsAreOnAndOff_Then_WaterAndGroundAreTheExactOnesWithinTheStatedShares()
    {
        var m = Compare(7);
        output.WriteLine(m.ToString());
        Assert.True(m.Cells == 6 * 2 * 4 * Side * Side, $"{m.Cells} cells");
        Assert.True(m.Same >= m.Cells * 0.95, $"{m.Same} of {m.Cells} columns are identical");
        Assert.True(m.HeightSame >= m.Cells * 0.97, $"{m.HeightSame} of {m.Cells} cells have their top at the exact height");
        Assert.True(m.HeightWithin8 >= m.Cells * 0.99, $"{m.HeightWithin8} of {m.Cells} cells have their top within 8 blocks of the exact height");
        Assert.True(m.Water > 500, $"{m.Water} wet cells in the sample");
        Assert.True(m.WaterKept >= m.Water * 0.99, $"{m.WaterKept} of {m.Water} wet cells keep their water");
        Assert.True(m.WaterNew <= m.Cells * 0.002, $"{m.WaterNew} cells gain water");
        Assert.True(m.Rock > 10, $"{m.Rock} rock-topped cells in the sample");
        Assert.True(m.RockKept >= m.Rock * 0.5, $"{m.RockKept} of {m.Rock} rock-topped cells keep their rock");
    }
}
