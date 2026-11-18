using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Crafting;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.Items.Crafting;

/// <summary>The armour's, the accessories', the off-hand's and the bags' recipes at the workbench (I08b, inventory plan 3.13a): what each one takes, and a craft beside the table that makes the piece.</summary>
public sealed class GearRecipesTests
{
    public static TheoryData<string, string, (string Material, int Count)[]> Recipes => new()
    {
        { "casco", "helmet", [("hierro", 2)] },
        { "coraza", "cuirass", [("hierro", 4)] },
        { "greba", "greaves", [("hierro", 3)] },
        { "bota", "boots", [("hierro", 2)] },
        { "anillo", "ring", [("hierro", 1)] },
        { "pulsera", "bracelet", [("hierro", 1)] },
        { "cinturón", "belt", [("cuero", 2)] },
        { "collar", "necklace", [("hierro", 1), ("cuero", 1)] },
        { "amuleto", "amulet", [("hierro", 1)] },
        { "escudo", "shield", [("madera", 2), ("hierro", 2)] },
        { "farol", "lantern", [("hierro", 1), ("antorcha", 1)] },
        { "saco", "sack", [("tela", 2)] },
        { "mochila", "backpack", [("tela", 3), ("cuero", 2)] },
    };

    [Theory]
    [MemberData(nameof(Recipes))]
    public void Given_AGearRecipe_When_ItIsRead_Then_ItIsMadeAtTheWorkbenchFromTheMaterialsOfTheList(string output, string item, (string Material, int Count)[] ingredients)
    {
        var entry = TestGame.NewSession().Recipes.ForOutput(output)!;

        Assert.Equal("mesa", entry.Station!.Lemma);
        Assert.Equal(BlockId.Table, entry.Rule.Station);
        Assert.Equal(TestItems.Catalogue.Find(item)!.Id, entry.Rule.Output);
        Assert.Equal(1, entry.OutputCount);
        Assert.Equal(ingredients, entry.Ingredients.Select(static i => (i.Noun.Lemma, i.Count)).ToArray());
    }

    [Theory]
    [MemberData(nameof(Recipes))]
    public void Given_TheMaterialsAndATableAtHand_When_ThePieceIsCrafted_Then_TheMaterialsAreUsedAndThePieceIsInTheBag(string output, string item, (string Material, int Count)[] ingredients)
    {
        var session = TestGame.NewSession();
        var feet = session.Simulation.Player.FeetBlock;
        session.Scene.Grid.SetBlock(feet.X + 1, feet.Y, feet.Z, BlockId.Table);
        var index = session.Recipes.IndexOf(output);
        var piece = TestItems.Catalogue.Find(item)!.Id;
        var held = new Dictionary<ItemId, int>();
        foreach (var (material, count) in ingredients)
        {
            var id = TestGame.Nouns.ForLemma(material)!.Items[0];
            _ = session.Simulation.GrantItems(id, count + 1); // one more than the recipe takes
            held[id] = count + 1;
        }

        Assert.Equal(CraftResult.Done, session.Simulation.CanCraft(index));
        session.Step([VoxelCommand.Craft(index)]);

        Assert.Equal(1, session.Simulation.Inventory.Count(piece));
        foreach (var (material, count) in ingredients)
        {
            var id = TestGame.Nouns.ForLemma(material)!.Items[0];
            Assert.Equal(held[id] - count, session.Simulation.Inventory.Count(id));
        }
    }

    [Fact]
    public void Given_TheIronButNoTable_When_TheHelmetIsCrafted_Then_NothingIsMade()
    {
        var session = TestGame.NewSession();
        var index = session.Recipes.IndexOf("casco");
        _ = session.Simulation.GrantItems(TestItems.Catalogue.Find("iron")!.Id, 2);

        Assert.Equal(CraftResult.MissingStation, session.Simulation.CanCraft(index));
    }

    [Fact]
    public void Given_TheIronAndNoTorch_When_TheLanternIsCrafted_Then_ItIsMissingTheTorch()
    {
        var session = TestGame.NewSession();
        var feet = session.Simulation.Player.FeetBlock;
        session.Scene.Grid.SetBlock(feet.X + 1, feet.Y, feet.Z, BlockId.Table);
        _ = session.Simulation.GrantItems(TestItems.Catalogue.Find("iron")!.Id, 3);

        Assert.NotEqual(CraftResult.Done, session.Simulation.CanCraft(session.Recipes.IndexOf("farol")));
    }
}
