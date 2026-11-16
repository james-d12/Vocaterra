using Vocaterra.Game.Core.Menu;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.Graphics.Far;
using Xunit.Abstractions;

namespace Vocaterra.Game.Tests.Graphics.Far;

/// <summary>The far view's choice of tiles (B07): a quadtree that covers the ground between the voxel zone and the range exactly once, with cells that grow with distance.</summary>
public sealed class FarTileSelectorTests(ITestOutputHelper output)
{
    private static readonly FarViewSettings Settings = new(4096);

    private static readonly (double X, double Z)[] Eyes = [(192, 192), (-3000.5, 777.25), (12345, -9876), (0, 0), (383.9, 208), (-64, -64)];

    private static bool Inside(FarViewSettings settings, FarTileKey key, double x, double z)
    {
        var size = settings.SizeOf(key.Level);
        return x >= (double)key.Tx * size && x < (double)(key.Tx + 1) * size && z >= (double)key.Tz * size && z < (double)(key.Tz + 1) * size;
    }

    [Fact]
    public void Given_AnEye_When_TheLeavesAreChosen_Then_EveryPointBetweenTheInnerRadiusAndTheRangeIsInExactlyOneLeaf()
    {
        const int Voxel = 304;
        foreach (var (ex, ez) in Eyes)
        {
            var leaves = FarTileSelector.Select(Settings, ex, ez, Voxel);
            var inner = Settings.InnerFraction * Voxel;
            var points = 0;
            for (var dz = -4100; dz <= 4100; dz += 61)
            {
                for (var dx = -4100; dx <= 4100; dx += 59)
                {
                    var distance = Math.Sqrt((dx * (double)dx) + (dz * (double)dz));
                    var inView = distance >= inner && distance <= Settings.Range;
                    var count = leaves.Count(key => Inside(Settings, key, ex + dx, ez + dz));
                    if (inView)
                    {
                        Assert.True(count == 1, $"eye ({ex}, {ez}), offset ({dx}, {dz}): {count} leaves hold it");
                        points++;
                    }
                    else
                    {
                        Assert.True(count <= 1, $"eye ({ex}, {ez}), offset ({dx}, {dz}): {count} leaves overlap there");
                    }
                }
            }

            Assert.True(points > 5000);
        }
    }

    [Fact]
    public void Given_TheLeaves_When_TheyAreChecked_Then_NoneIsBeyondTheRangeOrWhollyInsideTheInnerRadiusAndTheNearestFirstOrderHolds()
    {
        foreach (var (ex, ez) in Eyes)
        {
            var leaves = FarTileSelector.Select(Settings, ex, ez, 400);
            var inner = Settings.InnerFraction * 400;
            Assert.NotEmpty(leaves);
            Assert.All(leaves, key =>
            {
                Assert.True(key.NearestDistance(Settings, ex, ez) <= Settings.Range);
                Assert.True(key.FarthestDistance(Settings, ex, ez) >= inner);
            });
            var distances = leaves.Select(key => key.NearestDistance(Settings, ex, ez)).ToList();
            Assert.Equal(distances.Order().ToList(), distances);
            Assert.Equal(leaves, FarTileSelector.Select(Settings, ex, ez, 400));
        }
    }

    [Fact]
    public void Given_TheLeaves_When_TheirLevelsAreChecked_Then_TheCellsGrowWithDistanceAndEachLeafIsAsCoarseAsTheRuleAllows()
    {
        var (ex, ez) = (1000.0, -500.0);
        var leaves = FarTileSelector.Select(Settings, ex, ez, 304);

        Assert.All(leaves, key =>
        {
            var nearest = key.NearestDistance(Settings, ex, ez);
            if (key.Level > 0)
            {
                Assert.True(nearest >= Settings.SplitDistance(key.Level, 304), $"{key} is near enough to be split");
            }

            if (key.Level < Settings.TopLevel)
            {
                Assert.True(key.GetParent().NearestDistance(Settings, ex, ez) < Settings.SplitDistance(key.Level + 1, 304), $"{key}'s parent would not have been split");
            }
        });
        var byLevel = leaves.GroupBy(static l => l.Level).ToDictionary(static g => g.Key, g => g.Min(l => l.NearestDistance(Settings, ex, ez)));
        var ordered = byLevel.OrderBy(static p => p.Key).Select(static p => p.Value).ToList();
        Assert.Equal(ordered.Order().ToList(), ordered); // the nearest leaf of a coarser level is farther than that of a finer one
        Assert.True(byLevel.Count >= 3, "the range holds several levels");
    }

