using System.Text.Json;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Event;

/// <summary>
/// One trigger model for the tips, the lessons' <c>do</c> steps and the objectives (CONTENT_PLAN 3.4, C06): a world event, with an NPC and a detail that must
/// match exactly when the trigger names them.
/// </summary>
public sealed class TriggerTests
{
    private static WorldEvent World(string name, string? npc = null, string? detail = null) => new(0, 0, name, npc, detail);

    [Fact]
    public void ATriggerMatchesItsEventAndTheNpcAndTheDetailItNames()
    {
        var any = new Trigger(WorldEvents.ItemPickedUp);
        var fruit = new Trigger(WorldEvents.ItemPickedUp, Detail: "fruta");
        var atStall = new Trigger(WorldEvents.TalkOpened, Npc: "shopkeeper");

        Assert.True(any.Matches(World(WorldEvents.ItemPickedUp, detail: "madera")));
        Assert.False(any.Matches(World(WorldEvents.BottleFilled)));
        Assert.True(fruit.Matches(World(WorldEvents.ItemPickedUp, detail: "fruta")));
        Assert.False(fruit.Matches(World(WorldEvents.ItemPickedUp, detail: "frutas"))); // exact: not a part of the detail
        Assert.False(fruit.Matches(World(WorldEvents.ItemPickedUp, detail: "Fruta"))); // and not without case
        Assert.False(fruit.Matches(World(WorldEvents.ItemPickedUp)));
        Assert.True(atStall.Matches(World(WorldEvents.TalkOpened, "shopkeeper")));
        Assert.False(atStall.Matches(World(WorldEvents.TalkOpened, "villager")));
        Assert.False(atStall.Matches(World(WorldEvents.TalkOpened)));
    }

    [Fact]
    public void TheCampsRingIsNotThePlayersRing()
    {
        var camp = new Trigger(WorldEvents.TorchAtRing, Detail: "firering");

        Assert.True(camp.Matches(World(WorldEvents.TorchAtRing, detail: "firering")));
        Assert.False(camp.Matches(World(WorldEvents.TorchAtRing, detail: "firering.player"))); // an exact detail keeps the two rings apart
    }

    [Fact]
    public void AMomentThatIsNoWorldEventMatchesOnlyATriggerWithNoFilter()
    {
        Assert.True(new Trigger(Trigger.Start).Matches(Trigger.Start));
        Assert.False(new Trigger(Trigger.Start).Matches(Trigger.AimedNpc));
        Assert.False(new Trigger(Trigger.AimedNpc, Npc: "guide").Matches(Trigger.AimedNpc));
    }

    [Theory]
    [InlineData("""{ "event": "the_moon_landed" }""", "not an event the game raises")]
    [InlineData("""{ "event": "talk_opened", "at": "guide" }""", "'at'")]
    [InlineData("""{ "detail": "fruta" }""", "names no event")]
    [InlineData("\"item_picked_up\"", "must be an object")]
    public void ATriggerThatNamesNoEventOrAnUnknownOneIsRefused(string json, string problem)
    {
        using var document = JsonDocument.Parse(json);

        var error = Assert.Throws<InvalidDataException>(() => Trigger.Parse(document.RootElement, WorldEvents.All, "test"));

        Assert.Contains(problem, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheShippedLessonsDetailFiltersAreWholeDetails()
    {
        // Before C06 a do step's detail matched as a part of the event's, ignoring case. The shipped lessons' detail filters are whole details: the noun the pickup
        // names, and since VILLAGE_PLAN V10 the scene's names of the places El pueblo walks to (the arrival's detail is the place's name), so matching exactly changes nothing.
        // V11: Números y pan's wood (a pickup's noun), its order (the delivery's detail is the order's id) and its purchase (a gift's detail is the noun and how it came).
        // V12: Gente del pueblo's wood and its order, the same two kinds. V13: ¿Cómo es?'s walk to the river by the fisher (an arrival at his point) and its order.
        var filters = TestGame.Data.Lessons.Lessons.SelectMany(static l => l.Steps).Where(static s => s.Type == LessonStepType.Do && s.Detail is not null).ToList();

        Assert.Equal(10, filters.Count);
        Assert.Equal(new Trigger(WorldEvents.ItemPickedUp, Detail: "fruta"), filters[0].Trigger);
        Assert.NotNull(TestGame.Nouns.ForLemma(filters[0].Detail!));
        Assert.Equal([new Trigger(WorldEvents.BeaconReached, Detail: "bakery"), new Trigger(WorldEvents.BeaconReached, Detail: "smithy")], filters.Skip(1).Take(2).Select(static s => s.Trigger));
        Assert.All(filters.Skip(1).Take(2), static s => Assert.Equal(s.Detail, s.PointPlace));
        Assert.Equal(
            [new Trigger(WorldEvents.ItemPickedUp, Detail: "madera"), new Trigger(WorldEvents.OrderDelivered, "baker", "oven-wood"), new Trigger(WorldEvents.ItemGiven, "baker", "pan, request")],
            filters.Skip(3).Take(3).Select(static s => s.Trigger));
        Assert.Equal([new Trigger(WorldEvents.ItemPickedUp, Detail: "madera"), new Trigger(WorldEvents.OrderDelivered, "smith", "axe-handle")], filters.Skip(6).Take(2).Select(static s => s.Trigger));
        Assert.Equal([new Trigger(WorldEvents.BeaconReached, Detail: "npc.fisher"), new Trigger(WorldEvents.OrderDelivered, "farmer", "water-for-plants")], filters.Skip(8).Select(static s => s.Trigger));
        Assert.Equal("npc.fisher", filters[8].PointPlace);
        Assert.NotNull(TestGame.Nouns.ForLemma(filters[3].Detail!));
    }

    [Fact]
    public void TheShippedTipsWaitForTheirMoments()
    {
        Assert.Equal(["look", "walk", "jump", "note", "gather"], TestGame.Data.Tips.Where(static t => t.Trigger == new Trigger(Trigger.Start)).Select(static t => t.Id));
        Assert.All(TestGame.Data.Tips, static t => Assert.Contains(t.Trigger.Event, Trigger.TipEvents));
    }
}
