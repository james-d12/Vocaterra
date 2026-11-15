using System.Text.Json.Nodes;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Game.Core.Tests.Npc;

/// <summary>
/// <c>game/host-events.json</c> schema 2 (village-plan V02): <c>give_item</c> with a count (a number, or the number the learner said, capped at five), and the beacon
/// sent to a named place of the scene or to the place a slot of the learner's line names. A purchase has no cap since V19: the number said, bounded by the coins and the bag.
/// </summary>
public sealed class HostEventTableTests
{
    private static string File(string action) =>
        $$"""{ "schemaVersion": 2, "events": [ { "npc": "villager", "event": "villager.do", "action": {{action}} } ] }""";

    private static NpcEffect Read(string action) => HostEventTable.Parse(File(action), TestItems.Catalogue).Of("villager", "villager.do");

    private static SlotMap Slots(params (string Key, string Value)[] pairs) => SlotMap.From(pairs.Select(static p => new KeyValuePair<string, string>(p.Key, p.Value)));

    [Fact]
    public void GiveItemGivesOneAFixedCountOrTheNumberTheLearnerSaid()
    {
        Assert.Equal(NpcEffect.Give(TestItems.Bread), Read("""{ "kind": "give_item", "item": "bread" }"""));
        Assert.Equal(NpcEffect.Give(TestItems.Bread, 3), Read("""{ "kind": "give_item", "item": "bread", "count": 3 }"""));
        Assert.Equal(NpcEffect.GiveAsked(TestItems.Bread), Read("""{ "kind": "give_item", "item": "bread", "count": "qty" }"""));

        Assert.Equal(1, Read("""{ "kind": "give_item", "item": "bread" }""").CountFor(Slots(("qty", "4"))));
        Assert.Equal(3, Read("""{ "kind": "give_item", "item": "bread", "count": 3 }""").CountFor(Slots(("qty", "4")))); // a fixed count ignores the learner's number
        Assert.Equal(4, NpcEffect.GiveAsked(TestItems.Bread).CountFor(Slots(("qty", "4"), ("item", "pan"))));
    }

    [Theory]
    [InlineData(null, 1, 1)] // "quiero pan": no number, one
    [InlineData("2", 2, 2)]
    [InlineData("5", 5, 5)]
    [InlineData("9", 9, NpcEffect.MaxQuantity)] // "quiero nueve panes": the number said (V19, the review's R9); a gift by the number said is capped at five
    [InlineData("15", 15, NpcEffect.MaxQuantity)] // session 10 teaches 11 to 20
    [InlineData("1000000000000", int.MaxValue, NpcEffect.MaxQuantity)] // twelve digits overflow no int
    [InlineData("99999999999999999999999", int.MaxValue, NpcEffect.MaxQuantity)] // nor more than a long holds
    [InlineData("0", 0, 0)] // "quiero cero panes": none (V19, the review's R10: it was read as one)
    [InlineData("dos", 1, 1)] // the core gives a number as its digits; anything else is read as one
    [InlineData("-3", 1, 1)]
    public void TheNumberTheLearnerSaidIsOneWithoutANumberAndAGiftByItIsAtMostFive(string? qty, int said, int given)
    {
        var slots = qty is null ? Slots(("item", "pan")) : Slots(("item", "pan"), ("qty", qty));
        Assert.Equal(said, NpcEffect.Quantity(slots));
        Assert.Equal(said, NpcEffect.SellAt(TestItems.Bread, 2).CountFor(slots));
        Assert.Equal(given, NpcEffect.GiveAsked(TestItems.Bread).CountFor(slots));
    }

    [Fact]
    public void TheBeaconGoesToANamedPlaceOrToThePlaceASlotNames()
    {
        Assert.Equal(NpcEffect.PointAtPlaceNamed("stall"), Read("""{ "kind": "point_at_place", "place": "stall" }"""));
        Assert.Equal(NpcEffect.PointAtSlotNamed("place"), Read("""{ "kind": "point_at_slot", "slot": "place" }"""));
        Assert.Equal(NpcEffect.PointAt("río"), Read("""{ "kind": "point_at_noun", "noun": "río" }"""));
    }

