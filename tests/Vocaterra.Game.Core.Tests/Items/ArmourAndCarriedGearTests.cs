using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Stats;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Items;

/// <summary>
/// The second half of the gear the bundle ships (I08b, inventory plan 3.13a): the four pieces of iron armour, the five accessories, the shield, the lantern and the torch in the off-hand, and the two bags: their rows, their numbers
/// and their words.
/// </summary>
public sealed class ArmourAndCarriedGearTests
{
    private static ItemDefinition Row(string name) => TestItems.Catalogue.Find(name) ?? throw new InvalidOperationException($"No item row {name}.");

    [Theory]
    [InlineData("helmet", 110, "head", 2, "head")]
    [InlineData("cuirass", 111, "body", 5, "body")]
    [InlineData("greaves", 112, "legs", 3, "legs")]
    [InlineData("boots", 113, "feet", 2, "feet")]
    public void Given_APieceOfArmour_When_ItsRowIsRead_Then_ItHasItsSlotItsPointsAndAColourForThePartItCovers(string name, int id, string wear, int points, string part)
    {
        var row = Row(name);

        Assert.Equal((ItemId)id, row.Id);
        Assert.Equal(1, row.MaxStack);
        Assert.Equal(["armour"], row.Tags);
        Assert.Equal(wear, row.Wear);
        Assert.Equal([(StatKeys.Armour, (double)points)], row.Stats!.Select(static p => (p.Key, p.Value)));
        Assert.Equal([part], row.Look!.Parts.Keys);
        Assert.Equal(name, row.Icon);
    }

    [Fact]
    public void Given_TheArmour_When_ItsPointsAreAddedUp_Then_TheFullSetIsTwelveAndWithTheShieldFifteen()
    {
        var points = (string name) => Row(name).Stats![StatKeys.Armour];

        Assert.Equal(12, points("helmet") + points("cuirass") + points("greaves") + points("boots"));
        Assert.Equal(15, points("helmet") + points("cuirass") + points("greaves") + points("boots") + points("shield"));
    }

    [Fact]
    public void Given_TheArmourAndTheClothes_When_TheirLooksAreCompared_Then_NoTwoPiecesOfOneSlotShareAColour()
    {
        var worn = TestItems.Catalogue.All.Where(static d => d.Tags.Contains("armour") || d.Tags.Contains("clothing")).ToList();

        foreach (var slot in worn.GroupBy(static d => d.Wear))
        {
            var colours = slot.Select(d => d.Look!.Parts[slot.Key!].ToString()).ToList();
            Assert.Equal(colours.Count, colours.Distinct().Count());
        }
    }

    [Theory]
    [InlineData("ring", 114, StatKeys.MaxStamina, 20.0)]
    [InlineData("bracelet", 115, StatKeys.WalkSpeed, 1.1)]
    [InlineData("belt", 116, StatKeys.HungerDrain, 0.75)]
    [InlineData("necklace", 117, StatKeys.ThirstDrain, 0.75)]
    [InlineData("amulet", 118, StatKeys.Light, 5.0)]
    public void Given_AnAccessory_When_ItsRowIsRead_Then_ItIsWornAsAnAccessoryWithItsOneStat(string name, int id, string key, double value)
    {
        var row = Row(name);

        Assert.Equal((ItemId)id, row.Id);
        Assert.Equal(1, row.MaxStack);
        Assert.Equal(["accessory"], row.Tags);
        Assert.Equal("accessory", row.Wear);
        Assert.Equal([(key, value)], row.Stats!.Select(static p => (p.Key, p.Value)));
        Assert.Null(row.Look); // accessories are not drawn
        Assert.Equal(name, row.Icon);
    }

    [Theory]
    [InlineData("shield", 119, StatKeys.Armour, 3.0)]
    [InlineData("lantern", 120, StatKeys.Light, 12.0)]
    public void Given_AnOffHandItem_When_ItsRowIsRead_Then_ItIsWornInTheOffHandAndStacksToOne(string name, int id, string key, double value)
    {
        var row = Row(name);

        Assert.Equal((ItemId)id, row.Id);
        Assert.Equal(1, row.MaxStack);
        Assert.Equal(["offhand"], row.Tags);
        Assert.Equal("offhand", row.Wear);
        Assert.Equal([(key, value)], row.Stats!.Select(static p => (p.Key, p.Value)));
        Assert.Equal(name, row.Icon);
    }

