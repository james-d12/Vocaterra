using Microsoft.Xna.Framework;
using Vocaterra.Game.Core.Light;
using Vocaterra.Game.Graphics.Gl;
using Vocaterra.Game.Graphics.World;

namespace Vocaterra.Game.Tests.Graphics.World;

/// <summary>The renderer's side of the light round the player (I05b): the daylight at the eye from night and depth, the colour the shader adds, and no term at all when nothing is worn.</summary>
public sealed class PlayerLightGlowTests
{
    private static readonly PlayerLightData Data = new(2.0, 0.5, 0.06, 0.9, (1.0, 0.8, 0.4), 0.4, 0.8);

    [Theory]
    [InlineData(1f, 1f, 1.0)] // noon in the open
    [InlineData(0.36f, 1f, 0.36)] // a night in the open
    [InlineData(1f, 0.28f, 0.28)] // noon deep in a cave
    [InlineData(0.36f, 0.28f, 0.1008)] // a night in a cave
    public void Given_TheSunAndTheEyesSky_When_TheDaylightIsMade_Then_ItIsTheirProduct(float sun, float sky, double expected) =>
        Assert.Equal(expected, PlayerLightGlow.Daylight(sun, sky), 4);

    [Fact]
    public void Given_Noon_When_ALanternIsWorn_Then_TheLightIsOffAndTheColourIsBlack()
    {
        var light = PlayerLight.Of(Data, 12, PlayerLightGlow.Daylight(1f, 1f));

        Assert.False(light.IsOn);
        Assert.Equal((0f, 0f, 0f), PlayerLightGlow.Colour(light));
    }

    [Fact]
    public void Given_NightInTheOpen_When_ALanternIsWorn_Then_TheLightIsOn()
    {
        var light = PlayerLight.Of(Data, 12, PlayerLightGlow.Daylight(0.36f, 1f));

        Assert.True(light.IsOn);
        Assert.Equal(0.72f * 1f, PlayerLightGlow.Colour(light).R, 4);
        Assert.Equal(0.72f * 0.8f, PlayerLightGlow.Colour(light).G, 4);
    }

    [Fact]
    public void Given_NoDaylightAtAll_When_NothingIsWorn_Then_ThereIsNoLightAndNoColour()
    {
        var light = PlayerLight.Of(Data, 0, PlayerLightGlow.Daylight(0.36f, 0.28f));

        Assert.False(light.IsOn);
        Assert.Equal((0f, 0f, 0f), PlayerLightGlow.Colour(light));
    }

    [Fact]
    public void Given_ALightFrameMadeWithoutAPlayerLight_When_ItIsRead_Then_TheReachIsZeroSoTheShaderSkipsIt()
    {
        var frame = GlLightFrame.Noon(Vector3.Zero);

        Assert.Equal(0f, frame.PlayerLightRadius);
        Assert.Equal(Vector3.Zero, frame.PlayerLightColour);
    }
}
