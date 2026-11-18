using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Graphics.Far;
using Vocaterra.Game.Streaming;
using Xunit.Abstractions;

namespace Vocaterra.Game.Tests.Graphics.Far;

/// <summary>Tests split out of FarViewTests (B28), so they run beside the others: ACapOnTheTiles.</summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class FarViewCapTests(ITestOutputHelper output) : FarViewTestsBase(output)
{
    [Fact]
    public void Given_ACapOnTheTiles_When_TheEyeWalksFar_Then_TheFarthestUnusedTilesAreDroppedAndFreed()
    {
        using var pool = new JobPool(0);
        var capped = new FarViewSettings(320, MaxTiles: 14, TreeLevels: 1);
        var view = Make(pool, capped);
        HangGuard.Settled(limit => view.Settle(192, 192, Voxel, Release, limit));
        Land(view, 192, 192);
        var first = view.Stats.Cached;

        HangGuard.Settled(limit => view.Settle(192 + 3000, 192, Voxel, Release, limit));
        Land(view, 192 + 3000, 192);

        Assert.True(first > 12);
        Assert.InRange(view.Stats.Cached, 1, Math.Max(14, view.Draw.Count));
        Assert.NotEmpty(_released);
    }
}
