using Vocaterra.Game.Core.Menu;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.Streaming;

namespace Vocaterra.Game.Tests.Streaming;

/// <summary>The streamed world's host (B07): none for the bounded valley, and radii that follow the view distance.</summary>
public sealed class StreamedWorldHostTests : StreamedWorldHostTestBase
{
    [Fact]
    public void Given_TheBoundedValleyAndAStreamedWorld_When_AHostIsAsked_Then_OnlyTheStreamedWorldHasOne()
    {
        var bounded = WorldFactory.Create(TestGame.Data, 42, WorldMode.Bounded);
        var streamed = StreamedWorlds.Scene();
        var preset = QualityPreset.For(QualityLevel.Low);

        Assert.Null(StreamedWorldHost.For(bounded, preset, 0));
        using var host = StreamedWorldHost.For(streamed, preset, 1);
        Assert.NotNull(host);
        Assert.Equal(1, host.Pool.Workers);
        Assert.Equal(preset.FarRange, host.Far.Settings.Range);
    }


    [Theory]
    [InlineData(224)]
    [InlineData(304)]
    [InlineData(400)]
    [InlineData(576)]
    public void Given_AViewDistance_When_ThePolicyIsAsked_Then_TheLoadReachesTheViewWithTheNeighboursTheMeshesNeedAndTheKeepAddsSlack(int viewDistance)
    {
        var policy = StreamedWorldHost.PolicyFor(viewDistance);

        Assert.True(policy.LoadRadius * 16 >= viewDistance + (2 * 16), "a chunk at the edge of the view has its neighbours, a chunk and the diagonal beyond it");
        Assert.True(policy.KeepRadius > policy.LoadRadius);
        Assert.True(policy.NearRadius < policy.UndergroundNearRadius);
    }
}
