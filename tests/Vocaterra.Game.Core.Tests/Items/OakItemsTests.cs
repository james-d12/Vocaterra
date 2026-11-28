using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Crafting;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.Items;

/// <summary>The seven oak pieces as items (K05, building plan 3.7): their rows, their nouns and gender, their recipes at the table and the colour rule. Nothing places them yet (K06).</summary>
public sealed class OakItemsTests
{
    // item name, id, noun lemma, gender, the block names the noun lists, the recipe (output count, planks)
    public static TheoryData<string, int, string, string, string[], int, int> Pieces => new()
    {
        { "oak-log", 123, "tronco", "m", ["oak-log"], 1, 4 },
        { "oak-stairs", 124, "escalera", "f", ["oak-stairs"], 4, 6 },
        { "oak-slab", 125, "losa", "f", ["oak-slab"], 6, 3 },
        { "oak-fence", 126, "valla", "f", ["fence", "oak-fence"], 3, 4 },
        { "oak-fence-gate", 127, "portón", "m", ["oak-fence-gate"], 1, 4 },
        { "oak-door", 128, "puerta", "f", ["door", "oak-door"], 3, 6 },
        { "oak-trapdoor", 129, "trampilla", "f", ["oak-trapdoor"], 2, 6 },
    };

    private static ItemId Wood => TestItems.Catalogue.Find("wood")!.Id;

    [Theory]
    [MemberData(nameof(Pieces))]
    public void Given_AnOakPiece_When_ItsRowIsRead_Then_ItStacksLikeWoodIsPlaceableAndHasItsOwnIcon(string name, int id, string lemma, string gender, string[] blocks, int count, int planks)
    {
        _ = (lemma, gender, blocks, count, planks);
        var row = TestItems.Catalogue.Find(name)!;
        var wood = TestItems.Catalogue.Find("wood")!;

        Assert.Equal((ItemId)id, row.Id);
        Assert.Equal(wood.MaxStack, row.MaxStack);
        Assert.Equal(["placeable"], row.Tags);
        Assert.Equal(name, row.Icon);
        Assert.Null(row.Wear);
        Assert.Null(row.Tool);
    }

    [Theory]
    [MemberData(nameof(Pieces))]
    public void Given_AnOakPiece_When_ItsNounIsRead_Then_ItNamesTheItemAndTheBlocksWithItsGender(string name, int id, string lemma, string gender, string[] blocks, int count, int planks)
    {
        _ = (count, planks);
        var noun = TestGame.Nouns.ForLemma(lemma)!;

        Assert.Equal(gender, noun.Gender);
        Assert.Contains((ItemId)id, noun.Items);
        Assert.Equal(noun, TestGame.Nouns.ForItem((ItemId)id));
        Assert.Contains(TestGame.Nouns.Nouns, n => n.Lemma == lemma);
        var expected = blocks.Select(static b => BlockDefinition.All.Single(d => d.Name == b).Id).ToArray();
        Assert.Equal(expected, noun.Blocks);
        Assert.Contains(TestGame.Bundle.Lexicon.EntriesForLemma(lemma), e => e.Pos == "noun" && e.Features.Gender == gender);
        _ = name;
    }

    [Fact]
    public void Given_TheTreeTrunksAndTheSceneFence_When_TheirNounsAreRead_Then_TheLogNamesOnlyTheOakLogAndTheFenceNounAlsoNamesTheScenesFence()
    {
        Assert.Equal([BlockId.OakLog], TestGame.Nouns.ForLemma("tronco")!.Blocks);
        Assert.DoesNotContain(BlockId.Trunk, TestGame.Nouns.ForLemma("tronco")!.Blocks);
        Assert.Contains(BlockId.Trunk, TestGame.Nouns.ForLemma("árbol")!.Blocks);
        Assert.Equal([BlockId.Fence, BlockId.OakFence], TestGame.Nouns.ForLemma("valla")!.Blocks);
        Assert.Equal("valla", TestGame.Nouns.ForBlock(BlockId.Fence)!.Lemma);
        Assert.Equal("puerta", TestGame.Nouns.ForBlock(BlockId.OakDoor)!.Lemma);
        Assert.Equal("puerta", TestGame.Nouns.ForBlock(BlockId.Door)!.Lemma);
        Assert.Equal([ItemId.Wood], TestGame.Nouns.ForLemma("madera")!.Items); // planks stay the wood item
    }

