using Vocaterra.Game.Core.Light;
using Vocaterra.Game.Core.Stats;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Light;

/// <summary>The light round the player (I05b): the stat's contributors, the radius and strength from the stat and the data, off by day, on at night and underground, nothing worn meaning nothing.</summary>
public sealed class PlayerLightTests
{
    private static readonly PlayerLightData Data = new(2.0, 0.5, 0.06, 0.9, (1.0, 0.8, 0.4), 0.4, 0.8);

    [Fact]
    public void Given_NothingWorn_When_TheSheetIsRead_Then_TheLightIsZeroAndTheLightIsOff()
    {
        var sim = GearItems.Sim();

        Assert.Equal(0, StatRules.Read(sim.Stats, StatKeys.Light));
        Assert.Equal(PlayerLight.Off, PlayerLight.Of(Data, StatRules.Read(sim.Stats, StatKeys.Light), 0));
        Assert.False(PlayerLight.Off.IsOn);
    }

    [Fact]
    public void Given_AnOffHandAndAnAccessoryWithLight_When_TheyAreWorn_Then_TheSheetSumsThem()
    {
        var sim = GearItems.Sim();

        _ = sim.Equip(GearItems.Bag(sim, GearItems.Lantern), GearItems.Slot("offhand", "offhand"));
        Assert.Equal(12, sim.Stats.Get(StatKeys.Light));
        _ = sim.Equip(GearItems.Bag(sim, GearItems.Amulet, 13), GearItems.Nth("accessories", 0));

        Assert.Equal(15, sim.Stats.Get(StatKeys.Light));
    }

    [Fact]
    public void Given_ALightItemTakenOff_When_TheSheetIsRead_Then_ItsLightIsGone()
    {
        var sim = GearItems.Sim();
        var slot = GearItems.Slot("offhand", "offhand");
        _ = sim.Equip(GearItems.Bag(sim, GearItems.Lantern), slot);

        _ = sim.Unequip(slot);

        Assert.Equal(0, sim.Stats.Get(StatKeys.Light));
    }

    [Fact]
    public void Given_TheLightRule_When_ItIsRead_Then_ItIsASumOfZeroToSixteenPerItem()
    {
        var rule = StatRules.Find(StatKeys.Light)!;

        Assert.Equal(StatCombine.Sum, rule.Combine);
        Assert.Null(rule.Problem(16));
        Assert.NotNull(rule.Problem(16.5));
        Assert.NotNull(rule.Problem(-1));
    }

    [Theory]
    [InlineData(3, 3.5, 0.18)]
    [InlineData(12, 8.0, 0.72)]
    [InlineData(16, 10.0, 0.9)] // 0.96 capped at the data's maximum
    public void Given_AStatAtFullDark_When_TheLightIsMade_Then_RadiusAndStrengthComeFromTheData(double stat, double radius, double strength)
    {
        var light = PlayerLight.Of(Data, stat, 0.3);

        Assert.True(light.IsOn);
        Assert.Equal(radius, light.Radius, 4);
        Assert.Equal(strength, light.Strength, 4);
        Assert.Equal((1f, 0.8f, 0.4f), light.Colour);
    }

    [Fact]
    public void Given_MoreLightThanTheTop_When_TheLightIsMade_Then_TheStatIsClampedToSixteen()
    {
        Assert.Equal(PlayerLight.Of(Data, 16, 0), PlayerLight.Of(Data, 60, 0));
    }

    [Theory]
    [InlineData(1.0)] // the day in the open
    [InlineData(0.8)] // the edge of the day
    public void Given_Daylight_When_TheLightIsMade_Then_ItIsOff(double daylight) => Assert.Equal(PlayerLight.Off, PlayerLight.Of(Data, 12, daylight));

    [Theory]
    [InlineData(0.36)] // a night in the open
    [InlineData(0.28)] // deep in a cave
    [InlineData(0.0)]
    public void Given_NightOrACave_When_TheLightIsMade_Then_ItIsOnAtFullStrength(double daylight)
    {
        var light = PlayerLight.Of(Data, 12, daylight);

        Assert.True(light.IsOn);
        Assert.Equal(0.72, light.Strength, 4);
    }

    [Fact]
    public void Given_ADuskBetweenTheThresholds_When_TheLightIsMade_Then_ItFadesInAStraightLine()
    {
        var half = PlayerLight.Of(Data, 12, 0.6); // halfway between 0.4 and 0.8

        Assert.Equal(0.36, half.Strength, 4);
        Assert.Equal(8.0, half.Radius, 4); // the reach does not shrink with the dark: the strength carries the fade
    }

    [Fact]
    public void Given_NoLightItemsButADarkPlace_When_TheLightIsMade_Then_ItIsOff()
    {
        Assert.Equal(PlayerLight.Off, PlayerLight.Of(Data, 0, 0));
        Assert.Equal(PlayerLight.Off, PlayerLight.Of(Data, double.NaN, 0));
    }

    [Fact]
    public void Given_TheShippedFile_When_ItIsLoaded_Then_ItHasNoProblemsAndATorchLikeReach()
    {
        var data = TestGame.Data.Light;

        Assert.Empty(data.Problems());
        Assert.InRange(PlayerLight.Of(data, 16, 0).Radius, 6f, 16f);
    }

    [Fact]
    public void Given_BadNumbers_When_TheyAreChecked_Then_EachIsNamed()
    {
        var bad = Data with { MaxStrength = 1.5, DarkAtDaylight = 0.9, OffAtDaylight = 0.5 };

        var problems = bad.Problems();

        Assert.Contains(problems, p => p.Contains("maxStrength", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Contains("darkAtDaylight", StringComparison.Ordinal) && p.Contains("below", StringComparison.Ordinal));
    }

    [Fact]
    public void Given_TheShippedFile_When_ItIsValidatedAgainstItsSchema_Then_ItHasNoErrors() =>
        Assert.Empty(Schema().Validate(File.ReadAllText(Repo.Path("content", "game-v1", "game", "light.json"))));

    [Fact]
    public void Given_AFileWithAnUnknownKey_When_ItIsValidatedAgainstItsSchema_Then_ItIsRefused() =>
        Assert.NotEmpty(Schema().Validate(File.ReadAllText(Repo.Path("content", "game-v1", "game", "light.json")).Replace("\"radiusBase\"", "\"flicker\": 1, \"radiusBase\"", StringComparison.Ordinal)));

    private static NJsonSchema.JsonSchema Schema() => NJsonSchema.JsonSchema.FromFileAsync(Repo.Path("content", "schema", "light.schema.json")).GetAwaiter().GetResult();
}
