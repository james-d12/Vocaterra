using Vocaterra.Simulation.Crafting;
using Vocaterra.Simulation.Items;
using Vocaterra.Simulation.Tiles;

namespace Vocaterra.Simulation.Tests;

/// <summary>The crafting rule: ingredients, stations in reach, a full inventory and the command, on hand-built worlds and plain recipe values.</summary>
public sealed class CraftingTests
{
    private const int PlayerColumn = 20;
    private const int GroundRow = TestWorlds.GroundRow;

    private static readonly Recipe Table = new(ItemId.Table, 1, [new Ingredient(ItemId.Wood, 10)], null);
    private static readonly Recipe Door = new(ItemId.Door, 1, [new Ingredient(ItemId.Wood, 6)], TileId.Table);
    private static readonly Recipe Sword = new(ItemId.Sword, 1, [new Ingredient(ItemId.Wood, 2), new Ingredient(ItemId.Stone, 6)], TileId.Table);
    private static readonly Recipe Glass = new(ItemId.Glass, 1, [new Ingredient(ItemId.Sand, 2)], TileId.Furnace);

    private static GameSimulation NewSim(Action<TileGrid>? customise = null)
    {
        var sim = TestWorlds.StandingAt(PlayerColumn, customise);
        sim.SetRecipes([Table, Door, Sword, Glass]);
        return sim;
    }

    [Fact]
    public void ACraftByHandTakesTheIngredientsAndGivesTheOutput()
    {
        var sim = NewSim();
        sim.GrantItems(ItemId.Wood, 12);

        Assert.Equal(CraftResult.Done, sim.CanCraft(0));
        Assert.Equal(CraftResult.Done, sim.Craft(0));

        Assert.Equal(2, sim.Inventory.Count(ItemId.Wood));
        Assert.Equal(1, sim.Inventory.Count(ItemId.Table));
        Assert.Contains(sim.Events, e => e.Kind == SimulationEventKind.ItemCrafted);
    }

    [Fact]
    public void MissingIngredientsChangeNothing()
    {
        var sim = NewSim();
        sim.GrantItems(ItemId.Wood, 9);
        var before = sim.ComputeStateDigest();

        Assert.Equal(CraftResult.MissingIngredients, sim.CanCraft(0));
        Assert.Equal(CraftResult.MissingIngredients, sim.Craft(0));

        Assert.Equal(before, sim.ComputeStateDigest());
        Assert.Equal(9, sim.Inventory.Count(ItemId.Wood));
        Assert.Equal(0, sim.Inventory.Count(ItemId.Table));
    }

    [Fact]
    public void EveryIngredientOfARecipeWithTwoIsNeeded()
    {
        var sim = NewSim(grid => grid.SetTile(PlayerColumn + 2, GroundRow - 1, TileId.Table));
        sim.GrantItems(ItemId.Wood, 2);
        sim.GrantItems(ItemId.Stone, 5);
        Assert.Equal(CraftResult.MissingIngredients, sim.CanCraft(2));

        sim.GrantItems(ItemId.Stone, 1);
        Assert.Equal(CraftResult.Done, sim.Craft(2));
        Assert.Equal(0, sim.Inventory.Count(ItemId.Wood));
        Assert.Equal(0, sim.Inventory.Count(ItemId.Stone));
        Assert.Equal(1, sim.Inventory.Count(ItemId.Sword));
    }

    [Fact]
    public void ARecipeWithAStationNeedsTheStationTileWithinReach()
    {
        var withoutStation = NewSim();
        withoutStation.GrantItems(ItemId.Wood, 6);
        Assert.Equal(CraftResult.MissingStation, withoutStation.CanCraft(1));
        Assert.Equal(CraftResult.MissingStation, withoutStation.Craft(1));
        Assert.Equal(6, withoutStation.Inventory.Count(ItemId.Wood));

        var near = NewSim(grid => grid.SetTile(PlayerColumn + 3, GroundRow - 1, TileId.Table));
        near.GrantItems(ItemId.Wood, 6);
        Assert.True(near.StationInReach(TileId.Table));
        Assert.Equal(CraftResult.Done, near.Craft(1));
        Assert.Equal(1, near.Inventory.Count(ItemId.Door));
        Assert.Equal(0, near.Inventory.Count(ItemId.Wood));
    }

    [Fact]
    public void TheStationReachIsOneNumberAndAStationBeyondItDoesNotCount()
    {
        var reach = CraftingRules.StationReachInTiles;
        var inside = NewSim(grid => grid.SetTile(PlayerColumn + reach - 1, GroundRow - 1, TileId.Table));
        var outside = NewSim(grid => grid.SetTile(PlayerColumn + reach + 2, GroundRow - 1, TileId.Table));

        Assert.True(inside.StationInReach(TileId.Table));
        Assert.False(outside.StationInReach(TileId.Table));
    }

    [Fact]
    public void TheWrongStationDoesNotCount()
    {
        var sim = NewSim(grid => grid.SetTile(PlayerColumn + 1, GroundRow - 1, TileId.Table));
        sim.GrantItems(ItemId.Sand, 2);

        Assert.False(sim.StationInReach(TileId.Furnace));
        Assert.Equal(CraftResult.MissingStation, sim.CanCraft(3));
    }

