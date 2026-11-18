using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Streaming;
using Xunit.Abstractions;

namespace Vocaterra.Game.Tests.Graphics.Far;

/// <summary>Tests split out of FarViewTests (B28), so they run beside the others: LeafNotBuiltYet.</summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class FarViewLeafStandInTests(ITestOutputHelper output) : FarViewTestsBase(output)
{
    [Fact]
    public void Given_ALeafNotBuiltYetWhoseParentIs_When_TheViewIsAssembled_Then_TheParentStandsInBehind()
    {
        using var pool = new JobPool(0);
        var view = Make(pool);
        HangGuard.Settled(limit => view.Settle(5000, 5000, Voxel, Release, limit)); // far from the valley: coarse tiles near the eye
        Land(view, 5000, 5000);
        var parents = view.Draw.Select(static t => t.Key).Where(static k => k.Level > 0).ToList();
        Assert.NotEmpty(parents);

        // Walk right up to a coarse tile: its children become the leaves and are not built yet; one inline job a frame is built.
        var lowest = parents.Min(static k => k.Level);
        var target = parents.Where(k => k.Level == lowest).OrderBy(k => k.NearestDistance(Small, 5000, 5000)).Last();
        var eyeX = target.MinX(Small) + (Small.SizeOf(target.Level) / 2.0);
        var eyeZ = target.MinZ(Small) + (Small.SizeOf(target.Level) / 2.0);
        view.Update(eyeX, eyeZ, Voxel, Release, 0.0);

        Assert.True(view.Missing > 0);
        Assert.True(view.FallbackCount > 0, "a built ancestor stands in for the leaves that are not built");
        Assert.All(view.Draw.Take(view.FallbackCount), t => Assert.True(t.OnCard));
        AssertNoOverlap(view);

        HangGuard.Settled(limit => view.Settle(eyeX, eyeZ, Voxel, Release, limit));
        Assert.True(view.PendingTiles.Count > 0, "the leaves are built but not on the card: the stand-in still covers them");
        Assert.True(view.FallbackCount > 0);
        AssertNoOverlap(view);
        Land(view, eyeX, eyeZ);
        Assert.Equal(0, view.FallbackCount);
        AssertNoOverlap(view);
    }
}
