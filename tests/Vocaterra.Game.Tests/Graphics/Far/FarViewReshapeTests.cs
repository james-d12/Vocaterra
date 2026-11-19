using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Graphics.Far;
using Vocaterra.Game.Streaming;
using Vocaterra.Game.Tests.Streaming;
using Vocaterra.Game.Tests.TestSupport;
using Xunit.Abstractions;

namespace Vocaterra.Game.Tests.Graphics.Far;

/// <summary>Tests split out of FarViewTests (B28), so they run beside the others: TileJobRunningWhenTheViewIsReshaped, ANewRange.</summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class FarViewReshapeTests(ITestOutputHelper output) : FarViewTestsBase(output)
{
    [Fact]
    public void Given_ANewRange_When_TheViewIsReconfigured_Then_TheTilesStayForTheSameShapeAndGoForAnother()
    {
        using var pool = new JobPool(0);
        var view = Make(pool);
        HangGuard.Settled(limit => view.Settle(192, 192, Voxel, Release, limit));
        var cached = view.Stats.Cached;

        view.Reconfigure(Small.WithRange(1536), Release);
        Assert.Equal(cached, view.Stats.Cached); // the same shape: the tiles are kept
        Assert.Empty(_released);

        view.Reconfigure(Small with { Levels = Small.Levels - 1 }, Release);
        Assert.Equal(0, view.Stats.Cached);
        Assert.Equal(cached, _released.Count);
    }

    [Fact]
    public void Given_ATileJobRunningWhenTheViewIsReshaped_When_ItFinishesAfterwards_Then_ItsMeshIsRefusedAndTheViewIsBuiltFromTheNewSettingsAlone()
    {
        // B07f, C7: a job of the old settings finishing after the lane was cleared was taken for the answer to a newer request of the same key.
        using var pool = new JobPool(1);
        var gate = new GatedDisk();
        var view = new FarView(StreamedWorlds.Composition.World.Far, Small, pool, new Vocaterra.Game.Core.World.Lod.LodStore(gate));
        view.Update(192, 192, Voxel, Release, 0);
        Assert.True(gate.Started.Wait(HangGuard.Scale(TimeSpan.FromMinutes(10))), HangGuard.Hung("a tile job did not start", TimeSpan.FromMinutes(10)));

        var reshaped = Small with { Levels = 6 };
        view.Reconfigure(reshaped, Release);
        gate.Release.Set();
        var until = HangGuard.DeadlineAfter(HangGuardMilliseconds);
        while (pool.Pending > 0 && !HangGuard.Passed(until))
        {
            Thread.Sleep(5); // the old job finishes and leaves its mesh in the view's queue
        }

        view.Update(192, 192, Voxel, Release, 0); // collects what the old job left: nothing of it may be taken (what this call asks for has not been collected yet)
        Assert.Equal(0, view.Stats.Cached);
        Assert.Equal(0, view.Stats.Built);
        Assert.True(SettleGuard.Settle(limit => view.Settle(192, 192, Voxel, Release, limit), () => view.Stats.Built), "the view settles (no slice without progress)");

        var leaves = FarTileSelector.Select(reshaped, 192, 192, Voxel);
        Assert.Equal(leaves.Count + view.Cache.Keys.Count(k => !leaves.Contains(k)), view.Stats.Built); // each leaf once, and the B35 stand-ins once
        Assert.Equal(0, view.Stale);
    }

    [Fact]
    public void Given_TheDrawingPathChanges_When_UsePathIsCalled_Then_EveryTileIsDroppedToBeBuiltAgainInBothDirectionsAndNeverReadBack()
    {
        // B31: a tile's buffers are write-only on MonoGame's path and cannot be read back, so a change of path (the Shaders switch) builds the tiles again from their data.
        using var pool = new JobPool(0);
        var view = Make(pool);
        Assert.False(view.UsePath(false, Release)); // the first path is taken, nothing is dropped
        HangGuard.Settled(limit => view.Settle(192, 192, Voxel, Release, limit));
        var cached = view.Stats.Cached;
        Assert.True(cached > 0);

        Assert.False(view.UsePath(false, Release)); // the same path: the tiles stay
        Assert.Empty(_released);

        Assert.True(view.UsePath(true, Release)); // off to on
        Assert.Equal(0, view.Stats.Cached);
        Assert.Equal(cached, _released.Count);
        Assert.Empty(view.Draw);
        HangGuard.Settled(limit => view.Settle(192, 192, Voxel, Release, limit)); // built again from the data
        Assert.Equal(cached, view.Stats.Cached);

        _released.Clear();
        Assert.True(view.UsePath(false, Release)); // on to off
        Assert.Equal(cached, _released.Count);
        Assert.Equal(0, view.Stats.Cached);
    }
}
