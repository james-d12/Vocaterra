using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Temperature;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Temperature;

/// <summary>Heat (I06c): bands above warm, each with its walk factor and thirst factor as data, the line «Tengo calor.» said once on the way into the heat, the thirst falling faster, and nothing of it with the needs off.</summary>
public sealed class HeatBandTests
{
    private static readonly TemperatureContext Open = TemperatureRig.Open(TemperatureRig.Sky(13, "sunny"));

    private static PlayerTemperature Held(int tenths)
    {
        var temperature = new PlayerTemperature(TemperatureRig.Data, TemperatureRig.Place, NoWarmth.Instance) { HeldTenths = tenths };
        return temperature;
    }

    [Theory]
    [InlineData(279, "warm", 1.0, 1.0)]
    [InlineData(280, "hot", 0.9, 1.5)]
    [InlineData(349, "hot", 0.9, 1.5)]
    [InlineData(350, "very-hot", 0.75, 2.0)]
    [InlineData(900, "very-hot", 0.75, 2.0)]
    public void Given_AFeeling_When_ItsBandIsRead_Then_TheWalkAndThirstFactorsAreTheBandsOwn(int feeling, string band, double walk, double thirst)
    {
        var found = TemperatureRig.Data.BandFor(feeling);

        Assert.Equal((band, walk, thirst), (found.Id, found.WalkFactor, found.ThirstFactor));
        Assert.Equal(0, found.Chill);
    }

    [Fact]
    public void Given_TheFeelingRisesIntoTheHeat_When_ItIsRead_Then_TheHotLineIsSaidOnceAndNotWhileStayingOrDeepening()
    {
        var temperature = Held(200);
        _ = temperature.Update(Open);
        Assert.Null(temperature.TakeLine());

        temperature.HeldTenths = 300;
        _ = temperature.Update(Open);
        Assert.Equal("temperature.hot", temperature.TakeLine());
        Assert.Null(temperature.TakeLine());

        temperature.HeldTenths = 400; // very hot: the same heat
        _ = temperature.Update(Open);
        Assert.Null(temperature.TakeLine());

        temperature.HeldTenths = 200;
        _ = temperature.Update(Open);
        temperature.HeldTenths = 300;
        _ = temperature.Update(Open);
        Assert.Equal("temperature.hot", temperature.TakeLine());
    }

    [Fact]
    public void Given_ColdThenHeat_When_TheFeelingGoesStraightFromOneToTheOther_Then_EachSaysItsOwnLine()
    {
        var temperature = Held(-20);
        _ = temperature.Update(Open);
        Assert.Equal("temperature.cold", temperature.TakeLine());

        temperature.HeldTenths = 300;
        _ = temperature.Update(Open);

        Assert.Equal("temperature.hot", temperature.TakeLine());
    }

    [Fact]
    public void Given_TheHeatLine_When_ItsKeyIsLooked_Then_ItIsAnA1LineOfTheWordsAndTheLocaleWithSpeech()
    {
        Assert.Equal("Tengo calor.", TestGame.Data.Words.HudLine("temperature.hot"));
        Assert.True(TestGame.Data.Locale.Has("temperature.hot"));
    }

    [Fact]
    public void Given_AHotBand_When_TheSessionSteps_Then_TheWalkIsSlowedAndTheThirstFallsByTheFactor()
    {
        var hot = TestGame.NewSession();
        var warm = TestGame.NewSession();
        hot.Temperature.HeldTenths = 300 - hot.Temperature.Warmth.WarmthTenths();
        warm.Temperature.HeldTenths = 200 - warm.Temperature.Warmth.WarmthTenths();

        for (var i = 0; i < 600; i++)
        {
            hot.Step([]);
            warm.Step([]);
        }

        Assert.Equal(900, hot.Simulation.WalkPermille);
        Assert.Equal(1000, warm.Simulation.WalkPermille);
        var hotLoss = PlayerNeeds.Full - hot.Needs.Value(NeedMeter.Thirst);
        var warmLoss = PlayerNeeds.Full - warm.Needs.Value(NeedMeter.Thirst);
        Assert.True(warmLoss > 0);
        Assert.InRange(hotLoss / warmLoss, 1.45, 1.55);
        Assert.Equal(1.5, hot.Needs.ThirstFactor);
        Assert.Equal(1.0, warm.Needs.ThirstFactor);
    }

    [Fact]
    public void Given_TheNeedsOff_When_TheFeelingIsVeryHot_Then_NothingIsSlowedOrThirstier()
    {
        var session = TestGame.NewSession();
        session.NeedsEnabled = false;
        session.Temperature.HeldTenths = 500 - session.Temperature.Warmth.WarmthTenths();

        session.Step([]);

        Assert.Equal(1000, session.Simulation.WalkPermille);
        Assert.Equal(1.0, session.Needs.ThirstFactor);
        Assert.Equal("very-hot", session.Temperature.Current!.Value.Band.Id);
    }

    [Fact]
    public void Given_HeatNeverHarms_When_TheSessionRunsVeryHot_Then_TheHealthIsAsInTheWarm()
    {
        var hot = TestGame.NewSession();
        var warm = TestGame.NewSession();
        hot.Temperature.HeldTenths = 500;
        warm.Temperature.HeldTenths = 200;

        for (var i = 0; i < 600; i++)
        {
            hot.Step([]);
            warm.Step([]);
        }

        Assert.Equal(warm.Needs.Value(NeedMeter.Health), hot.Needs.Value(NeedMeter.Health));
    }
}