    [Fact]
    public void Given_TheTorch_When_ItsRowIsRead_Then_ItIsStillPlaceableAndStacksButCanBeWornInTheOffHandForNineLight()
    {
        var torch = Row("torch");

        Assert.Equal(ItemId.Torch, torch.Id);
        Assert.Equal(99, torch.MaxStack);
        Assert.Contains("placeable", torch.Tags);
        Assert.Contains("offhand", torch.Tags);
        Assert.Equal("offhand", torch.Wear);
        Assert.Equal(9, torch.Stats![StatKeys.Light]);
        Assert.Equal("torch", torch.Icon);
        Assert.Equal("torch", torch.Held);
    }

    [Theory]
    [InlineData("sack", 121, 10)]
    [InlineData("backpack", 122, 20)]
    public void Given_ABag_When_ItsRowIsRead_Then_ItIsWornOnTheBackWithItsCapacity(string name, int id, int capacity)
    {
        var row = Row(name);

        Assert.Equal((ItemId)id, row.Id);
        Assert.Equal(1, row.MaxStack);
        Assert.Equal(["bag"], row.Tags);
        Assert.Equal("back", row.Wear);
        Assert.Equal(capacity, row.Capacity);
        Assert.Null(row.Stats);
        Assert.Equal(name, row.Icon);
    }

    [Fact]
    public void Given_TheCatalogue_When_TheIdsAreRead_Then_TheThirteenNewItemsTakeTheNextIdsAndNoneClashesWithATestRow()
    {
        var ids = new[] { "helmet", "cuirass", "greaves", "boots", "ring", "bracelet", "belt", "necklace", "amulet", "shield", "lantern", "sack", "backpack" }.Select(static n => (int)Row(n).Id).ToList();

        Assert.Equal(Enumerable.Range(110, 13), ids);
        Assert.All(ids, static id => Assert.True(id < 200)); // the tests' own gear starts at 200
    }

    [Theory]
    [InlineData("casco", "m", "helmet", "el casco", "un casco")]
    [InlineData("coraza", "f", "cuirass", "la coraza", "una coraza")]
    [InlineData("anillo", "m", "ring", "el anillo", "un anillo")]
    [InlineData("pulsera", "f", "bracelet", "la pulsera", "una pulsera")]
    [InlineData("cinturón", "m", "belt", "el cinturón", "un cinturón")]
    [InlineData("collar", "m", "necklace", "el collar", "un collar")]
    [InlineData("amuleto", "m", "amulet", "el amuleto", "un amuleto")]
    [InlineData("escudo", "m", "shield", "el escudo", "un escudo")]
    [InlineData("farol", "m", "lantern", "el farol", "un farol")]
    [InlineData("saco", "m", "sack", "el saco", "un saco")]
    [InlineData("mochila", "f", "backpack", "la mochila", "una mochila")]
    public void Given_AnItem_When_ItsNounIsLooked_Then_ItIsACatalogueNounOfTheRightGenderAndArticle(string lemma, string gender, string item, string definite, string indefinite)
    {
        var noun = TestGame.Nouns.ForLemma(lemma)!;

        Assert.Equal(gender, noun.Gender);
        Assert.Equal([Row(item).Id], noun.Items);
        Assert.Same(noun, TestGame.Nouns.ForItem(Row(item).Id));
        Assert.False(noun.PluralOnly);
        Assert.Equal(definite, noun.WithDefinite);
        Assert.Equal(indefinite, noun.WithIndefinite);
    }

    [Theory]
    [InlineData("greba", "greaves", "las grebas", "unas grebas", "grebas")]
    [InlineData("bota", "boots", "las botas", "unas botas", "botas")]
    public void Given_APairNoun_When_ItsArticlesAreMade_Then_TheyAreThePluralOnesAndTheLabelIsThePlural(string lemma, string item, string definite, string indefinite, string label)
    {
        var noun = TestGame.Nouns.ForLemma(lemma)!;

        Assert.Equal("f", noun.Gender);
        Assert.True(noun.PluralOnly);
        Assert.Equal([Row(item).Id], noun.Items);
        Assert.Equal(definite, noun.WithDefinite);
        Assert.Equal(indefinite, noun.WithIndefinite);
        Assert.Equal(label, TestGame.Nouns.LabelOf(Row(item).Id));
    }

    [Fact]
    public void Given_TheWordBotas_When_TheItemsAreLabelled_Then_OnlyTheArmourIsCalledThat()
    {
        var called = TestItems.Catalogue.All.Where(static d => TestGame.Nouns.LabelOf(d.Id) == "botas").Select(static d => d.Name).ToList();

        Assert.Equal(["boots"], called);
        Assert.Equal("zapatos", TestGame.Nouns.LabelOf(Row("shoes").Id)); // the clothes' feet are «los zapatos»
    }
}
