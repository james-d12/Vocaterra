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
        Assert.True(view.Settle(192, 192, Voxel, Release, 120_000));
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
        Assert.True(gate.Started.Wait(TimeSpan.FromMinutes(10)), "a tile job is running");

        var reshaped = Small with { Levels = 6 };
        view.Reconfigure(reshaped, Release);
        gate.Release.Set();
        var until = Environment.TickCount64 + HangGuardMilliseconds;
        while (pool.Pending > 0 && Environment.TickCount64 < until)
        {
            Thread.Sleep(5); // the old job finishes and leaves its mesh in the view's queue
        }

        view.Update(192, 192, Voxel, Release, 0); // collects what the old job left: nothing of it may be taken (what this call asks for has not been collected yet)
        Assert.Equal(0, view.Stats.Cached);
        Assert.Equal(0, view.Stats.Built);
        Assert.True(SettleGuard.Settle(limit => view.Settle(192, 192, Voxel, Release, limit), () => view.Stats.Built), "the view settles (no slice without progress)");

        var leaves = FarTileSelector.Select(reshaped, 192, 192, Voxel).Count;
        Assert.Equal(leaves, view.Stats.Built);
        Assert.Equal(0, view.Stale);
    }
}
