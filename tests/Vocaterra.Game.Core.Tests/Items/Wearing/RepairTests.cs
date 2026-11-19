using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Crafting;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.Items.Wearing;

/// <summary>The repair rows at the workbench (I07): what each takes, when the panel shows them, and what a repair does to the item, the bag and the stats.</summary>
public sealed class RepairTests
{
    private static VoxelSession AtTheTable()
    {
        var session = TestGame.NewSession();
        var feet = session.Simulation.Player.FeetBlock;
        session.Scene.Grid.SetBlock(feet.X + 1, feet.Y, feet.Z, BlockId.Table);
        return session;
    }

    private static ItemId Item(string name) => TestItems.Catalogue.Find(name)!.Id;

    [Fact]
    public void Given_EveryDurableItemOfTheBundle_When_TheRecipesAreRead_Then_EachHasOneRepairRowAtTheWorkbenchWithOneMaterial()
    {
        var session = TestGame.NewSession();

        foreach (var item in TestItems.Catalogue.All.Where(static d => d.Durability is not null))
        {
            var lemma = TestGame.Nouns.ForItem(item.Id)!.Lemma;
            var index = session.Recipes.RepairIndexOf(lemma);
            Assert.True(index >= 0, $"{item.Name} has no repair row");
            var entry = session.Recipes.Entries[index];
            Assert.True(entry.Rule.Repairs);
            Assert.Equal(BlockId.Table, entry.Rule.Station);
            _ = Assert.Single(entry.Ingredients);
            Assert.Equal(item.Id, entry.Rule.Output);
        }
    }

    [Theory]
    [InlineData("hacha", "madera", 1)]
    [InlineData("sombrero", "tela", 1)]
    [InlineData("chaqueta", "tela", 2)]
    [InlineData("zapato", "cuero", 1)]
    [InlineData("cinturón", "cuero", 1)]
    [InlineData("casco", "hierro", 1)]
    [InlineData("coraza", "hierro", 2)]
    [InlineData("escudo", "hierro", 1)]
    [InlineData("farol", "hierro", 1)]
    [InlineData("mochila", "tela", 2)]
    public void Given_ARepairRow_When_ItIsRead_Then_ItUsesTheMaterialOfTheItsKind(string output, string material, int count)
    {
        var entry = TestGame.NewSession().Recipes.Entries[TestGame.NewSession().Recipes.RepairIndexOf(output)];

        Assert.Equal((material, count), (entry.Ingredients[0].Noun.Lemma, entry.Ingredients[0].Count));
    }

    [Fact]
    public void Given_ARepairRow_When_TheGuideLooksForHowToMakeTheItem_Then_ItIsNotAWayToMakeIt()
    {
        var book = TestGame.NewSession().Recipes;

        Assert.Null(book.ForOutput("hacha")); // the axe has no recipe: its repair row is not one
        Assert.False(book.Entries[book.IndexOf("sombrero")].Rule.Repairs);
        Assert.True(book.Entries[book.RepairIndexOf("sombrero")].Rule.Repairs);
    }

    [Fact]
    public void Given_NothingWorn_When_ThePanelRowsAreMade_Then_NoRepairRowIsShown()
    {
        var session = AtTheTable();

        var rows = CraftingScreen.Rows(session.Simulation, session.Recipes);

        Assert.DoesNotContain(rows, static r => r.Recipe.Rule.Repairs);
    }

    [Fact]
    public void Given_AWornAxeInTheBag_When_ThePanelRowsAreMade_Then_OnlyTheAxesRepairRowIsAdded()
    {
        var session = AtTheTable();
        Assert.Equal(7, session.Simulation.SetWear(TestItems.Axe, 7));

        var repairs = CraftingScreen.Rows(session.Simulation, session.Recipes).Where(static r => r.Recipe.Rule.Repairs).ToList();

        Assert.Equal(TestItems.Axe, Assert.Single(repairs).Recipe.Rule.Output);
        Assert.Equal(CraftResult.MissingIngredients, repairs[0].Result);
    }

    [Fact]
    public void Given_AWornAxeAndWoodAtTheTable_When_ItIsRepaired_Then_TheWearIsGoneAndOneWoodIsUsed()
    {
        var session = AtTheTable();
        _ = session.Simulation.SetWear(TestItems.Axe, 150);
        _ = session.Simulation.GrantItems(ItemId.Wood, 3);
        var index = session.Recipes.RepairIndexOf("hacha");
        Assert.Equal(CraftResult.Done, session.Simulation.CanCraft(index));

        session.Step([VoxelCommand.Craft(index)]);

        Assert.Equal(0, session.Simulation.Inventory.Slots.Single(static s => !s.IsEmpty && s.Item == TestItems.Axe).Wear);
        Assert.Equal(2, session.Simulation.Inventory.Count(ItemId.Wood));
        Assert.Equal(1, session.Simulation.Inventory.Count(TestItems.Axe));
        Assert.Contains(session.Simulation.Events, static e => e.Kind == SimulationEventKind.ItemRepaired);
    }

