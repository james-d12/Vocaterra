namespace Vocaterra.Game.Core.Tests.TestSupport;

/// <summary>The built-scene cache keeps only the default seed and the last few others, so the seeds the tests ask for do not pile up on the heap (T10).</summary>
public sealed class RouteSceneCacheTests
{
    [Fact]
    public void Given_ManySeeds_When_ScenesAreAskedFor_Then_OnlyTheLastFewOtherSeedsStayCached()
    {
        for (ulong seed = 1; seed <= 6; seed++)
        {
            _ = RouteScene.Clearing(seed);
        }

        Assert.True(RouteScene.CachedOtherSeeds <= 2, $"{RouteScene.CachedOtherSeeds} scenes of other seeds are cached.");
    }

    [Fact]
    public void Given_TheSameSeedTwice_When_ScenesAreAskedFor_Then_TheyAreSeparateCopies()
    {
        var first = RouteScene.Clearing(3);
        var second = RouteScene.Clearing(3);

        Assert.NotSame(first, second);
        Assert.NotSame(first.Grid, second.Grid);
    }
}
