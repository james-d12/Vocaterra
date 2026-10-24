using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Session;

namespace Vocaterra.Game.Core.Tests;

/// <summary>The camp checklist as plain values: its steps, its line, its completions and the host event for the water.</summary>
public sealed class ObjectiveTests
{
    [Fact]
    public void TheCampChecklistStartsWithNothingDone()
    {
        var objective = Objective.Camp();

        Assert.Equal("Campamento: madera 0/5 · frutas 0/3 · antorcha 0/1 · agua 0/1", objective.Summary);
        Assert.Equal(new[] { "madera", "frutas", "antorcha", "agua" }, objective.Steps.Select(static s => s.Label).ToArray());
        Assert.False(objective.Complete);
    }

    [Fact]
    public void WoodAndBerriesCountThePickupsAndCompleteOnce()
    {
        var objective = Objective.Camp();

        Assert.Empty(objective.RecordPickup(ItemId.Wood, 4));
        var completed = objective.RecordPickup(ItemId.Wood, 1);
        Assert.Single(completed);
        Assert.Equal("madera", completed[0].Label);
        Assert.Empty(objective.RecordPickup(ItemId.Wood, 2));

        // The step shows its target at most, and passing it does not complete it again.
        Assert.Equal("madera 5/5", objective.Step(Objective.WoodKey)!.Text);
        Assert.Equal("Campamento: madera 5/5 · frutas 0/3 · antorcha 0/1 · agua 0/1", objective.Summary);
        Assert.Empty(objective.RecordPickup(ItemId.Berry, 2));
        Assert.Single(objective.RecordPickup(ItemId.Berry, 1));
    }

    [Fact]
    public void AnItemThatIsNoStepDoesNotMoveTheChecklist()
    {
        var objective = Objective.Camp();

        Assert.Empty(objective.RecordPickup(ItemId.Torch, 1));
        Assert.Empty(objective.RecordPickup(ItemId.Berry, 0));
        Assert.Equal("Campamento: madera 0/5 · frutas 0/3 · antorcha 0/1 · agua 0/1", objective.Summary);
    }

    [Fact]
    public void TheTorchCountsOnlyAtTheFireRing()
    {
        var objective = Objective.Camp();

        Assert.Empty(objective.RecordTorchPlaced(atFireRing: false));
        Assert.Equal(0, objective.Step(Objective.TorchKey)!.Count);

        Assert.Single(objective.RecordTorchPlaced(atFireRing: true));
        Assert.Equal("antorcha 1/1", objective.Step(Objective.TorchKey)!.Text);
    }

    [Fact]
    public void TheWaterStepTakesItsHostEventAndNothingElse()
    {
        var objective = Objective.Camp();

        Assert.Empty(objective.RecordHostEvent("shopkeeper.give_water"));
        Assert.Equal(0, objective.Step(Objective.WaterKey)!.Count);

        var completed = objective.RecordHostEvent(Objective.WaterEvent);
        Assert.Single(completed);
        Assert.Equal("agua", completed[0].Label);
        Assert.Equal("agua 1/1", objective.Step(Objective.WaterKey)!.Text);
    }

    [Fact]
    public void TheChecklistIsCompleteWhenEveryStepIs()
    {
        var objective = Objective.Camp();
        objective.RecordPickup(ItemId.Wood, 5);
        objective.RecordPickup(ItemId.Berry, 3);
        objective.RecordTorchPlaced(atFireRing: true);

        Assert.False(objective.Complete);
        Assert.Equal("Campamento: madera 5/5 · frutas 3/3 · antorcha 1/1 · agua 0/1", objective.Summary);

        objective.RecordHostEvent(Objective.WaterEvent);

        Assert.True(objective.Complete);
        Assert.Equal("Campamento: madera 5/5 · frutas 3/3 · antorcha 1/1 · agua 1/1", objective.Summary);
    }
}