    [Fact]
    public void AFurnaceInReachMakesGlass()
    {
        var sim = NewSim(grid => grid.SetTile(PlayerColumn - 2, GroundRow - 1, TileId.Furnace));
        sim.GrantItems(ItemId.Sand, 5);

        Assert.Equal(CraftResult.Done, sim.Craft(3));

        Assert.Equal(3, sim.Inventory.Count(ItemId.Sand));
        Assert.Equal(1, sim.Inventory.Count(ItemId.Glass));
    }

    [Fact]
    public void AFullInventoryStillCraftsWhenTheIngredientsFreeASlot()
    {
        var sim = NewSim();
        for (var slot = 0; slot < Inventory.SlotCount; slot++)
        {
            sim.Inventory.SetSlot(slot, new ItemStack(ItemId.Dirt, 99));
        }

        sim.Inventory.SetSlot(7, new ItemStack(ItemId.Wood, 10));

        // The table needs the ten wood: taking them empties slot 7, so the table fits there.
        Assert.Equal(CraftResult.Done, sim.Craft(0));
        Assert.Equal(new ItemStack(ItemId.Table, 1), sim.Inventory.Slots[7]);
    }

    [Fact]
    public void AnOutputThatCannotFitIsRefusedAndNothingChanges()
    {
        var sim = NewSim();
        for (var slot = 0; slot < Inventory.SlotCount; slot++)
        {
            sim.Inventory.SetSlot(slot, new ItemStack(ItemId.Dirt, 99));
        }

        sim.Inventory.SetSlot(0, new ItemStack(ItemId.Wood, 99));
        // Ten wood come out of a stack of 99, which leaves 89 in slot 0: no slot is freed and none is empty.
        var before = sim.ComputeStateDigest();

        Assert.Equal(CraftResult.InventoryFull, sim.CanCraft(0));
        Assert.Equal(CraftResult.InventoryFull, sim.Craft(0));
        Assert.Equal(before, sim.ComputeStateDigest());
    }

    [Fact]
    public void ANumberOutsideTheRecipeListIsNoRecipe()
    {
        var sim = NewSim();

        Assert.Equal(CraftResult.NoSuchRecipe, sim.CanCraft(-1));
        Assert.Equal(CraftResult.NoSuchRecipe, sim.Craft(4));
        Assert.Equal(CraftResult.NoSuchRecipe, TestWorlds.StandingAt(PlayerColumn).CanCraft(0));
    }

    [Fact]
    public void TheCraftCommandCraftsOncePerCommandAfterTheMiningAndPlacing()
    {
        var sim = NewSim();
        sim.GrantItems(ItemId.Wood, 25);

        sim.Step([PlayerCommand.Craft(0), PlayerCommand.Craft(0)]);

        Assert.Equal(5, sim.Inventory.Count(ItemId.Wood));
        Assert.Equal(2, sim.Inventory.Count(ItemId.Table));
        Assert.Equal(2, sim.Events.Count(e => e.Kind == SimulationEventKind.ItemCrafted));

        sim.Step([PlayerCommand.Craft(9)]);
        Assert.Empty(sim.Events);
    }

    [Fact]
    public void TheRecipesAreRulesAndNotStateAndTheSameCommandsGiveTheSameDigest()
    {
        var a = NewSim();
        var b = TestWorlds.StandingAt(PlayerColumn);
        Assert.Equal(b.ComputeStateDigest(), a.ComputeStateDigest());

        foreach (var sim in new[] { a, b })
        {
            sim.SetRecipes([Table]);
            sim.GrantItems(ItemId.Wood, 10);
            sim.Step([PlayerCommand.Craft(0)]);
        }

        Assert.Equal(b.ComputeStateDigest(), a.ComputeStateDigest());
    }

    [Fact]
    public void ThePureRuleWorksOnAnInventoryAlone()
    {
        var inventory = new Inventory();
        inventory.Add(ItemId.Sand, 3);

        Assert.Equal(CraftResult.MissingStation, CraftingRules.Check(Glass, inventory, stationInReach: false));
        Assert.Equal(CraftResult.Done, CraftingRules.Craft(Glass, inventory, stationInReach: true));
        Assert.Equal(1, inventory.Count(ItemId.Sand));
        Assert.Equal(1, inventory.Count(ItemId.Glass));
        Assert.Equal(CraftResult.MissingIngredients, CraftingRules.Craft(Glass, inventory, stationInReach: true));
    }

    [Fact]
    public void TheNewTilesAndItemsAreDefinedAndPlaceable()
    {
        foreach (var (item, tile) in new[] { (ItemId.Furnace, TileId.Furnace), (ItemId.Glass, TileId.Glass), (ItemId.Brick, TileId.Brick) })
        {
            Assert.Equal(tile, ItemDefinition.Get(item).PlacesTile);
            Assert.Equal(item, TileDefinition.Get(tile).Drop);
            Assert.True(TileDefinition.IsSolid(tile));
        }

        Assert.Equal(ItemDefinition.ToolStackLimit, ItemDefinition.Get(ItemId.Sword).MaxStack);
        Assert.Null(ItemDefinition.Get(ItemId.Sword).PlacesTile);
        Assert.Equal(18, TileDefinition.All.Count);
    }
}
