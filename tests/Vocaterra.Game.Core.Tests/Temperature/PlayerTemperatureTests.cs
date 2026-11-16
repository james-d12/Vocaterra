using Vocaterra.Game.Core.Temperature;

namespace Vocaterra.Game.Core.Tests.Temperature;

/// <summary>The feeling (the place plus the warmth), its band, the warmth seam and the line said once on entering the cold (I06).</summary>
public sealed class PlayerTemperatureTests
{
    private static readonly TemperatureSituation Open = TemperatureRig.Open(TemperatureRig.Sky(13, "sunny"));

    private static PlayerTemperature Fresh(IWarmthSource? warmth = null) => new(TemperatureRig.Data, TemperatureRig.Place, warmth ?? NoWarmth.Instance);

    private static PlayerTemperature Held(int tenths)
    {
        var temperature = Fresh();
        temperature.HeldTenths = tenths;
        return temperature;
    }

    [Fact]
    public void WithNoWarmthTheFeelingIsThePlacesTemperature()
    {
        var reading = Fresh().Update(Open);

        Assert.Equal(0, reading.WarmthTenths);
        Assert.Equal(reading.PlaceTenths, reading.FeelingTenths);
        Assert.Equal("warm", reading.Band.Id);
        Assert.False(reading.Held);
    }

    [Fact]
    public void AWarmthSourceIsAddedToTheFeelingAndCanLiftABand()
    {
        var temperature = Held(-100);
        Assert.Equal("very-cold", temperature.Update(Open).Band.Id);

        temperature.Warmth = new FixedWarmth(80);
        var reading = temperature.Update(Open);

        Assert.Equal((-100, 80, -20), (reading.PlaceTenths, reading.WarmthTenths, reading.FeelingTenths));
        Assert.Equal("cold", reading.Band.Id);
        temperature.Warmth = new FixedWarmth(200);
        Assert.Equal("cool", temperature.Update(Open).Band.Id);
    }

    [Fact]
    public void AHeldTemperatureReplacesThePlacesAndSaysSo()
    {
        var temperature = Held(55);
        var reading = temperature.Update(Open);

        Assert.Equal(55, reading.PlaceTenths);
        Assert.True(reading.Held);
        Assert.Equal("cool", reading.Band.Id);

        temperature.HeldTenths = null;
        Assert.Equal(TemperatureRig.Place.TenthsAt(Open), temperature.Update(Open).PlaceTenths);
    }

    [Fact]
    public void TheLineIsSaidOnceOnEnteringTheColdAndNotWhileStayingOrDeepening()
    {
        var temperature = Held(100);
        _ = temperature.Update(Open);
        Assert.Null(temperature.TakeLine());

        temperature.HeldTenths = -20; // cold
        _ = temperature.Update(Open);
        Assert.Equal("temperature.cold", temperature.TakeLine());
        Assert.Null(temperature.TakeLine()); // taken

        _ = temperature.Update(Open);
        temperature.HeldTenths = -80; // very cold: still the same cold
        _ = temperature.Update(Open);
        Assert.Null(temperature.TakeLine());
    }

    [Fact]
    public void TheLineIsReArmedWhenTheFeelingRisesToCoolOrWarm()
    {
        var temperature = Held(-20);
        _ = temperature.Update(Open);
        Assert.NotNull(temperature.TakeLine());

        temperature.HeldTenths = 60; // cool
        _ = temperature.Update(Open);
        Assert.Null(temperature.TakeLine());
        temperature.HeldTenths = -20;
        _ = temperature.Update(Open);
        Assert.NotNull(temperature.TakeLine());

        temperature.HeldTenths = 200; // warm
        _ = temperature.Update(Open);
        temperature.HeldTenths = -80; // straight into very cold
        _ = temperature.Update(Open);
        Assert.NotNull(temperature.TakeLine());
    }

    [Fact]
    public void AStartInTheColdSaysItOnceAndAWarmthThatLiftsTheFeelingOutReArms()
    {
        var temperature = Held(-20);
        _ = temperature.Update(Open);
        Assert.NotNull(temperature.TakeLine()); // a load that starts cold says it again: fine

        temperature.Warmth = new FixedWarmth(100); // dressed: cool
        _ = temperature.Update(Open);
        temperature.Warmth = NoWarmth.Instance; // undressed: cold again
        _ = temperature.Update(Open);
        Assert.NotNull(temperature.TakeLine());
    }

    [Fact]
    public void NoWarmthIsZero() => Assert.Equal(0, NoWarmth.Instance.WarmthTenths());

    private sealed class FixedWarmth(int tenths) : IWarmthSource
    {
        public int WarmthTenths() => tenths;
    }
}