    [Fact]
    public void Given_ARangeOfSixteenKilometres_When_TheLeavesAreChosen_Then_TheirCountAndCellsAreReported()
    {
        var high = new FarViewSettings(16384);

        var leaves = FarTileSelector.Select(high, 192, 192, 400);

        var cells = (long)leaves.Count * high.TileCells * high.TileCells;
        output.WriteLine($"16 km: {leaves.Count} tiles, {cells} cells ({cells * 2} triangles), by level {string.Join(", ", leaves.GroupBy(static l => l.Level).OrderBy(static g => g.Key).Select(static g => $"L{g.Key}={g.Count()}"))}");
        Assert.InRange(leaves.Count, 100, 4000);
        Assert.Equal(high.Range > 0, high.Enabled);
    }

    [Fact]
    public void Given_AWalkOfSixHundredBlocks_When_TheLeavesAreChosenEachBlock_Then_OnlyAFewTilesAtTheEdgesOfTheBandsAreNewEachBlock()
    {
        var first = FarTileSelector.Select(Settings, 37.5, 91.5, 304).ToHashSet();
        var everything = new HashSet<FarTileKey>(first);
        var previous = first;
        var changes = 0;
        var fresh = 0;
        for (var x = 1; x <= 600; x++)
        {
            var now = FarTileSelector.Select(Settings, 37.5 + x, 91.5, 304).ToHashSet();
            if (!now.SetEquals(previous))
            {
                changes++;
                Assert.True(now.Except(previous).Count() <= 64, $"{now.Except(previous).Count()} new tiles at x = {x}");
                fresh += now.Except(previous).Count();
            }

            everything.UnionWith(now);
            previous = now;
        }

        output.WriteLine($"{first.Count} tiles at the start, {everything.Count} used in all, the set changed {changes} times in 600 blocks, {fresh} tiles came new");
        Assert.True(fresh < 600 * 4, $"{fresh} new tiles in 600 blocks");
        Assert.True(everything.Count < first.Count * 3);
    }

    [Fact]
    public void Given_TheFarViewOff_When_TheLeavesAreChosen_Then_ThereAreNone()
    {
        Assert.Empty(FarTileSelector.Select(FarViewSettings.Off, 0, 0, 400));
        Assert.False(FarViewSettings.Off.Enabled);
    }

    [Fact]
    public void Given_TheKeys_When_ParentsAndChildrenAreAsked_Then_TheyNestOnTheSameLattice()
    {
        var key = new FarTileKey(2, -3, 5);

        Assert.All(key.Children(), child =>
        {
            Assert.Equal(1, child.Level);
            Assert.Equal(key, child.GetParent());
            Assert.True(child.MinX(Settings) >= key.MinX(Settings) && child.MinX(Settings) < key.MinX(Settings) + Settings.SizeOf(2));
        });
        Assert.Equal(4, key.Children().Distinct().Count());
        Assert.Equal(2 * Settings.SizeOf(1), Settings.SizeOf(2));
        Assert.Equal(2 * Settings.CellOf(1), Settings.CellOf(2));
        Assert.True(Settings.SkirtOf(4) > Settings.SkirtOf(0));
    }

    [Fact]
    public void Given_ThePresets_When_TheirFarSettingsAreRead_Then_TheRingsFollowTheVoxelZoneAndTheTreesReachFurtherAtHigher()
    {
        var rings = new List<(QualityLevel Level, double Trees)>();
        foreach (var level in new[] { QualityLevel.Low, QualityLevel.Medium, QualityLevel.High })
        {
            var preset = QualityPreset.For(level);
            var far = preset.Far;
            var voxel = preset.ViewDistance;
            Assert.Equal(preset.FarRange, far.Range);
            Assert.Equal(1, far.CellOf(0));
            Assert.Equal(128, far.CellOf(far.TopLevel));
            Assert.Equal(preset.FarTreeLevels, far.TreeLevels);

            // Level 0, block by block, is the ring just past the voxel zone; the next level starts at a tenth beyond it.
            var leaves = FarTileSelector.Select(far, 192, 192, voxel);
            var finest = leaves.Where(static k => k.Level == 0).Select(k => k.NearestDistance(far, 192, 192)).ToList();
            Assert.NotEmpty(finest);
            Assert.True(finest.Min() <= voxel, $"{level}: the finest tiles start at {finest.Min():0}");
            Assert.True(finest.Max() < far.SplitDistance(1, voxel) + far.SizeOf(1), $"{level}: the finest tiles reach {finest.Max():0}");
            Assert.InRange(far.SplitDistance(1, voxel) / voxel, 1.05, 1.2);

            // The cells grow with distance out to the range, and no leaf is drawn under the voxel zone's inner radius.
            Assert.True(leaves.Max(static k => k.Level) >= 4);
            rings.Add((level, far.SplitDistance(far.TreeLevels, voxel)));
        }

        Assert.True(rings[0].Trees < rings[1].Trees && rings[1].Trees < rings[2].Trees, string.Join(", ", rings));
        Assert.InRange(rings[2].Trees, 1400, 2400); // about 2 km at High
        Assert.InRange(rings[0].Trees, 300, 800);
    }
}
