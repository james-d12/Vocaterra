using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Stats;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Items;

/// <summary>The three materials and the seven pieces of clothing the bundle ships (I08a, inventory plan 3.13a): their rows, their warmth, their looks and their words.</summary>
public sealed class ClothesAndMaterialsTests
{
    private static ItemDefinition Row(string name) => TestItems.Catalogue.Find(name) ?? throw new InvalidOperationException($"No item row {name}.");

    [Theory]
    [InlineData("cloth", 100)]
    [InlineData("leather", 101)]
    [InlineData("iron", 102)]
    public void Given_AMaterial_When_ItsRowIsRead_Then_ItStacksTo99WearsNothingAndHasItsOwnIcon(string name, int id)
    {
        var row = Row(name);

        Assert.Equal((ItemId)id, row.Id);
        Assert.Equal(99, row.MaxStack);
        Assert.Equal(["material"], row.Tags);
        Assert.Null(row.Wear);
        Assert.Null(row.Stats);
        Assert.Null(row.Look);
        Assert.Equal(name, row.Icon);
    }

    [Theory]
    [InlineData("hat", 103, "head", 10, new[] { "head" })]
    [InlineData("cap", 104, "head", 20, new[] { "head" })]
    [InlineData("shirt", 105, "body", 10, new[] { "body", "sleeve" })]
    [InlineData("jacket", 106, "body", 30, new[] { "body", "sleeve" })]
    [InlineData("coat", 107, "body", 50, new[] { "body", "sleeve" })]
    [InlineData("trousers", 108, "legs", 20, new[] { "legs" })]
    [InlineData("shoes", 109, "feet", 10, new[] { "feet" })]
    public void Given_APieceOfClothing_When_ItsRowIsRead_Then_ItHasItsSlotItsWarmthInTenthsAndALookColourPerCoveredPart(string name, int id, string wear, int tenths, string[] parts)
    {
        var row = Row(name);

        Assert.Equal((ItemId)id, row.Id);
        Assert.Equal(1, row.MaxStack);
        Assert.Equal(["clothing"], row.Tags);
        Assert.Equal(wear, row.Wear);
        Assert.Equal([(StatKeys.Warmth, (double)tenths)], row.Stats!.Select(static p => (p.Key, p.Value)));
        Assert.Equal(parts.Order(StringComparer.Ordinal), row.Look!.Parts.Keys);
        Assert.Equal(name, row.Icon);
    }

    [Fact]
    public void Given_TheClothes_When_TheirWarmthIsAddedUp_Then_TheWarmestSetGivesTenDegreesAndTheStartingSetFour()
    {
        var warmth = (string name) => Row(name).Stats![StatKeys.Warmth] / 10.0;

        Assert.Equal(10.0, warmth("cap") + warmth("coat") + warmth("trousers") + warmth("shoes"));
        Assert.Equal(4.0, warmth("shirt") + warmth("trousers") + warmth("shoes"));
    }

    [Fact]
    public void Given_TheClothes_When_TheirLooksAreCompared_Then_NoTwoPiecesOfOneSlotShareAColour()
    {
        var clothes = TestItems.Catalogue.All.Where(static d => d.Tags.Contains("clothing")).ToList();

        Assert.Equal(7, clothes.Count);
        foreach (var slot in clothes.GroupBy(static d => d.Wear))
        {
            var colours = slot.Select(static d => d.Look!.Parts.Values.First().ToString()).ToList();
            Assert.Equal(colours.Count, colours.Distinct().Count());
        }
    }

    [Theory]
    [InlineData("tela", "f", "cloth")]
    [InlineData("cuero", "m", "leather")]
    [InlineData("hierro", "m", "iron")]
    [InlineData("sombrero", "m", "hat")]
    [InlineData("gorro", "m", "cap")]
    [InlineData("camisa", "f", "shirt")]
    [InlineData("chaqueta", "f", "jacket")]
    [InlineData("abrigo", "m", "coat")]
    [InlineData("pantalón", "m", "trousers")]
    [InlineData("zapato", "m", "shoes")]
    public void Given_AnItem_When_ItsNounIsLooked_Then_ItIsACatalogueNounOfTheRightGenderNamingIt(string lemma, string gender, string item)
    {
        var noun = TestGame.Nouns.ForLemma(lemma)!;

        Assert.Equal(gender, noun.Gender);
        Assert.Equal([Row(item).Id], noun.Items);
        Assert.Same(noun, TestGame.Nouns.ForItem(Row(item).Id));
    }

    [Theory]
    [InlineData("pantalón", "los pantalones", "unos pantalones", "pantalones")]
    [InlineData("zapato", "los zapatos", "unos zapatos", "zapatos")]
    public void Given_ANounSaidInThePlural_When_ItsArticlesAreMade_Then_TheyAreThePluralOnesAndTheLabelIsThePlural(string lemma, string definite, string indefinite, string label)
    {
        var noun = TestGame.Nouns.ForLemma(lemma)!;

        Assert.True(noun.PluralOnly);
        Assert.Equal(definite, noun.WithDefinite);
        Assert.Equal(indefinite, noun.WithIndefinite);
        Assert.Equal(label, TestGame.Nouns.LabelOf(noun.Items[0]));
    }

    [Theory]
    [InlineData("camisa", "la camisa", "una camisa")]
    [InlineData("tela", "la tela", "una tela")]
    [InlineData("cuero", "el cuero", "un cuero")]
    [InlineData("gorro", "el gorro", "un gorro")]
    public void Given_ASingularNoun_When_ItsArticlesAreMade_Then_TheyAreTheSingularOnes(string lemma, string definite, string indefinite)
    {
        var noun = TestGame.Nouns.ForLemma(lemma)!;

        Assert.False(noun.PluralOnly);
        Assert.Equal(definite, noun.WithDefinite);
        Assert.Equal(indefinite, noun.WithIndefinite);
    }

    [Fact]
    public void Given_TheMaterials_When_TheirNounsAreRead_Then_TheyAreMassNouns()
    {
        Assert.All(new[] { "tela", "cuero", "hierro" }, lemma => Assert.True(TestGame.Nouns.ForLemma(lemma)!.Mass, lemma));
        Assert.False(TestGame.Nouns.ForLemma("camisa")!.Mass);
    }
}
