using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World.Streaming;
using Vocaterra.Game.Streaming;

namespace Vocaterra.Game.Tests.Streaming;

/// <summary>Tests split out of ChunkResidencyTests (B28), so they run beside the others: TheSameWalkAndEdits, FreshWorldFarFromTheValley.</summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class ChunkResidencyWalkTests : ChunkResidencyTestsBase
{
    [Fact]
    public void Given_AFreshWorldFarFromTheValley_When_FramesAreSpentInsideABudget_Then_ThePlanStartsFromTheNearBandsAndGrowsAsTheRestArrive()
    {
        var scene = StreamedWorlds.Scene();
        using var pool = new JobPool(0);
        var residency = Make(scene, pool, new BandStreamPolicy(14, 16, 2, 3));
        var frames = 0;

        while (residency.Plan is null && frames++ < 4000)
        {
            residency.Update(2000, 90, 2000, 2.0);
        }

        Assert.NotNull(residency.Plan);
        Assert.False(residency.IsSettled, "the far bands are not all made: the plan is a partial one");
        var first = residency.Plan!.Columns.Count;
        Assert.InRange(first, 20, 700);
        Assert.DoesNotContain(GridResidency.Resident(scene.Grid), c => c.X < 100); // nothing of the valley or anything else was dropped or made by a partial plan

        HangGuard.Settled(limit => residency.Settle(2000, 90, 2000, limit));
        Assert.True(residency.Plan!.Columns.Count > first);
        Assert.All(residency.Plan.Wanted, chunk => Assert.True(scene.Grid.IsResident(chunk)));
    }

    [Fact]
    public void Given_TheSameWalkAndEdits_When_ItRunsUnderTwoPoliciesWithAndWithoutWorkers_Then_TheStateDigestsAndTheSavesAreIdentical()
    {
        var tight = Run(new BandStreamPolicy(3, 4, 1, 2), workers: 0, framesPerStep: 6, settle: false);
        var wide = Run(new BandStreamPolicy(7, 10, 3, 5), workers: 2, framesPerStep: 40, settle: false);
        var settled = Run(new BandStreamPolicy(5, 6, 2, 3), workers: 2, framesPerStep: 0, settle: true);
        var inline = Run(new BandStreamPolicy(5, 6, 2, 3), workers: 0, framesPerStep: 0, settle: true);

        Assert.NotEqual(tight.Resident, wide.Resident); // the policies really kept different chunks
        Assert.Equal(tight.Digest, wide.Digest);
        Assert.Equal(tight.Digest, settled.Digest);
        Assert.Equal(tight.Digest, inline.Digest);
        Assert.Equal(tight.Save, wide.Save);
        Assert.Equal(tight.Save, settled.Save);
        Assert.Equal(tight.Save, inline.Save);
        Assert.InRange(tight.Save.Count, 4, 8); // the edited chunks: one edit was put back, some share a chunk, and where the ground already holds the block an edit is none
    }
}
