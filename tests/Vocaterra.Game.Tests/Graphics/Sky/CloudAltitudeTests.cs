using Vocaterra.Game.Core.Atmosphere;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Graphics.Sky;

namespace Vocaterra.Game.Tests.Graphics.Sky;

/// <summary>The clouds' height is data (B14, <c>cloudAltitude</c> of <c>game/weather.json</c>): the value the sky is generated at is the value the file holds.</summary>
public sealed class CloudAltitudeTests
{
    private static WeatherData Shipped => WeatherData.Parse(File.ReadAllText(Repo.Path("content", "game-v1", "game", "weather.json")));

    private static (float Low, float High) Range(float altitude)
    {
        var boxes = new List<CloudBox>();
        CloudField.Generate(200f, 200f, 0, boxes, 0.9, altitude);
        Assert.NotEmpty(boxes);
        return (boxes.Min(static b => b.Y), boxes.Max(static b => b.Y));
    }

    [Fact]
    public void Given_TheShippedData_When_TheCloudsAreDrawn_Then_TheyFloatAtTheAltitudeTheFileHolds()
    {
        var data = Shipped;
        var look = SkyLook.At(SkyLookInput.At(new AtmosphereSnapshot(12, DayPhase.Day, 1, WeatherChannels.Dry(0.9, 1, 0, 0.2), string.Empty, null), data.CloudAltitude));

        Assert.Equal(168f, look.CloudAltitude); // the value the file holds
        Assert.Equal((float)data.CloudAltitude, look.CloudAltitude);
        var (low, high) = Range(look.CloudAltitude);
        Assert.InRange(low, data.CloudAltitude - 1, data.CloudAltitude + 16);
        Assert.InRange(high, data.CloudAltitude - 1, data.CloudAltitude + 16);
    }

    [Theory]
    [InlineData(120f)]
    [InlineData(300f)]
    [InlineData(640f)]
    public void Given_AnotherAltitude_When_TheLookIsMade_Then_TheCloudsAreDrawnThere(float altitude)
    {
        var look = SkyLook.At(SkyLookInput.At(new AtmosphereSnapshot(12, DayPhase.Day, 1, WeatherChannels.Dry(0.9, 1, 0, 0.2), string.Empty, null), altitude));

        Assert.Equal(altitude, look.CloudAltitude);
        var (low, high) = Range(look.CloudAltitude);
        Assert.InRange(low, altitude - 1, altitude + 16);
        Assert.InRange(high, altitude - 1, altitude + 16);
    }

    [Fact]
    public void Given_NoAltitude_When_TheFieldIsGenerated_Then_ItIsTheDefaultOne()
    {
        var a = new List<CloudBox>();
        var b = new List<CloudBox>();
        CloudField.Generate(200f, 200f, 0, a, 0.9);
        CloudField.Generate(200f, 200f, 0, b, 0.9, (float)WeatherData.DefaultCloudAltitude);

        Assert.Equal(a, b);
        Assert.Equal(CloudField.Altitude, SkyLook.Noon.CloudAltitude);
    }
}
