using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Graphics.Far;
using Vocaterra.Game.Streaming;
using Vocaterra.Game.Tests.Streaming;
using Xunit.Abstractions;

namespace Vocaterra.Game.Tests.Graphics.Far;

/// <summary>B38: a fine leaf over footprints the ring has yet to make real waits for them, behind its stand-in, and is built once from the real data.</summary>
public sealed class FarViewRingWaitTests(ITestOutputHelper output) : FarViewTestsBase(output)
{
    private static readonly FarViewSettings Wide = new(2048, MaxTiles: 400, TreeLevels: 1, RealRadius: 192);

    private const int RingRadius = 192;

    private static (int Fine, int Built, int Cached, int Waited, int Uncovered) Run(bool wait)
    {
        var world = StreamedWorlds.Composition.World;
        using var pool = new JobPool(0);
        var view = Make(pool, Wide);
        var ring = new LodRing(view.World, world, pool, new ColumnBandCache(world.Bands));
        if (wait)
        {
            view.SetRingWait(ring.Awaits);
        }

        var waited = 0;
        var until = HangGuard.DeadlineAfter(HangGuardMilliseconds);
        for (var frame = 0; frame < 200_000 && !HangGuard.Passed(until); frame++)
        {
            ring.Update(192, 192, RingRadius);
            view.Update(192, 192, Voxel, static _ => { }, 0);
            waited = Math.Max(waited, view.Waiting);
            Land(view, 192, 192);
            if (ring.HasPlan && ring.IsIdle && view.Waiting == 0 && view.Missing == 0 && view.Requested.Count == 0 && view.Stale == 0)
            {
                break;
            }
        }

        Assert.Equal(0, view.Missing);
        var fine = FarTileSelector.Select(Wide, 192, 192, Voxel).Count(static k => k.Level <= 2);
        return (fine, (int)view.Stats.Built, view.Cache.Count, waited, view.Uncovered);
    }

    [Fact]
    [Trait(TestSpeed.Trait, TestSpeed.Slow)]
    public void Given_FineLeavesOverTheRingsFootprints_When_TheViewFillsWithTheRing_Then_EachTileIsBuiltOnceFromRealDataAndNoLeafIsUncovered()
    {
        var gated = Run(wait: true);
        var ungated = Run(wait: false);

        Assert.True(gated.Fine > 4, $"{gated.Fine} fine leaves");
        Assert.True(gated.Waited > 0, "some leaves waited for the ring");
        Assert.Equal(gated.Cached, gated.Built); // no key was built twice: nothing was built from the model and again from the real chunks
        Assert.Equal(0, gated.Uncovered);
        Assert.True(ungated.Built > ungated.Cached, $"without the wait the fine tiles are built twice ({ungated.Built} builds for {ungated.Cached} tiles)"); // the test has teeth
        Assert.Equal(ungated.Cached, gated.Cached); // the same tiles in the end
    }

    [Fact]
    public void Given_AGateThatHoldsEveryFootprint_When_TheViewUpdates_Then_FineLeavesAreNotAskedForUntilItOpensAndCoarseOnesAre()
    {
        using var pool = new JobPool(0);
        var view = Make(pool, Wide);
        var open = false;
        view.SetRingWait((_, _) => !open);

        for (var frame = 0; frame < 60; frame++)
        {
            view.Update(192, 192, Voxel, static _ => { }, 0);
        }

        Assert.True(view.Waiting > 0);
        Assert.DoesNotContain(view.Cache.Keys, static k => k.Level <= 2 && FarTileSelector.Select(Wide, 192, 192, Voxel).Contains(k));
        Assert.Contains(view.Cache.Keys, static k => k.Level >= 3); // stand-ins and coarse leaves are built meanwhile

        open = true;
        for (var frame = 0; frame < 4000 && view.Missing + view.Waiting > 0; frame++)
        {
            view.Update(192, 192, Voxel, static _ => { }, 0);
        }

        Assert.Equal(0, view.Waiting);
        Assert.Equal(0, view.Missing);
    }
}
