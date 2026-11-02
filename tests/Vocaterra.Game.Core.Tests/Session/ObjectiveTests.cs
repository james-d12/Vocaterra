using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Session;

/// <summary>The camp checklist as plain values (<c>game/objectives/camp.json</c>, C07): its steps, its line, and the world events its triggers count.</summary>
public sealed class ObjectiveTests
{
    [Fact]
    public void TheCampChecklistStartsWithNothingDone()
    {
        var objective = TestGame.Camp();

        Assert.Equal("Campamento: madera 0/5 · frutas 0/3 · antorcha 0/1 · agua 0/1", objective.Summary);
        Assert.Equal(new[] { "madera", "frutas", "antorcha", "agua" }, objective.Steps.Select(static s => s.Label).ToArray());
        Assert.False(objective.Complete);
    }

    [Fact]
    public void WoodAndBerriesCountThePickupsAndCompleteOnce()
    {
        var objective = TestGame.Camp();

        Assert.Empty(objective.Record(WorldEvents.ItemPickedUp, "madera", 4));
        var completed = objective.Record(WorldEvents.ItemPickedUp, "madera", 1);
        Assert.Single(completed);
        Assert.Equal("madera", completed[0].Label);
        Assert.Empty(objective.Record(WorldEvents.ItemPickedUp, "madera", 2));

        // The step shows its target at most, and passing it does not complete it again.
        Assert.Equal("madera 5/5", objective.Step("wood")!.Text);
        Assert.Equal("Campamento: madera 5/5 · frutas 0/3 · antorcha 0/1 · agua 0/1", objective.Summary);
        Assert.Empty(objective.Record(WorldEvents.ItemPickedUp, "fruta", 2));
        Assert.Single(objective.Record(WorldEvents.ItemPickedUp, "fruta", 1));
    }

    [Fact]
    public void AnItemThatIsNoStepOrAPickupOfNoneDoesNotMoveTheChecklist()
    {
        var objective = TestGame.Camp();

        Assert.Empty(objective.Record(WorldEvents.ItemPickedUp, "antorcha", 1));
        Assert.Empty(objective.Record(WorldEvents.ItemPickedUp, "fruta", 0));
        Assert.Equal("Campamento: madera 0/5 · frutas 0/3 · antorcha 0/1 · agua 0/1", objective.Summary);
    }

    [Fact]
    public void TheTorchCountsOnlyAtTheFireRing()
    {
        var objective = TestGame.Camp();

        Assert.Empty(objective.Record(WorldEvents.TorchAtRing, "firering.player"));
        Assert.Equal(0, objective.Step("torch")!.Count);

        Assert.Single(objective.Record(WorldEvents.TorchAtRing, "firering"));
        Assert.Equal("antorcha 1/1", objective.Step("torch")!.Text);
    }

    [Fact]
    public void TheWaterStepTakesTheFilledBottleAndNothingElse()
    {
        var objective = TestGame.Camp();

        Assert.Empty(objective.Record(WorldEvents.ItemGiven, "botella"));
        Assert.Equal(0, objective.Step("water")!.Count);

        var completed = objective.Record(WorldEvents.BottleFilled);
        Assert.Single(completed);
        Assert.Equal("agua", completed[0].Label);
        Assert.Equal("agua 1/1", objective.Step("water")!.Text);
    }

    [Fact]
    public void TheChecklistIsCompleteWhenEveryStepIs()
    {
        var objective = TestGame.Camp();
        objective.Record(WorldEvents.ItemPickedUp, "madera", 5);
        objective.Record(WorldEvents.ItemPickedUp, "fruta", 3);
        objective.Record(WorldEvents.TorchAtRing, "firering");

        Assert.False(objective.Complete);
        Assert.Equal("Campamento: madera 5/5 · frutas 3/3 · antorcha 1/1 · agua 0/1", objective.Summary);

        objective.Record(WorldEvents.BottleFilled);

        Assert.True(objective.Complete);
        Assert.Equal("Campamento: madera 5/5 · frutas 3/3 · antorcha 1/1 · agua 1/1", objective.Summary);
    }
}
