using Vocaterra.Simulation.Generation;
using Vocaterra.Simulation.Items;
using Vocaterra.Simulation.Tiles;

namespace Vocaterra.Simulation.Tests;

/// <summary>Mining a trunk tile fells the tree above it: its trunk and its canopy go, one wood per trunk tile.</summary>
public sealed class FellingTests
{
    private const int PlayerColumn = 20;
    private const int GroundRow = TestWorlds.GroundRow;

    // A tree on the flat world: trunk of the given height standing on the tile above the ground row, with the forest pass's canopy.
    private static void Plant(TileGrid grid, int x, int trunkHeight) => ForestPass.PlantTree(grid, x, GroundRow, trunkHeight);

    private static void Mine(GameSimulation sim, int x, int y)
    {
        var ticks = TileDefinition.Get(TileId.Trunk).MiningTicks;
        for (var i = 0; i < ticks; i++)
        {
            sim.Step([PlayerCommand.Mine(x, y)]);
        }
    }

    private static int Count(GameSimulation sim, TileId kind, int fromX, int toX)
    {
        var total = 0;
        for (var x = fromX; x <= toX; x++)
        {
            for (var y = 0; y < GroundRow; y++)
            {
                total += sim.World.GetTile(x, y) == kind ? 1 : 0;
            }
        }

        return total;
    }

    [Fact]
    public void MiningTheBaseOfATreeFellsTheWholeTreeAndGivesOneWoodPerTrunkTile()
    {
        var sim = TestWorlds.StandingAt(PlayerColumn, grid => Plant(grid, 23, 6));
        Assert.Equal(6, Count(sim, TileId.Trunk, 23, 23));
        Assert.True(Count(sim, TileId.Leaves, 20, 26) > 0);

        Mine(sim, 23, GroundRow - 1);

        Assert.Equal(0, Count(sim, TileId.Trunk, 0, 60));
        Assert.Equal(0, Count(sim, TileId.Leaves, 0, 60));
        Assert.Equal(6, sim.Inventory.Count(ItemId.Wood));
        Assert.Contains(sim.Events, e => e.Kind == SimulationEventKind.TileMined);
    }

    [Fact]
    public void MiningAMiddleTileFellsOnlyWhatIsAboveItAndLeavesTheStump()
    {
        var sim = TestWorlds.StandingAt(PlayerColumn, grid => Plant(grid, 23, 6));

        Mine(sim, 23, GroundRow - 3); // two tiles above the base: the lowest two stay

        Assert.Equal(2, Count(sim, TileId.Trunk, 23, 23));
        Assert.Equal(TileId.Trunk, sim.World.GetTile(23, GroundRow - 1));
        Assert.Equal(TileId.Trunk, sim.World.GetTile(23, GroundRow - 2));
        Assert.Equal(0, Count(sim, TileId.Leaves, 0, 60));
        Assert.Equal(4, sim.Inventory.Count(ItemId.Wood)); // the mined tile and the three above it
    }

    [Fact]
    public void ANeighbouringTreeIsLeftStanding()
    {
        var sim = TestWorlds.StandingAt(PlayerColumn, grid =>
        {
            Plant(grid, 23, 5);
            Plant(grid, 30, 7);
        });
        var neighbourLeaves = Count(sim, TileId.Leaves, 28, 32);

        Mine(sim, 23, GroundRow - 1);

        Assert.Equal(7, Count(sim, TileId.Trunk, 30, 30));
        Assert.Equal(neighbourLeaves, Count(sim, TileId.Leaves, 28, 32));
        Assert.Equal(5, sim.Inventory.Count(ItemId.Wood));
    }

    [Fact]
    public void FellingIsDeterministic()
    {
        var a = TestWorlds.StandingAt(PlayerColumn, grid => Plant(grid, 23, 6));
        var b = TestWorlds.StandingAt(PlayerColumn, grid => Plant(grid, 23, 6));

        Mine(a, 23, GroundRow - 1);
        Mine(b, 23, GroundRow - 1);

        Assert.Equal(a.ComputeStateDigest(), b.ComputeStateDigest());
    }

    [Fact]
    public void AFullInventoryHoldsTheFellingBackAndTheTreeStands()
    {
        var sim = TestWorlds.StandingAt(PlayerColumn, grid => Plant(grid, 23, 6));
        for (var slot = 2; slot < Inventory.SlotCount; slot++) // the axe stays in slot 0
        {
            sim.Inventory.SetSlot(slot, new ItemStack(ItemId.Dirt, 99));
        }

        sim.Inventory.SetSlot(2, new ItemStack(ItemId.Wood, 95)); // room for four more, not six

        Mine(sim, 23, GroundRow - 1);

        Assert.Equal(6, Count(sim, TileId.Trunk, 23, 23));
        Assert.Equal(95, sim.Inventory.Count(ItemId.Wood));
    }

    [Fact]
    public void ALoneTrunkTileStillDropsOneWood()
    {
        var sim = TestWorlds.StandingAt(PlayerColumn, grid => grid.SetTile(23, GroundRow - 2, TileId.Trunk));

        Mine(sim, 23, GroundRow - 2);

        Assert.Equal(1, sim.Inventory.Count(ItemId.Wood));
    }
}