    [Fact]
    public void TakeItemTakesACountAndSellChargesItsPriceForEachItemAsked()
    {
        Assert.Equal(NpcEffect.Take(ItemId.Wood, 2), Read("""{ "kind": "take_item", "item": "wood", "count": 2 }"""));
        Assert.Equal(NpcEffect.Take(ItemId.Coin), Read("""{ "kind": "take_item", "item": "coin" }"""));
        var sell = Read("""{ "kind": "sell", "item": "bread", "price": 2 }""");
        Assert.Equal(NpcEffect.SellAt(TestItems.Bread, 2), sell);

        Assert.Equal((1, 2), (sell.CountFor(Slots(("item", "pan"))), sell.CostFor(Slots(("item", "pan")))));
        Assert.Equal((3, 6), (sell.CountFor(Slots(("qty", "3"))), sell.CostFor(Slots(("qty", "3")))));
        Assert.Equal((12, 24), (sell.CountFor(Slots(("qty", "12"))), sell.CostFor(Slots(("qty", "12"))))); // no cap since V19 (the review's R9)
        Assert.Equal((0, 0), (sell.CountFor(Slots(("qty", "0"))), sell.CostFor(Slots(("qty", "0"))))); // none (R10)
        Assert.Equal(int.MaxValue, sell.CostFor(Slots(("qty", "1000000000000")))); // the price times twelve digits overflows nothing: more than any bag holds
        Assert.Equal(int.MaxValue, sell.PriceOf(int.MaxValue));
        Assert.Equal(0, NpcEffect.Give(TestItems.Bread).CostFor(Slots(("qty", "3")))); // only a sale costs
    }