    [Theory]
    [MemberData(nameof(Pieces))]
    public void Given_AnOakRecipe_When_ItIsRead_Then_ItIsMadeAtTheTableFromPlanksOnly(string name, int id, string lemma, string gender, string[] blocks, int count, int planks)
    {
        _ = (name, gender, blocks);
        var entry = TestGame.NewSession().Recipes.ForOutput(lemma)!;

        Assert.Equal("mesa", entry.Station!.Lemma);
        Assert.Equal(BlockId.Table, entry.Rule.Station);
        Assert.Equal((ItemId)id, entry.Rule.Output);
        Assert.Equal(count, entry.OutputCount);
        Assert.Equal([("madera", planks)], entry.Ingredients.Select(static i => (i.Noun.Lemma, i.Count)).ToArray());
    }

    [Theory]
    [MemberData(nameof(Pieces))]
    public void Given_PlanksAndATableAtHand_When_ThePieceIsCrafted_Then_ThePlanksAreUsedAndThePiecesAreInTheBag(string name, int id, string lemma, string gender, string[] blocks, int count, int planks)
    {
        _ = (name, gender, blocks);
        var session = TestGame.NewSession();
        var feet = session.Simulation.Player.FeetBlock;
        session.Scene.Grid.SetBlock(feet.X + 1, feet.Y, feet.Z, BlockId.Table);
        var index = session.Recipes.IndexOf(lemma);
        var before = session.Simulation.Inventory.Count(Wood);
        _ = session.Simulation.GrantItems(Wood, planks + 1);

        Assert.True(index >= 0, $"The crafting list has no row for {lemma}.");
        Assert.Contains(session.Recipes.Entries, e => e.Rule.Output == (ItemId)id);
        Assert.Equal(CraftResult.Done, session.Simulation.CanCraft(index));
        session.Step([VoxelCommand.Craft(index)]);

        Assert.Equal(count, session.Simulation.Inventory.Count((ItemId)id));
        Assert.Equal(before + 1, session.Simulation.Inventory.Count(Wood));
    }

    [Fact]
    public void Given_TooFewPlanks_When_AnOakPieceIsCrafted_Then_NothingIsMade()
    {
        var session = TestGame.NewSession();
        var feet = session.Simulation.Player.FeetBlock;
        session.Scene.Grid.SetBlock(feet.X + 1, feet.Y, feet.Z, BlockId.Table);
        var index = session.Recipes.IndexOf("losa");
        _ = session.Simulation.GrantItems(Wood, 2);

        Assert.NotEqual(CraftResult.Done, session.Simulation.CanCraft(index));
    }

    [Fact]
    public void Given_AnOakPieceWhoseWordIsNotKnown_When_ItsColourIsAsked_Then_ItIsGreyAndTheSlabKnownIsInFullColour()
    {
        var words = new WordProgress(TestGame.Nouns);
        var slab = TestItems.Catalogue.Find("oak-slab")!.Id;
        var log = TestItems.Catalogue.Find("oak-log")!.Id;

        Assert.Equal(0.0, words.SaturationOf(slab));
        Assert.Equal(0.0, words.SaturationOf(log));

        words.Restore([new WordRecord("losa", CreditUnits: WordProgress.UnitsToKnow)]);

        Assert.Equal(1.0, words.SaturationOf(slab));
        Assert.Equal(0.0, words.SaturationOf(log)); // one word, one colour: the log's word is still unknown
    }
}
