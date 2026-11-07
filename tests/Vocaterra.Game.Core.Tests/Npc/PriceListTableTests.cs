using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Game.Core.Tests.Npc;

/// <summary>
/// The price list of a <c>sell</c> or a <c>buy</c> (W04): <c>"slot": "item"</c> and <c>"prices": { "pan": 2, "manzana": 1 }</c> instead of one <c>item</c> and <c>price</c>. The table reads it,
/// rejects the malformed forms, and the effect comes to the item the line names at that item's price; an item not on the list is the miss a fixed entry gives for another item.
/// </summary>
public sealed class PriceListTableTests
{
    private static string File(string action) =>
        $$"""{ "schemaVersion": 2, "events": [ { "npc": "baker", "event": "baker.sell_bread", "action": {{action}} } ] }""";

    private static NpcEffect Read(string action) => HostEventTable.Parse(File(action)).Of("baker", "baker.sell_bread");

    private static SlotMap Slots(params (string Key, string Value)[] pairs) => SlotMap.From(pairs.Select(static p => new KeyValuePair<string, string>(p.Key, p.Value)));

    private const string Sells = """{ "kind": "sell", "slot": "item", "prices": { "pan": 2, "manzana": 1 } }""";

    [Fact]
    public void ASellAndABuyReadAPriceListBesideTheFixedForm()
    {
        var sell = Read(Sells);
        Assert.Equal(NpcEffectKind.Sell, sell.Kind);
        Assert.Equal(new Dictionary<string, int> { ["pan"] = 2, ["manzana"] = 1 }, sell.Prices);

        var buy = Read("""{ "kind": "buy", "slot": "item", "prices": { "madera": 1 } }""");
        Assert.Equal((NpcEffectKind.Buy, 1), (buy.Kind, buy.Prices!["madera"]));

        Assert.Null(Read("""{ "kind": "sell", "item": "bread", "price": 2 }""").Prices); // the fixed entry keeps its form
        Assert.Equal(NpcEffect.SellAt(ItemId.Bread, 2), Read("""{ "kind": "sell", "item": "bread", "price": 2 }"""));
    }

    [Theory]
    [InlineData("""{ "kind": "sell", "slot": "item", "prices": { "pan": 2 }, "item": "bread" }""")] // both forms
    [InlineData("""{ "kind": "sell", "slot": "item", "prices": { "pan": 2 }, "price": 2 }""")]
    [InlineData("""{ "kind": "buy", "slot": "item", "prices": { "pan": 2 }, "item": "bread", "price": 2 }""")]
    [InlineData("""{ "kind": "sell", "slot": "place", "prices": { "pan": 2 } }""")] // a slot a sale does not read
    [InlineData("""{ "kind": "sell", "prices": { "pan": 2 } }""")] // no slot
    [InlineData("""{ "kind": "sell", "slot": "item" }""")] // no prices
    [InlineData("""{ "kind": "sell", "slot": "item", "prices": { } }""")] // an empty list
    [InlineData("""{ "kind": "sell", "slot": "item", "prices": [ 2 ] }""")]
    [InlineData("""{ "kind": "sell", "slot": "item", "prices": { "pan": 0 } }""")] // not positive
    [InlineData("""{ "kind": "buy", "slot": "item", "prices": { "pan": -1 } }""")]
    [InlineData("""{ "kind": "sell", "slot": "item", "prices": { "pan": 1.5 } }""")] // not an integer
    [InlineData("""{ "kind": "sell", "slot": "item", "prices": { "pan": "2" } }""")]
    public void AMalformedPriceListIsRejected(string action) =>
        Assert.Throws<InvalidDataException>(() => Read(action));

    [Fact]
    public void ANounThatIsNoCatalogueNounOrNamesNoItemFailsTheValidator()
    {
        var bad = TestGame.Data.WithHostEvents(HostEventTable.Parse(File("""{ "kind": "sell", "slot": "item", "prices": { "pan": 2, "queso": 1, "casa": 1 } }""")));

        var problems = GameDataValidator.Validate(bad);

        Assert.Contains(problems, static p => p.Contains("prices 'queso', which is not a catalogue noun", StringComparison.Ordinal));
        Assert.Contains(problems, static p => p.Contains("prices 'casa', which names no item", StringComparison.Ordinal));
        Assert.DoesNotContain(problems, static p => p.Contains("prices 'pan'", StringComparison.Ordinal));
    }

    [Fact]
    public void TheEffectComesToTheItemTheLineNamesAtItsOwnPrice()
    {
        var nouns = TestGame.Data.Nouns;
        var sell = Read(Sells);

        Assert.Equal(NpcEffect.SellAt(ItemId.Bread, 2), sell.For(Slots(("item", "pan")), nouns));
        Assert.Equal(NpcEffect.SellAt(ItemId.Apple, 1), sell.For(Slots(("item", "manzana")), nouns));
        Assert.Equal(6, sell.For(Slots(("item", "pan"), ("qty", "3")), nouns).CostFor(Slots(("item", "pan"), ("qty", "3"))));
        Assert.Same(sell, sell.For(Slots(("item", "madera")), nouns)); // another item: unresolved
        Assert.Same(sell, sell.For(Slots(("qty", "2")), nouns)); // no item named
    }

    [Fact]
    public void AnItemNotOnTheListIsTheMissAFixedEntryGivesForAnotherItem()
    {
        var nouns = TestGame.Data.Nouns;
        var bag = new Inventory();
        Assert.Equal(0, bag.Add(ItemId.Coin, 10));
        var sell = Read(Sells);
        var buy = Read("""{ "kind": "buy", "slot": "item", "prices": { "madera": 1 } }""");

        Assert.Equal(NpcHostEvents.WontSell, sell.ExchangeMiss(Slots(("item", "madera")), bag, nouns));
        Assert.Equal(NpcHostEvents.WontSell, sell.ExchangeMiss(Slots(("item", "casa")), bag, nouns)); // a noun of no item
        Assert.Equal(NpcHostEvents.WontSell, sell.ExchangeMiss(Slots(("qty", "2")), bag, nouns));
        Assert.Equal(NpcHostEvents.WontBuy, buy.ExchangeMiss(Slots(("item", "pan")), bag, nouns));
        Assert.Null(sell.ExchangeMiss(Slots(("item", "pan"), ("qty", "2")), bag, nouns));
        Assert.Equal(NpcHostEvents.CantPay, sell.ExchangeMiss(Slots(("item", "pan"), ("qty", "6")), bag, nouns)); // twelve coins
        Assert.Equal(NpcHostEvents.NoneAsked, sell.ExchangeMiss(Slots(("item", "pan"), ("qty", "0")), bag, nouns));
        Assert.Equal(NpcHostEvents.CantSell, buy.ExchangeMiss(Slots(("item", "madera")), bag, nouns)); // none in the bag
    }
}