    [Theory]
    [InlineData("""{ "kind": "sell", "item": "bread" }""", "the price of villager's 'villager.do'")]
    [InlineData("""{ "kind": "sell", "item": "bread", "price": 0 }""", "the price of villager's 'villager.do'")]
    [InlineData("""{ "kind": "sell", "item": "queso", "price": 1 }""", "unknown item 'queso'")]
    [InlineData("""{ "kind": "take_item", "item": "axe", "count": 2 }""", "the item's stack")] // the axe stacks one
    [InlineData("""{ "kind": "give_item", "item": "bread", "count": 0 }""", "the count of villager's 'villager.do'")]
    [InlineData("""{ "kind": "give_item", "item": "bread", "count": "two" }""", "the count of villager's 'villager.do'")]
    [InlineData("""{ "kind": "give_item", "item": "book", "count": 2 }""", "the item's stack")] // the book stacks one
    [InlineData("""{ "kind": "point_at_place" }""", "'place' is missing")]
    [InlineData("""{ "kind": "point_at_slot", "slot": "" }""", "'slot' is missing")]
    public void AnActionThatDoesNotHoldTogetherIsRefused(string action, string problem)
    {
        var error = Assert.Throws<InvalidDataException>(() => HostEventTable.Parse(File(action), TestItems.Catalogue));
        Assert.Contains(problem, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void APlaceTheSceneDoesNotHaveOrAnNpcThatCannotSayItDoesNotKnowOneOrCannotBePaidIsAProblem()
    {
        var root = JsonNode.Parse(System.IO.File.ReadAllText(Repo.Path("content", "game-v1", "game", "host-events.json")))!;
        var events = root["events"]!.AsArray();
        events.Add(Event("villager", "villager.go", new JsonObject { ["kind"] = "point_at_place", ["place"] = "mill" }));
        events.Add(Event("villager", "villager.show", new JsonObject { ["kind"] = "point_at_place", ["place"] = "signpost" }));
        events.Add(Event("shopkeeper", "stall.point", new JsonObject { ["kind"] = "point_at_slot", ["slot"] = "place" }));
        events.Add(Event("fisher", "fisher.sell", new JsonObject { ["kind"] = "sell", ["item"] = "bread", ["price"] = 2 }));
        events.Add(Event("fisher", "fisher.buy", new JsonObject { ["kind"] = "buy", ["item"] = "wood", ["price"] = 1 }));

        var problems = GameDataValidator.Validate(TestGame.Data.WithHostEvents(HostEventTable.Parse(root.ToJsonString(), TestItems.Catalogue)));

        Assert.Contains("game/host-events.json: villager's 'villager.go' points at the place 'mill', which is not a point of the scene.", problems);
        Assert.Contains(
            "game/host-events.json: shopkeeper's 'stall.point' points at the place the learner names, and no state of agents/shopkeeper.json takes 'shopkeeper.unknown_place' to say it does not know one.",
            problems);
        Assert.Contains("game/host-events.json: fisher's 'fisher.sell' sells, and no state of agents/fisher.json takes 'fisher.cant_pay' to say the coins are too few.", problems);
        Assert.Contains("game/host-events.json: fisher's 'fisher.sell' sells, and no state of agents/fisher.json takes 'fisher.none_asked' to answer a request for none.", problems); // V19
        Assert.Contains("game/host-events.json: fisher's 'fisher.sell' sells, and no state of agents/fisher.json takes 'fisher.no_room' to say the bag is full.", problems);
        Assert.Contains("game/host-events.json: fisher's 'fisher.buy' buys, and no state of agents/fisher.json takes 'fisher.none_offered' to answer an offer of none.", problems);
        Assert.Contains("game/host-events.json: fisher's 'fisher.buy' buys, and no state of agents/fisher.json takes 'fisher.no_room' to say the bag is full.", problems);
        Assert.DoesNotContain(problems, static p => p.Contains("'villager.show'", StringComparison.Ordinal)); // a named prop is a point of the scene
        Assert.DoesNotContain(problems, static p => p.Contains("'villager.point'", StringComparison.Ordinal)); // the villager can say he does not know a place

        static JsonObject Event(string npc, string name, JsonObject action) => new() { ["npc"] = npc, ["event"] = name, ["action"] = action };
    }

    [Fact]
    public void AnInsteadThatNoStateTakesOrANounOffTheCatalogueIsAProblem()
    {
        var root = JsonNode.Parse(System.IO.File.ReadAllText(Repo.Path("content", "game-v1", "game", "host-events.json")))!;
        var gift = root["events"]!.AsArray().First(static e => e!["event"]!.GetValue<string>() == "stall.give_water")!["action"]!;
        gift["instead"] = new JsonObject { ["pan"] = "give_bred" }; // V30: a typo in the event's name
        gift["asked"] = new JsonArray("agua", "botela"); // and in a noun

        var problems = GameDataValidator.Validate(TestGame.Data.WithHostEvents(HostEventTable.Parse(root.ToJsonString(), TestItems.Catalogue)));

        Assert.Contains("game/host-events.json: shopkeeper's 'stall.give_water' answers 'pan' with 'give_bred' instead, and no state of agents/shopkeeper.json takes 'shopkeeper.give_bred'.", problems);
        Assert.Contains("game/host-events.json: shopkeeper's 'stall.give_water' names 'botela', which is not a catalogue noun.", problems);
        Assert.Empty(GameDataValidator.Validate(TestGame.Data));
    }

    [Fact]
    public void AFileOfSchemaOneIsRefused()
    {
        var error = Assert.Throws<InvalidDataException>(() => HostEventTable.Parse(File("""{ "kind": "give_item", "item": "bread" }""").Replace("\"schemaVersion\": 2", "\"schemaVersion\": 1", StringComparison.Ordinal), TestItems.Catalogue));
        Assert.Contains("schemaVersion 2", error.Message, StringComparison.Ordinal);
    }
}
