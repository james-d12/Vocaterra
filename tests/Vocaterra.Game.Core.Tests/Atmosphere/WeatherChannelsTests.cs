using Vocaterra.Game.Core.Atmosphere;

namespace Vocaterra.Game.Core.Tests.Atmosphere;

/// <summary>The blend of two kinds' channels (E04).</summary>
public sealed class WeatherChannelsTests
{
    private static readonly WeatherChannels Sunny = WeatherChannels.Dry(0.4, 1, 0, 0.2);
    private static readonly WeatherChannels Rain = new(1, 0.5, 0.5, 0.6, "rain", 0.8);

    [Fact]
    public void TheEndsOfTheBlendAreExactlyTheTwoKinds()
    {
        Assert.Equal(Sunny, WeatherChannels.Blend(Sunny, Rain, 0));
        Assert.Equal(Rain, WeatherChannels.Blend(Sunny, Rain, 1));
        Assert.Equal(Rain, WeatherChannels.Blend(Sunny, Rain, 7));
        Assert.Equal(Sunny, WeatherChannels.Blend(Sunny, Rain, -3));
    }

    [Fact]
    public void TheMiddleOfTheBlendIsHalfwayAndTheRainIsAlreadyFalling()
    {
        var middle = WeatherChannels.Blend(Sunny, Rain, 0.5);

        Assert.Equal(0.7, middle.CloudCover, 9);
        Assert.Equal(0.75, middle.Sunlight, 9);
        Assert.Equal(0.25, middle.Fog, 9);
        Assert.Equal(0.4, middle.Wind, 9);
        Assert.Equal(0.4, middle.PrecipitationIntensity, 9);
        Assert.Equal("rain", middle.PrecipitationKind);
    }

    [Fact]
    public void AFadingRainKeepsItsKindUntilTheIntensityReachesZero()
    {
        var halfway = WeatherChannels.Blend(Rain, Sunny, 0.5);
        var almost = WeatherChannels.Blend(Rain, Sunny, 0.99);
        var done = WeatherChannels.Blend(Rain, Sunny, 1);

        Assert.Equal("rain", halfway.PrecipitationKind);
        Assert.Equal("rain", almost.PrecipitationKind);
        Assert.True(almost.PrecipitationIntensity > 0);
        Assert.Equal(string.Empty, done.PrecipitationKind);
        Assert.Equal(0, done.PrecipitationIntensity);
    }

    [Fact]
    public void TwoDryKindsBlendToNoPrecipitationAtAnyPoint()
    {
        var other = WeatherChannels.Dry(0.9, 0.7, 0.25, 0.4);

        Assert.Equal(string.Empty, WeatherChannels.Blend(Sunny, other, 0.3).PrecipitationKind);
        Assert.Equal(0, WeatherChannels.Blend(Sunny, other, 0.3).PrecipitationIntensity);
    }

    [Fact]
    public void ADifferentPrecipitationTakesTheKindBlendedTowards()
    {
        var drizzle = new WeatherChannels(0.6, 0.8, 0.9, 0.1, "drizzle", 0.2);

        Assert.Equal("drizzle", WeatherChannels.Blend(Rain, drizzle, 0.5).PrecipitationKind);
    }
}