    [Fact]
    public void Given_AWornOutAxe_When_ItIsRepaired_Then_ItWorksAtTheAxesSpeedAgain()
    {
        var session = AtTheTable();
        _ = session.Simulation.SetWear(TestItems.Axe, 200);
        session.Simulation.Inventory.SetSlot(0, session.Simulation.Inventory.Slots.First(static s => !s.IsEmpty && s.Item == TestItems.Axe));
        session.Step([VoxelCommand.SelectSlot(0)]);
        Assert.Equal(BlockDefinition.TrunkGatherTicks, session.Simulation.GatherTicksOf(BlockId.Trunk));
        _ = session.Simulation.GrantItems(ItemId.Wood, 1);

        session.Step([VoxelCommand.Craft(session.Recipes.RepairIndexOf("hacha"))]);

        Assert.Equal(15, session.Simulation.GatherTicksOf(BlockId.Trunk));
    }

    [Fact]
    public void Given_WornOutClothesWorn_When_TheyAreRepaired_Then_TheyGiveTheirWarmthAgain()
    {
        var session = AtTheTable();
        var sim = session.Simulation;
        var shirt = Item("shirt");
        var before = sim.Stats.Sum("warmth");
        Assert.True(before > 0);
        Assert.Equal(240, sim.SetWear(shirt, 240));
        sim.RefreshStats();
        Assert.True(sim.Stats.Sum("warmth") < before);
        _ = sim.GrantItems(Item("cloth"), 1);

        session.Step([VoxelCommand.Craft(session.Recipes.RepairIndexOf("camisa"))]);

        Assert.Equal(before, sim.Stats.Sum("warmth"));
        Assert.Equal(0, sim.Equipment.Worn().Select(w => sim.Equipment.Get(w.Slot)).Single(s => s.Item == shirt).Wear);
    }

    [Fact]
    public void Given_TwoWornAxes_When_OneRepairIsDone_Then_TheMoreWornOneIsRepaired()
    {
        var session = AtTheTable();
        var sim = session.Simulation;
        sim.Inventory.SetSlot(12, new ItemStack(TestItems.Axe, 1, 40));
        sim.Inventory.SetSlot(13, new ItemStack(TestItems.Axe, 1, 90));
        _ = sim.GrantItems(ItemId.Wood, 1);

        session.Step([VoxelCommand.Craft(session.Recipes.RepairIndexOf("hacha"))]);

        Assert.Equal(40, sim.Inventory.Slots[12].Wear);
        Assert.Equal(0, sim.Inventory.Slots[13].Wear);
    }

    [Fact]
    public void Given_AnAxeWithNoWear_When_ItsRepairIsTried_Then_ThereIsNothingToRepair()
    {
        var session = AtTheTable();
        _ = session.Simulation.GrantItems(TestItems.Axe, 1);
        _ = session.Simulation.GrantItems(ItemId.Wood, 1);

        Assert.Equal(CraftResult.NothingToRepair, session.Simulation.CanCraft(session.Recipes.RepairIndexOf("hacha")));
    }

    [Fact]
    public void Given_AWornAxeButNoMaterial_When_ItsRepairIsTried_Then_TheMaterialIsMissingAndNothingChanges()
    {
        var session = AtTheTable();
        _ = session.Simulation.SetWear(TestItems.Axe, 9);

        Assert.Equal(CraftResult.MissingIngredients, session.Simulation.CanCraft(session.Recipes.RepairIndexOf("hacha")));
    }

    [Fact]
    public void Given_AWornAxeAndWoodButNoTable_When_ItsRepairIsTried_Then_TheStationIsMissing()
    {
        var session = TestGame.NewSession();
        _ = session.Simulation.SetWear(TestItems.Axe, 9);
        _ = session.Simulation.GrantItems(ItemId.Wood, 1);

        Assert.Equal(CraftResult.MissingStation, session.Simulation.CanCraft(session.Recipes.RepairIndexOf("hacha")));
    }

    [Fact]
    public void Given_ARepairRow_When_ItsStatusIsRead_Then_ItSaysRepairNotMake()
    {
        var session = AtTheTable();
        _ = session.Simulation.SetWear(TestItems.Axe, 9);
        _ = session.Simulation.GrantItems(ItemId.Wood, 1);

        var row = CraftingScreen.Rows(session.Simulation, session.Recipes).Single(static r => r.Recipe.Rule.Repairs);

        Assert.Equal("Repair", row.Status(TestGame.Locale));
    }

    [Fact]
    public void Given_ARepair_When_ItIsDone_Then_ItemRepairedIsLogged()
    {
        var session = AtTheTable();
        _ = session.Simulation.SetWear(TestItems.Axe, 9);
        _ = session.Simulation.GrantItems(ItemId.Wood, 1);

        session.Step([VoxelCommand.Craft(session.Recipes.RepairIndexOf("hacha"))]);

        Assert.Contains(session.Events.OfType<WorldEvent>(), static e => e.Name == WorldEvents.ItemRepaired);
    }
}
