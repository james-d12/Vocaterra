using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Crafting;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.Items.Crafting;

/// <summary>The clothes' recipes at the workbench (I08a, inventory plan 3.13a): what each one takes, and a craft beside the table that makes the piece.</summary>
public sealed class ClothesRecipesTests
{
    public static TheoryData<string, string, (string Material, int Count)[]> Recipes => new()
    {
        { "sombrero", "hat", [("tela", 1)] },
        { "gorro", "cap", [("tela", 2)] },
        { "camisa", "shirt", [("tela", 2)] },
        { "chaqueta", "jacket", [("tela", 3), ("cuero", 1)] },
        { "abrigo", "coat", [("tela", 4), ("cuero", 2)] },
        { "pantalón", "trousers", [("tela", 2)] },
        { "zapato", "shoes", [("cuero", 1)] },
    };

    [Theory]
    [MemberData(nameof(Recipes))]
    public void Given_AClothesRecipe_When_ItIsRead_Then_ItIsMadeAtTheWorkbenchFromTheMaterialsOfTheList(string output, string item, (string Material, int Count)[] ingredients)
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

        Assert.True(session.Simulation.StationInReach(BlockId.Table));
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
    public void Given_TheMaterialsButNoTable_When_ThePieceIsCrafted_Then_NothingIsMade()
    {
        var session = TestGame.NewSession();
        var index = session.Recipes.IndexOf("camisa");
        _ = session.Simulation.GrantItems(TestItems.Catalogue.Find("cloth")!.Id, 2);

        Assert.False(session.Simulation.StationInReach(BlockId.Table));
        Assert.Equal(CraftResult.MissingStation, session.Simulation.CanCraft(index));
    }
}
