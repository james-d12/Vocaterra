using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Crafting;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Items;

/// <summary>An item that is a row of the data alone (I01: id 100, no <see cref="ItemId"/> member) works where an item can be named: a noun, a recipe, a shop row, a gift and a script.</summary>
public sealed class DataOnlyItemTests
{
    private static readonly ItemId Gem = (ItemId)100;

    private static readonly IItemCatalogue Items = TestItems.With(new ItemDefinition(Gem, "gem", 7, "gem", ItemHeld.Sprite, ["material"]));

    private const string Nouns = """{"schemaVersion":1,"nouns":[{"lemma":"piedra","items":["gem"],"blocks":["stone"]},{"lemma":"madera","items":["wood"]}]}""";

    private static NounCatalog Catalogue() => NounCatalog.Parse(TestGame.Bundle, Nouns, Items);

    [Fact]
    public void Given_ANounThatNamesTheItem_When_TheCatalogueIsRead_Then_TheNounAndTheItemFindEachOther()
    {
        var nouns = Catalogue();

        Assert.Equal([Gem], nouns.ForLemma("piedra")!.Items);
        Assert.Equal("piedra", nouns.ForItem(Gem)!.Lemma);
        Assert.Same(Items, nouns.Items);
    }

    [Fact]
    public void Given_AnItemTheCatalogueLacks_When_ANounNamesIt_Then_TheReadFailsNamingIt()
    {
        var error = Assert.Throws<InvalidDataException>(() => NounCatalog.Parse(TestGame.Bundle, Nouns, TestItems.Catalogue));

        Assert.Contains("unknown item 'gem'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_ARecipeWhoseOutputIsTheItem_When_TheBookIsRead_Then_TheRuleMakesIt()
    {
        var book = RecipeBook.Parse("""{ "schemaVersion": 1, "recipes": [ { "output": "piedra", "count": 2, "ingredients": [ { "item": "madera", "count": 3 } ] } ] }""", Catalogue());

        var rule = Assert.Single(book.Rules);
        Assert.Equal(Gem, rule.Output);
        Assert.Equal(2, rule.OutputCount);
    }

    [Fact]
    public void Given_AShopRowAndAGiftOfTheItem_When_TheTableIsRead_Then_TheEffectsNameItAndAGiftIsHeldToTheItemsStack()
    {
        var table = HostEventTable.Parse(
            """
            { "schemaVersion": 2, "events": [
              { "npc": "shopkeeper", "event": "stall.sell_gem", "action": { "kind": "sell", "item": "gem", "price": 3 } },
              { "npc": "shopkeeper", "event": "stall.give_gem", "action": { "kind": "give_item", "item": "gem", "count": 7 } } ] }
            """,
            Items);

        Assert.Equal(Gem, table.Of("shopkeeper", "stall.sell_gem").Item);
        Assert.Equal(3, table.Of("shopkeeper", "stall.sell_gem").Price);
        Assert.Equal(7, table.Of("shopkeeper", "stall.give_gem").Count);
        _ = Assert.Throws<InvalidDataException>(() => HostEventTable.Parse(
            """{ "schemaVersion": 2, "events": [ { "npc": "shopkeeper", "event": "stall.give_gem", "action": { "kind": "give_item", "item": "gem", "count": 8 } } ] }""",
            Items));
    }

    [Fact]
    public void Given_AScript_When_ItNamesTheItemByItsKeyOrItsNoun_Then_ItResolves()
    {
        Assert.Equal(Gem, ScriptItemNames.Resolve(Catalogue(), Items, "gem"));
        Assert.Equal(Gem, ScriptItemNames.Resolve(Catalogue(), Items, "piedra"));
        Assert.Equal(Gem, ScriptItemNames.Resolve(Catalogue(), Items, "GEM"));
        Assert.Null(ScriptItemNames.Resolve(Catalogue(), TestItems.Catalogue, "gem"));
    }
}
