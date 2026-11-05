using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Needs;

/// <summary><c>game/needs.json</c> (N01): the shipped file holds together, and the validator refuses what the design forbids.</summary>
public sealed class NeedsDataTests
{
    private static NeedsData Needs => TestGame.Data.Needs;

    private static IReadOnlyList<string> Problems(NeedsData needs) => GameDataValidator.Validate(TestGame.Data.WithNeeds(needs));

    [Fact]
    public void TheShippedNeedsHoldTogether() => Assert.Empty(NeedsValidator.Validate(Needs));

    [Fact]
    public void TheShippedFoodsAreTheItemsThatExistToday()
    {
        Assert.Equal(["berry", "apple", "bread", "water-bottle"], Needs.Foods.Select(static f => f.Item).ToArray());
        Assert.Equal((8, 12, 25), (Needs.FoodOf("berry")!.Hunger, Needs.FoodOf("apple")!.Hunger, Needs.FoodOf("bread")!.Hunger));
        Assert.Equal(40, Needs.FoodOf("water-bottle")!.Thirst);
        Assert.Equal(10, Needs.DrinkByHandThirst);
    }

    [Fact]
    public void TheStartingValuesAreTheDesigns()
    {
        Assert.Equal([75, 50, 25, 0], Needs.Of(Drive.Hunger).Stages.Select(static s => s.Point).ToArray());
        Assert.Equal(60, Needs.CollapseDelaySeconds);
        Assert.Equal((3, 2, 2), (Needs.Rest.RadiusBlocks, (int)Needs.Rest.RefillFactor, (int)Needs.Rest.RecoveryFactor));
        Assert.Equal((20, 0.25), (Needs.Stamina.MinimumMaximum, Needs.Stamina.MinimumRefillFactor));
        Assert.Equal((100, 20, 15, 20, 10), (Needs.Night.SleepTo, Needs.Night.Health, Needs.Night.HungerCost, Needs.Night.ThirstCost, Needs.Night.Floor));
        Assert.Equal(("firering.player", 50), (Needs.Death.WakeAt, Needs.Death.Health));
    }

    [Fact]
    public void TheSprintAndTheSlowGatherAreTheDataAndTheValidatorRefusesWhatCannotWork()
    {
        Assert.Equal((1.6, 0.5), (Needs.Stamina.SprintSpeedFactor, Needs.Stamina.GatherSlowFactor));

        Assert.Contains(Problems(Needs with { Stamina = Needs.Stamina with { SprintSpeedFactor = 1 } }), static p => p.Contains("sprintSpeedFactor", StringComparison.Ordinal));
        Assert.Contains(Problems(Needs with { Stamina = Needs.Stamina with { GatherSlowFactor = 0 } }), static p => p.Contains("gatherSlowFactor", StringComparison.Ordinal)); // 0 would stop gathering
    }

    [Fact]
    public void AFoodThatIsNoItemIsRefused()
    {
        var broken = Needs with { Foods = [.. Needs.Foods, new NeedFood("moon-cheese", 5, 0, 0, null)] };
        Assert.Contains(Problems(broken), static p => p.Contains("'moon-cheese' is not an item", StringComparison.Ordinal));

        var leaves = Needs with { Foods = [new NeedFood("water-bottle", 0, 40, 0, "empty-moon")] };
        Assert.Contains(Problems(leaves), static p => p.Contains("empty-moon", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-36)]
    public void ANonPositiveRateIsRefused(double rate)
    {
        var drives = Needs.Drives.Select(d => d.Drive == Drive.Thirst ? d with { SecondsPerPoint = rate } : d).ToList();

        Assert.Contains(Problems(Needs with { Drives = drives }), static p => p.Contains("thirst's secondsPerPoint must be above 0", StringComparison.Ordinal));
    }

    [Fact]
    public void StagesOutOfOrderAreRefused()
    {
        var hunger = Needs.Of(Drive.Hunger);
        var swapped = hunger with { Stages = [hunger.Stages[1], hunger.Stages[0], hunger.Stages[2], hunger.Stages[3]] };
        var drives = Needs.Drives.Select(d => d.Drive == Drive.Hunger ? swapped : d).ToList();

        Assert.Contains(Problems(Needs with { Drives = drives }), static p => p.Contains("stages of hunger are out of order", StringComparison.Ordinal));
    }

    [Fact]
    public void ALastStageThatIsNotAtZeroAndAFactorOutOfRangeAreRefused()
    {
        var sleep = Needs.Of(Drive.Sleep);
        var raised = sleep with { Stages = [.. sleep.Stages.Take(3), sleep.Stages[3] with { Point = 5 }] };
        Assert.Contains(Problems(Needs with { Drives = [Needs.Drives[0], Needs.Drives[1], raised] }), static p => p.Contains("last stage of sleep must be at 0", StringComparison.Ordinal));

        var wild = sleep with { Stages = [sleep.Stages[0] with { Effects = new StageEffects(RefillFactor: 1.5) }, .. sleep.Stages.Skip(1)] };
        Assert.Contains(Problems(Needs with { Drives = [Needs.Drives[0], Needs.Drives[1], wild] }), static p => p.Contains("out of range", StringComparison.Ordinal));
    }

    [Fact]
    public void TheLoaderRefusesAFileMissingAPart()
    {
        var ex = Assert.Throws<InvalidDataException>(() => NeedsData.Parse("""{ "schemaVersion": 1, "drives": {} }"""));
        Assert.Contains("malformed", ex.Message, StringComparison.Ordinal);
    }
}
