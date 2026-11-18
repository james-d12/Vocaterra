using Vocaterra.Game.Core.Atmosphere;
using Vocaterra.Game.Core.Temperature;
using Vocaterra.Game.UI.Hud;

namespace Vocaterra.Game.Tests.UI.Hud;

/// <summary>The clock and weather line of the 3D debug overlay (E03): its text in four states, running, held, held weather and a change of kind.</summary>
public sealed class VoxelDebugTextTests
{
    private static AtmosphereSnapshot Sky(double hour, DayPhase phase, string kind) => new(hour, phase, 1, default, kind, null);

    [Fact]
    public void Given_ARunningClockAndSteadyWeather_When_Written_Then_TheHourPhaseAndKindAreShownWithoutHeld()
    {
        var line = VoxelDebugText.Atmosphere(Sky(13, DayPhase.Day, "second"), "second", 1, clockHeld: false, weatherHeld: false);

        Assert.Equal("time 13:00 day   weather second", line);
    }

    [Fact]
    public void Given_AHeldClock_When_Written_Then_TheHourIsAsHhMmAndTheClockIsHeld()
    {
        var line = VoxelDebugText.Atmosphere(Sky(21.5, DayPhase.Dusk, "first"), "first", 1, clockHeld: true, weatherHeld: false);

        Assert.Equal("time 21:30 dusk held   weather first", line);
        Assert.Equal("time 00:00 night   weather first", VoxelDebugText.Atmosphere(Sky(23.999, DayPhase.Night, "first"), "first", 1, clockHeld: false, weatherHeld: false));
    }

    [Fact]
    public void Given_AHeldWeather_When_Written_Then_TheKindIsHeld()
    {
        var line = VoxelDebugText.Atmosphere(Sky(7.5, DayPhase.Dawn, "first"), "first", 1, clockHeld: false, weatherHeld: true);

        Assert.Equal("time 07:30 dawn   weather first held", line);
    }

    [Fact]
    public void Given_AChangeOfKind_When_Written_Then_TheKindItLeavesAndTheBlendInPercentAreShown()
    {
        var line = VoxelDebugText.Atmosphere(Sky(9, DayPhase.Day, "second"), "first", 0.4, clockHeld: false, weatherHeld: false);

        Assert.Equal("time 09:00 day   weather second from first 40%", line);
    }

    [Fact]
    public void Given_ATemperatureReading_When_Written_Then_TheLineShowsPlaceWarmthFeelingAndBand()
    {
        var cool = new TemperatureBand("cool", 30, 1, 0, null);

        Assert.Equal("temp 12.5C   warmth 0.0   feels 12.5C cool", VoxelDebugText.Temperature(new TemperatureReading(125, 0, cool, Held: false)));
        Assert.Equal("temp -5.0C held   warmth 3.0   feels -2.0C cool", VoxelDebugText.Temperature(new TemperatureReading(-50, 30, cool, Held: true)));
        Assert.Equal("temp -5.8C   warmth 0.0   feels -5.8C cool   climate -12.0", VoxelDebugText.Temperature(new TemperatureReading(-58, 0, cool, Held: false, ClimateTenths: -120)));
    }
}
