using Vocaterra.Game.Core.Atmosphere;

namespace Vocaterra.Game.Core.Tests.Atmosphere;

/// <summary>The snapshot the consumers read (E04): the clock's hour, phase and daylight, the blended channels, the kind's id and its say key.</summary>
public sealed class AtmosphereModelTests
{
    private const int Second = DayClock.TicksPerSecond;

    private static WeatherData WithSay() => WeatherTestData.Shipped with
    {
        Kinds = WeatherTestData.Shipped.Kinds.Select(static k => k with { Say = new WeatherSay(k.Id + ".day", k.Id + ".night") }).ToList(),
    };

    [Fact]
    public void TheSnapshotCarriesTheClocksValuesAndTheCurrentKind()
    {
        var clock = new DayClock(DayData.Default);
        var weather = new WeatherSystem(WeatherTestData.Shipped, 42);

        var snapshot = AtmosphereModel.At(clock, weather, 600);

        Assert.Equal(clock.HourAt(600), snapshot.Hour);
        Assert.Equal(clock.PhaseAt(600), snapshot.Phase);
        Assert.Equal(clock.DaylightAt(600), snapshot.Daylight);
        Assert.Equal("sunny", snapshot.KindId);
        Assert.Equal(weather.Current.Channels, snapshot.Channels);
        Assert.Null(snapshot.Say);
    }

    [Fact]
    public void InTheMiddleOfATransitionTheChannelsAreHalfwayBetweenTheTwoKinds()
    {
        var clock = new DayClock(DayData.Default);
        var weather = new WeatherSystem(WeatherTestData.Shipped, 42);
        var end = weather.State.SpellEnd;
        weather.Advance(end); // sunny to cloudy

        var snapshot = AtmosphereModel.At(clock, weather, end + (long)(22.5 * Second));

        Assert.Equal("cloudy", snapshot.KindId);
        Assert.Equal((0.42 + 0.9) / 2, snapshot.Channels.CloudCover, 9);
        Assert.Equal(0.85, snapshot.Channels.Sunlight, 9);
        Assert.Equal(0.125, snapshot.Channels.Fog, 9);
    }

    [Fact]
    public void TheSayKeyIsTheNightsWhileItIsNightAndTheDaysOtherwise()
    {
        var clock = new DayClock(DayData.Default);
        var weather = new WeatherSystem(WithSay(), 42);
        clock.Freeze(12);
        Assert.Equal("sunny.day", AtmosphereModel.At(clock, weather, 0).Say);
        clock.Freeze(5);
        Assert.Equal("sunny.day", AtmosphereModel.At(clock, weather, 0).Say); // dawn
        clock.Freeze(23);

        var night = AtmosphereModel.At(clock, weather, 0);

        Assert.Equal(DayPhase.Night, night.Phase);
        Assert.Equal("sunny.night", night.Say);
    }
}
