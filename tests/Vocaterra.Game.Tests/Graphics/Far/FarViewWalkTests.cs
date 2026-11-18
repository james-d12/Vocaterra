using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Graphics.Far;
using Vocaterra.Game.Streaming;
using Xunit.Abstractions;

namespace Vocaterra.Game.Tests.Graphics.Far;

/// <summary>Tests split out of FarViewTests (B28), so they run beside the others: TheEyeWalksAcrossATileEdge, TheEyeMovesAFewBlocks, AFarView_When_ItSettles.</summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class FarViewWalkTests(ITestOutputHelper output) : FarViewTestsBase(output)
{
    [Fact]
    public void Given_AFarView_When_ItSettles_Then_EveryLeafIsBuiltAndInTheDrawListNearestFirst()
    {
        using var pool = new JobPool(0);
        var view = Make(pool);

        HangGuard.Settled(limit => view.Settle(192, 192, Voxel, Release, limit));
        var standIns = view.Cache.Keys.Count(k => !FarTileSelector.Select(Small, 192, 192, Voxel).Contains(k)); // B35: the level-4 stand-ins are built and waiting for the card too
        Assert.Equal(view.Unuploaded + standIns, view.PendingTiles.Count);
        Assert.Empty(view.Draw); // built, none on the card: nothing is drawn yet
        Land(view, 192, 192);

        var leaves = FarTileSelector.Select(Small, 192, 192, Voxel);
        Assert.Equal(0, view.Missing);
        Assert.Equal(0, view.Failed);
        Assert.Equal(leaves.Count, view.Draw.Count);
        Assert.Equal(leaves, view.Draw.Select(static t => t.Key).ToList());
        Assert.All(view.Draw, t => Assert.True(t.IsBuilt));
        var stats = view.Stats;
        Assert.Equal(leaves.Count + standIns, stats.Cached);
        Assert.Equal(leaves.Count + view.Cache.Keys.Count(k => !leaves.Contains(k)), stats.Built); // each leaf once, and the B35 stand-ins (their level-4 ancestors) once
        Assert.Equal(view.Draw.Sum(static t => (long)t.Triangles), stats.Triangles);
        Assert.True(stats.BuildMilliseconds > 0 && stats.Bytes > 0);
        Output.WriteLine($"320 blocks: {leaves.Count} tiles, {stats.Triangles} triangles, {stats.Bytes / 1048576.0:0.0} MB, built in {stats.BuildMilliseconds:0} ms (longest {stats.LongestBuildMilliseconds:0.0} ms), Debug, one thread");
    }

    [Fact]
    public void Given_ASettledView_When_TheEyeMovesAFewBlocks_Then_NothingIsBuiltAgain()
    {
        using var pool = new JobPool(0);
        var view = Make(pool);
        HangGuard.Settled(limit => view.Settle(192, 192, Voxel, Release, limit));
        var built = view.Stats.Built;

        view.Update(193.5, 192.5, Voxel, Release, null);
        view.Update(194, 191, Voxel, Release, null);

        Assert.Equal(built, view.Stats.Built);
        Assert.Equal(0, view.Missing);
    }

    [Fact]
    public void Given_ASettledView_When_TheEyeWalksAcrossATileEdge_Then_OnlyTheNewLeavesAreBuiltAndTheRestAreKept()
    {
        using var pool = new JobPool(0);
        var view = Make(pool);
        HangGuard.Settled(limit => view.Settle(192, 192, Voxel, Release, limit));
        Land(view, 192, 192);
        var before = view.Draw.Select(static t => t.Key).ToHashSet();
        var built = view.Stats.Built;
        var cachedBefore = view.Cache.Keys.ToHashSet();

        HangGuard.Settled(limit => view.Settle(192 + 30, 192, Voxel, Release, limit));
        Land(view, 192 + 30, 192);

        var after = view.Draw.Select(static t => t.Key).ToHashSet();
        var fresh = after.Except(before).Count(k => !cachedBefore.Contains(k)); // a stand-in kept from before may become a leaf: not built again
        Assert.Equal(built + fresh, view.Stats.Built);
        Assert.True(fresh < after.Count, "the tiles away from the edge are the same tiles");
        Assert.True(after.Intersect(before).Count() > after.Count / 2);
    }
}
