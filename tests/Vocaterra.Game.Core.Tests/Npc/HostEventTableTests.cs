using System.Text.Json.Nodes;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Game.Core.Tests.Npc;

/// <summary>
/// <c>game/host-events.json</c> schema 2 (VILLAGE_PLAN V02): <c>give_item</c> with a count (a number, or the number the learner said, capped at five), and the beacon
/// sent to a named place of the scene or to the place a slot of the learner's line names.
/// </summary>
public sealed class HostEventTableTests
{
    private static string File(string action) =>
        $$"""{ "schemaVersion": 2, "events": [ { "npc": "villager", "event": "villager.do", "action": {{action}} } ] }""";

    private static NpcEffect Read(string action) => HostEventTable.Parse(File(action)).Of("villager", "villager.do");

    private static SlotMap Slots(params (string Key, string Value)[] pairs) => SlotMap.From(pairs.Select(static p => new KeyValuePair<string, string>(p.Key, p.Value)));

    [Fact]
    public void GiveItemGivesOneAFixedCountOrTheNumberTheLearnerSaid()
    {
        Assert.Equal(NpcEffect.Give(ItemId.Bread), Read("""{ "kind": "give_item", "item": "bread" }"""));
        Assert.Equal(NpcEffect.Give(ItemId.Bread, 3), Read("""{ "kind": "give_item", "item": "bread", "count": 3 }"""));
        Assert.Equal(NpcEffect.GiveAsked(ItemId.Bread), Read("""{ "kind": "give_item", "item": "bread", "count": "qty" }"""));

        Assert.Equal(1, Read("""{ "kind": "give_item", "item": "bread" }""").CountFor(Slots(("qty", "4"))));
        Assert.Equal(3, Read("""{ "kind": "give_item", "item": "bread", "count": 3 }""").CountFor(Slots(("qty", "4")))); // a fixed count ignores the learner's number
        Assert.Equal(4, NpcEffect.GiveAsked(ItemId.Bread).CountFor(Slots(("qty", "4"), ("item", "pan"))));
    }

    [Theory]
    [InlineData(null, 1)] // "quiero pan": no number, one
    [InlineData("2", 2)]
    [InlineData("5", 5)]
    [InlineData("9", NpcEffect.MaxQuantity)] // "quiero nueve panes": capped at five
    [InlineData("1000000000000", NpcEffect.MaxQuantity)]
    [InlineData("0", 1)]
    [InlineData("dos", 1)] // the core gives a number as its digits; anything else is read as one
    public void TheNumberTheLearnerSaidIsOneWithoutANumberAndAtMostFive(string? qty, int expected)
    {
        var slots = qty is null ? Slots(("item", "pan")) : Slots(("item", "pan"), ("qty", qty));
        Assert.Equal(expected, NpcEffect.Quantity(slots));
        Assert.Equal(expected, NpcEffect.GiveAsked(ItemId.Bread).CountFor(slots));
    }

    [Fact]
    public void TheBeaconGoesToANamedPlaceOrToThePlaceASlotNames()
    {
        Assert.Equal(NpcEffect.PointAtPlaceNamed("stall"), Read("""{ "kind": "point_at_place", "place": "stall" }"""));
        Assert.Equal(NpcEffect.PointAtSlotNamed("place"), Read("""{ "kind": "point_at_slot", "slot": "place" }"""));
        Assert.Equal(NpcEffect.PointAt("río"), Read("""{ "kind": "point_at_noun", "noun": "río" }"""));
    }

    [Theory]
    [InlineData("""{ "kind": "give_item", "item": "bread", "count": 0 }""", "the count of villager's 'villager.do'")]
    [InlineData("""{ "kind": "give_item", "item": "bread", "count": "two" }""", "the count of villager's 'villager.do'")]
    [InlineData("""{ "kind": "give_item", "item": "book", "count": 2 }""", "the item's stack")] // the book stacks one
    [InlineData("""{ "kind": "point_at_place" }""", "'place' is missing")]
    [InlineData("""{ "kind": "point_at_slot", "slot": "" }""", "'slot' is missing")]
    public void AnActionThatDoesNotHoldTogetherIsRefused(string action, string problem)
    {
        var error = Assert.Throws<InvalidDataException>(() => HostEventTable.Parse(File(action)));
        Assert.Contains(problem, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void APlaceTheSceneDoesNotHaveOrAnNpcThatCannotSayItDoesNotKnowOneIsAProblem()
    {
        var root = JsonNode.Parse(System.IO.File.ReadAllText(Repo.Path("content", "game-v1", "game", "host-events.json")))!;
        var events = root["events"]!.AsArray();
        events.Add(Event("villager", "villager.go", new JsonObject { ["kind"] = "point_at_place", ["place"] = "bakery" }));
        events.Add(Event("villager", "villager.show", new JsonObject { ["kind"] = "point_at_place", ["place"] = "signpost" }));
        events.Add(Event("shopkeeper", "stall.point", new JsonObject { ["kind"] = "point_at_slot", ["slot"] = "place" }));

        var problems = GameDataValidator.Validate(TestGame.Data.WithHostEvents(HostEventTable.Parse(root.ToJsonString())));

        Assert.Contains("game/host-events.json: villager's 'villager.go' points at the place 'bakery', which is not a point of the scene.", problems);
        Assert.Contains(
            "game/host-events.json: shopkeeper's 'stall.point' points at the place the learner names, and no state of agents/shopkeeper.json takes 'shopkeeper.unknown_place' to say it does not know one.",
            problems);
        Assert.DoesNotContain(problems, static p => p.Contains("'villager.show'", StringComparison.Ordinal)); // a named prop is a point of the scene
        Assert.DoesNotContain(problems, static p => p.Contains("'villager.point'", StringComparison.Ordinal)); // the villager can say he does not know a place

        static JsonObject Event(string npc, string name, JsonObject action) => new() { ["npc"] = npc, ["event"] = name, ["action"] = action };
    }

    [Fact]
    public void AFileOfSchemaOneIsRefused()
    {
        var error = Assert.Throws<InvalidDataException>(() => HostEventTable.Parse(File("""{ "kind": "give_item", "item": "bread" }""").Replace("\"schemaVersion\": 2", "\"schemaVersion\": 1", StringComparison.Ordinal)));
        Assert.Contains("schemaVersion 2", error.Message, StringComparison.Ordinal);
    }
}
