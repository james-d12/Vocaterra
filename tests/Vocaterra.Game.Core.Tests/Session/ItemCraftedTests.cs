using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Session;

namespace Vocaterra.Game.Core.Tests.Session;

/// <summary>W01: a craft is a world event (<see cref="WorldEvents.ItemCrafted"/>), named with the output's noun and counted like a pickup.</summary>
public sealed class ItemCraftedTests
{
    [Fact]
    public void ATorchCraftLogsTheWorldEventWithItsNoun()
    {
        var (session, _, _) = CampObjectiveTests.PlayCamp();

        var crafted = session.Events.OfType<WorldEvent>().Single(static e => e.Name == WorldEvents.ItemCrafted);

        Assert.Equal("world item_crafted (antorcha)", crafted.Text);
        Assert.Equal("antorcha", crafted.Detail);
    }

    [Fact]
    public void AnObjectiveStepOnACraftCountsTheCraftedItemOnly()
    {
        var step = new ObjectiveStepSpec("torch", "antorcha", "antorcha", 1, new Trigger(WorldEvents.ItemCrafted, null, "antorcha"), null);
        var objective = new Objective(new ObjectiveSpec("craft", "Taller", [step], [], []));

        _ = objective.Record(WorldEvents.ItemCrafted, "madera", 1);
        Assert.False(objective.Complete);

        _ = objective.Record(WorldEvents.ItemCrafted, "antorcha", 1);
        Assert.True(objective.Complete);
    }

    [Fact]
    public void ACraftIsAValidTriggerEventName()
    {
        using var document = System.Text.Json.JsonDocument.Parse("""{ "event": "item_crafted", "detail": "antorcha" }""");

        var trigger = Trigger.Parse(document.RootElement, WorldEvents.All, "test");

        Assert.True(trigger.Matches(new WorldEvent(0, 0, WorldEvents.ItemCrafted, null, "antorcha")));
        Assert.Contains(WorldEvents.ItemCrafted, WorldEvents.All);
    }
}
