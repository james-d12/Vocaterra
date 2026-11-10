using Vocaterra.Game.Graphics.Sky;
using Vocaterra.Game.Graphics.World;

namespace Vocaterra.Game.Tests.Graphics.World;

/// <summary>The fog's distances by the weather's fog channel (E05): today's at 0, nearer as it rises, about a third of the view distance at 1.</summary>
public sealed class FogReachTests
{
    [Theory]
    [InlineData(48)]
    [InlineData(224)]
    [InlineData(304)]
    [InlineData(400)]
    [InlineData(576)]
    public void Given_NoFog_When_TheFogEndIsAsked_Then_ItIsTodaysDistanceExactly(int viewDistance)
    {
        Assert.Equal(viewDistance * 0.94f, VoxelRenderer.FogEndFor(viewDistance));
        Assert.Equal(viewDistance * 0.94f, VoxelRenderer.FogEndFor(viewDistance, 0f));
    }

    [Fact]
    public void Given_ARisingFog_When_TheFogEndIsAsked_Then_ItOnlyComesNearerAndReachesAThirdOfTheViewDistance()
    {
        var last = float.MaxValue;
        for (var fog = 0f; fog <= 1.0001f; fog += 0.01f)
        {
            var end = VoxelRenderer.FogEndFor(400, fog);
            Assert.True(end < last);
            last = end;
        }

        Assert.InRange(VoxelRenderer.FogEndFor(400, 1f), 400 * 0.3f, 400 * 0.36f);
        Assert.Equal(VoxelRenderer.FogEndFor(400, 1f), VoxelRenderer.FogEndFor(400, 7f)); // clamped
        Assert.Equal(VoxelRenderer.FogEndFor(400), VoxelRenderer.FogEndFor(400, -1f));
    }

    [Fact]
    public void Given_TheFogEnd_When_TheStartIsTakenInProportion_Then_ItStaysBelowTheEndAtEveryFog()
    {
        for (var fog = 0f; fog <= 1f; fog += 0.1f)
        {
            var end = VoxelRenderer.FogEndFor(224, fog);
            Assert.True(end * VoxelRenderer.FogStartFraction < end);
        }
    }

    [Fact]
    public void Given_TheLookWithFog_When_ItIsRead_Then_TheFogChannelIsInIt()
    {
        var look = SkyLook.At(new SkyLookInput(13, 1, Vocaterra.Game.Core.Atmosphere.DayPhase.Day, 1, 0.5, 0.5));

        Assert.Equal(0.5f, look.Fog);
        Assert.Equal(1.0, look.CloudCover);
        Assert.Equal(2f / 3f, SkyWeather.FogReach(0.5), 5);
    }
}
