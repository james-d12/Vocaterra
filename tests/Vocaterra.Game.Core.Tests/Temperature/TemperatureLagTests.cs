using Vocaterra.Game.Core.Temperature;

namespace Vocaterra.Game.Core.Tests.Temperature;

/// <summary>The lag (I06c): what the player feels moves towards the place's value by a share of the gap each tick, never overshoots, is the same on every run, and is bypassed by the first reading, a held value and a lag of 0.</summary>
public sealed class TemperatureLagTests
{
    private static readonly TemperatureContext Noon = TemperatureRig.Open(TemperatureRig.Sky(13, "sunny"));
    private static readonly TemperatureContext Night = TemperatureRig.Open(TemperatureRig.Sky(0, "rain"));

    private static PlayerTemperature Player(int seconds)
    {
        var data = TemperatureRig.Data with { Lag = new TemperatureLag(seconds) };
        return new PlayerTemperature(data, PlaceTemperature.From(data), NoWarmth.Instance);
    }

    [Fact]
    public void Given_TheFirstReading_When_ItIsTaken_Then_ItIsThePlacesValueAtOnce()
    {
        var player = Player(15);

        var reading = player.Update(Noon);

        Assert.Equal(reading.TargetTenths, reading.PlaceTenths);
    }

    [Fact]
    public void Given_ThePlaceTurnsColder_When_TicksPass_Then_TheFeltValueFallsStepByStepToTheNewValueWithoutOvershooting()
    {
        var player = Player(15);
        var warm = player.Update(Noon).PlaceTenths;
        var target = player.Update(Night).TargetTenths!.Value;
        Assert.True(target < warm - 50);

        var previous = player.Current!.Value.PlaceTenths;
        Assert.True(previous > target, "the first tick after the change has not arrived");
        for (var tick = 0; tick < 15 * 60 * 12; tick++)
        {
            var felt = player.Update(Night).PlaceTenths;
            Assert.InRange(felt, target, previous);
            previous = felt;
        }

        Assert.InRange(previous, target, target + 1);
    }

    [Fact]
    public void Given_AboutOneTimeConstantOfTicks_When_ThePlaceHasChanged_Then_TheFeltValueHasCoveredAboutTwoThirdsOfTheGap()
    {
        var player = Player(15);
        var start = player.Update(Noon).PlaceTenths;
        var target = player.Update(Night).TargetTenths!.Value;

        for (var tick = 0; tick < (15 * Vocaterra.Game.Core.Needs.NeedsData.TicksPerSecond) - 1; tick++)
        {
            _ = player.Update(Night);
        }

        var covered = (start - player.Current!.Value.PlaceTenths) / (double)(start - target);
        Assert.InRange(covered, 0.58, 0.68);
    }

    [Fact]
    public void Given_TwoPlayersGivenTheSameTicks_When_TheyAreRead_Then_TheirFeltValuesAreTheSameEveryTick()
    {
        var one = Player(15);
        var two = Player(15);
        _ = one.Update(Noon);
        _ = two.Update(Noon);

        for (var tick = 0; tick < 2000; tick++)
        {
            var place = tick % 700 < 350 ? Night : Noon;
            Assert.Equal(one.Update(place).PlaceTenths, two.Update(place).PlaceTenths);
        }
    }

    [Fact]
    public void Given_ALagOfZero_When_ThePlaceChanges_Then_TheFeltValueIsTheNewValueAtOnce()
    {
        var player = Player(0);
        _ = player.Update(Noon);

        var reading = player.Update(Night);

        Assert.Equal(reading.TargetTenths, reading.PlaceTenths);
    }

    [Fact]
    public void Given_AHeldValue_When_ItIsSetAndReleased_Then_TheFeltValueFollowsItAtOnceBothWays()
    {
        var player = Player(15);
        _ = player.Update(Noon);

        player.HeldTenths = -200;
        Assert.Equal(-200, player.Update(Noon).PlaceTenths);

        player.HeldTenths = null;
        var released = player.Update(Noon);
        Assert.Equal(released.TargetTenths, released.PlaceTenths);
    }

    [Fact]
    public void Given_TheWarmthChanges_When_TheFeelingIsRead_Then_ItChangesAtOnceForTheLagIsOnTheSurroundingsOnly()
    {
        var warmth = new Mutable();
        var data = TemperatureRig.Data with { Lag = new TemperatureLag(15) };
        var player = new PlayerTemperature(data, PlaceTemperature.From(data), warmth);
        var bare = player.Update(Noon).FeelingTenths;

        warmth.Tenths = 40;

        Assert.Equal(bare + 40, player.Update(Noon).FeelingTenths);
    }

    [Fact]
    public void Given_TheShippedLag_When_ItIsRead_Then_ItIsASmallShareOfTheGapEachTick() =>
        Assert.InRange(TemperatureRig.Data.Lag.StepPerMillion, 100, 5000);

    private sealed class Mutable : IWarmthSource
    {
        public int Tenths { get; set; }

        public int WarmthTenths() => Tenths;
    }
}
