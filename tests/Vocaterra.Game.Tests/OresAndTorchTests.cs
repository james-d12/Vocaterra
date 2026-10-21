using Vocaterra.Simulation.Crafting;
using Vocaterra.Simulation.Items;
using Vocaterra.Simulation.Tiles;

namespace Vocaterra.Game.Tests;

/// <summary>P7 in the game layer: the ores and the torch in the catalogue and the world map, the metal and torch recipes, and the guide's answers about ores.</summary>
public sealed class OresAndTorchTests
{
    private static RecipeBook Book => RecipeBook.Load(TestGame.Bundle, TestGame.Nouns);

    public static TheoryData<string, TileId, ItemId> OreNouns => new()
    {
        { "carbón", TileId.CoalOre, ItemId.Coal },
        { "cobre", TileId.CopperOre, ItemId.Copper },
        { "hierro", TileId.IronOre, ItemId.Iron },
        { "oro", TileId.GoldOre, ItemId.Gold },
    };

    [Theory]
    [MemberData(nameof(OreNouns))]
    public void EachOreIsACatalogueNounThatDropsItsItemAndIsFoundUnderground(string lemma, TileId ore, ItemId item)
    {
        var noun = TestGame.Nouns.ForLemma(lemma);
        Assert.NotNull(noun);
        Assert.True(noun.Mass);
        Assert.True(noun.Deep);
        Assert.Equal(lemma, Assert.Single(noun.Sources));
        Assert.Equal(lemma, TestGame.Nouns.ForTile(ore)?.Lemma);
        Assert.Equal(lemma, TestGame.Nouns.ForItem(item)?.Lemma);
        Assert.Equal(lemma, WorldFacts.NounOfTile(ore));
        Assert.Equal([ore], WorldFacts.TilesOfNoun(lemma));
    }

    [Fact]
    public void TheTorchAndTheLightAndTheMetalItemsHaveNounsInTheCatalogue()
    {
        Assert.Equal("antorcha", TestGame.Nouns.ForTile(TileId.Torch)?.Lemma);
        Assert.Equal("antorcha", TestGame.Nouns.ForItem(ItemId.Torch)?.Lemma);
        Assert.Equal("lámpara", TestGame.Nouns.ForItem(ItemId.Lamp)?.Lemma);
        Assert.Equal("cubo", TestGame.Nouns.ForItem(ItemId.Bucket)?.Lemma);
        Assert.Equal("anillo", TestGame.Nouns.ForItem(ItemId.Ring)?.Lemma);
        Assert.Equal("hierba", TestGame.Nouns.ForTile(TileId.TallGrass)?.Lemma);
        Assert.Equal("hierba", WorldFacts.NounOfTile(TileId.TallGrass));
        Assert.Equal("f", TestGame.Nouns.ForLemma("antorcha")!.Gender);
    }

    [Fact]
    public void TheTorchIsCraftedByHandFromWoodAndCoalForFour()
    {
        var torch = Book.ForOutput("antorcha");
        Assert.NotNull(torch);
        Assert.Equal(4, torch.OutputCount);
        Assert.Null(torch.Station);
        Assert.Equal(2, torch.Ingredients.Count);
        Assert.Equal(1, torch.Ingredients.Single(i => i.Noun.Lemma == "madera").Count);
        Assert.Equal(1, torch.Ingredients.Single(i => i.Noun.Lemma == "carbón").Count);
        Assert.Equal(ItemId.Torch, torch.Output.Items[0]);
    }

    [Theory]
    [InlineData("lámpara", "mesa", 1)]
    [InlineData("cubo", "horno", 3)]
    [InlineData("anillo", "horno", 2)]
    public void TheMetalRecipesNeedTheirStationAndTheirOre(string output, string station, int ironOrGold)
    {
        var recipe = Book.ForOutput(output);
        Assert.NotNull(recipe);
        Assert.Equal(station, recipe.Station?.Lemma);
        Assert.Equal(1, recipe.OutputCount);
        var ingredient = recipe.Ingredients.First(i => i.Noun.Lemma is "hierro" or "oro");
        Assert.Equal(ironOrGold, ingredient.Count);
    }

    [Fact]
    public void TheLampTakesIronAndGlassAtTheTable()
    {
        var lamp = Book.ForOutput("lámpara");
        Assert.NotNull(lamp);
        Assert.Equal(["hierro", "cristal"], lamp.Ingredients.Select(i => i.Noun.Lemma));
    }

    [Fact]
    public void TheGuideFindsCoalWithAnArrowOrSaysItIsDeepWhenNoneIsNear()
    {
        var session = TestGame.NewSession();
        var turn = session.Say("¿dónde hay carbón?");

        Assert.Equal("ask_find", turn.Intent);
        Assert.Equal("carbón", turn.Slots["material"]);
        Assert.NotNull(turn.Pointer);
        Assert.Equal("carbón", turn.Pointer.Place);
        Assert.Contains(turn.Pointer.Direction, turn.WorldLine, StringComparison.Ordinal);
        var nearest = TestGame.BruteForceNearest(session.Simulation.World, session.PlayerTile, TileId.CoalOre);
        Assert.Equal(nearest, turn.Pointer.Target);
    }

    [Fact]
    public void TheGuideSaysItIsVeryDeepForGoldThatIsNotInTheSearch()
    {
        var session = TestGame.NewSession();
        var turn = session.Say("¿dónde hay oro?");

        Assert.Equal("ask_find", turn.Intent);
        Assert.Equal("Está muy abajo.", turn.WorldLine);
        Assert.Null(turn.Pointer);
    }

    [Fact]
    public void ThePlayerWhoCarriesCoalIsToldSo()
    {
        var session = TestGame.NewSession();
        session.Simulation.GrantItems(ItemId.Coal, 2);
        var turn = session.Say("¿dónde hay carbón?");

        Assert.Equal("Tienes dos bloques de carbón.", turn.WorldLine);
        Assert.Null(turn.Pointer);
    }
}
