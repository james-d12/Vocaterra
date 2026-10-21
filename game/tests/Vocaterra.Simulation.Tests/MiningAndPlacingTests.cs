using Vocaterra.Simulation.Items;
using Vocaterra.Simulation.Tiles;

namespace Vocaterra.Simulation.Tests;

/// <summary>Mining (time, tools, drops, reach, progress) and placing (reach, overlap, the selected slot) on hand-built worlds.</summary>
public sealed class MiningAndPlacingTests
{
    // The player stands on the stone row 64 at column 20 (its box covers columns 19 to 21 and rows 61 to 63).
    private const int PlayerColumn = 20;
    private const int GroundRow = TestWorlds.GroundRow;

    private static GameSimulation WithTrunkAt(int x, int y)
    {
        return TestWorlds.StandingAt(PlayerColumn, grid => grid.SetTile(x, y, TileId.Trunk));
    }

    private static void Hold(GameSimulation sim, int ticks, int x, int y)
    {
        for (var i = 0; i < ticks; i++)
        {
            sim.Step([PlayerCommand.Mine(x, y)]);
        }
    }

    [Fact]
    public void TheAxeBreaksATrunkAfterItsMiningTicksAndTheWoodGoesToTheInventory()
    {
        var sim = WithTrunkAt(23, GroundRow - 2);
        var ticks = TileDefinition.Get(TileId.Trunk).MiningTicks;
        Hold(sim, ticks - 1, 23, GroundRow - 2);
        Assert.Equal(TileId.Trunk, sim.World.GetTile(23, GroundRow - 2));
        Assert.Empty(sim.Events);

        sim.Step([PlayerCommand.Mine(23, GroundRow - 2)]);
        Assert.Equal(TileId.Air, sim.World.GetTile(23, GroundRow - 2));
        Assert.Equal(1, sim.Inventory.Count(ItemId.Wood));
        Assert.Contains(sim.Events, e => e.Kind == SimulationEventKind.TileMined && e.Tick == sim.Tick);
        Assert.Null(sim.MiningTarget);
    }

    [Fact]
    public void TheHandAndThePickaxeCannotCutATrunkAndTheAxeCannotMineStone()
    {
        var sim = WithTrunkAt(23, GroundRow - 2);
        sim.Step([PlayerCommand.SelectSlot(1)]);
        Hold(sim, 200, 23, GroundRow - 2);
        Assert.Equal(TileId.Trunk, sim.World.GetTile(23, GroundRow - 2));
        Assert.Equal(0, sim.Inventory.Count(ItemId.Wood));

        var empty = TestWorlds.StandingAt(PlayerColumn, grid => grid.SetTile(23, GroundRow - 2, TileId.Trunk));
        empty.Step([PlayerCommand.SelectSlot(5)]);
        Hold(empty, 200, 23, GroundRow - 2);
        Assert.Equal(TileId.Trunk, empty.World.GetTile(23, GroundRow - 2));

        var stone = TestWorlds.StandingAt(PlayerColumn);
        Hold(stone, 200, 22, GroundRow);
        Assert.Equal(TileId.Stone, stone.World.GetTile(22, GroundRow));
        Assert.Equal(0, stone.Inventory.Count(ItemId.Stone));
    }

    [Fact]
    public void ThePickaxeMinesStoneAndTheHandPicksFlowersAndMushrooms()
    {
        var sim = TestWorlds.StandingAt(PlayerColumn, grid =>
        {
            grid.SetTile(23, GroundRow - 1, TileId.Flower);
            grid.SetTile(24, GroundRow - 1, TileId.Mushroom);
        });
        sim.Step([PlayerCommand.SelectSlot(1)]);
        Hold(sim, TileDefinition.Get(TileId.Stone).MiningTicks, 22, GroundRow);
        Assert.Equal(TileId.Air, sim.World.GetTile(22, GroundRow));
        Assert.Equal(1, sim.Inventory.Count(ItemId.Stone));

        sim.Step([PlayerCommand.SelectSlot(3)]);
        Hold(sim, TileDefinition.Get(TileId.Flower).MiningTicks, 23, GroundRow - 1);
        Assert.Equal(TileId.Air, sim.World.GetTile(23, GroundRow - 1));
        Hold(sim, TileDefinition.Get(TileId.Mushroom).MiningTicks, 24, GroundRow - 1);
        Assert.Equal(TileId.Air, sim.World.GetTile(24, GroundRow - 1));
        Assert.Equal(1, sim.Inventory.Count(ItemId.Flower));
        Assert.Equal(1, sim.Inventory.Count(ItemId.Mushroom));
    }

    [Fact]
    public void LeavesAreMinedByHandAndGiveNothing()
    {
        var sim = TestWorlds.StandingAt(PlayerColumn, grid => grid.SetTile(23, GroundRow - 2, TileId.Leaves));
        sim.Step([PlayerCommand.SelectSlot(4)]);
        Hold(sim, TileDefinition.Get(TileId.Leaves).MiningTicks, 23, GroundRow - 2);
        Assert.Equal(TileId.Air, sim.World.GetTile(23, GroundRow - 2));
        Assert.All(sim.Inventory.Slots.Skip(2), slot => Assert.True(slot.IsEmpty));
    }

    [Fact]
    public void ReleasingTheButtonResetsTheProgressAndChangingTheTargetDoesToo()
    {
        var sim = WithTrunkAt(23, GroundRow - 2);
        var ticks = TileDefinition.Get(TileId.Trunk).MiningTicks;
        Hold(sim, ticks - 10, 23, GroundRow - 2);
        sim.Step();
        Assert.Null(sim.MiningTarget);
        Assert.Equal(0, sim.MiningProgress);
        Hold(sim, ticks - 10, 23, GroundRow - 2);
        Assert.Equal(TileId.Trunk, sim.World.GetTile(23, GroundRow - 2));

        Hold(sim, 5, 23, GroundRow - 2);
        sim.Step([PlayerCommand.SelectSlot(1)]);
        Hold(sim, 1, 22, GroundRow);
        Assert.Equal(1, sim.MiningProgress);
        Assert.Equal(new TilePoint(22, GroundRow), sim.MiningTarget);
    }

    [Fact]
    public void ATileOutOfReachIsNotMined()
    {
        var sim = WithTrunkAt(PlayerColumn + 6, GroundRow - 2);
        Hold(sim, TileDefinition.Get(TileId.Trunk).MiningTicks + 10, PlayerColumn + 6, GroundRow - 2);
        Assert.Equal(TileId.Trunk, sim.World.GetTile(PlayerColumn + 6, GroundRow - 2));
        Assert.Equal(0, sim.Inventory.Count(ItemId.Wood));
    }

    [Fact]
    public void AFullInventoryHoldsTheMiningBackAndKeepsTheTile()
    {
        var sim = TestWorlds.StandingAt(PlayerColumn);
        for (var slot = 2; slot < Inventory.SlotCount; slot++)
        {
            sim.Inventory.SetSlot(slot, new ItemStack(ItemId.Sand, ItemDefinition.DefaultStackLimit));
        }

        sim.Step([PlayerCommand.SelectSlot(1)]);
        Hold(sim, TileDefinition.Get(TileId.Stone).MiningTicks + 5, 22, GroundRow);
        Assert.Equal(TileId.Stone, sim.World.GetTile(22, GroundRow));
        Assert.Null(sim.MiningTarget);
    }

    [Fact]
    public void MiningMakesTheChunkDirtyAndTheDigestChanges()
    {
        var sim = WithTrunkAt(23, GroundRow - 2);
        var before = sim.ComputeStateDigest();
        var chunk = sim.World.Tiles.GetChunk(ChunkCoord.FromTile(23, GroundRow - 2));
        Hold(sim, TileDefinition.Get(TileId.Trunk).MiningTicks, 23, GroundRow - 2);
        Assert.True(chunk.Dirty);
        Assert.NotEqual(before, sim.ComputeStateDigest());
    }

    [Fact]
    public void PlacingUsesTheSelectedSlotAndTakesOneBlockFromIt()
    {
        var sim = TestWorlds.StandingAt(PlayerColumn);
        sim.GrantItems(ItemId.Wood, 3);
        Assert.Equal(new ItemStack(ItemId.Wood, 3), sim.Inventory.Slots[2]);
        sim.Step([PlayerCommand.SelectSlot(2), PlayerCommand.Place(18, GroundRow - 1)]);
        Assert.Equal(TileId.Planks, sim.World.GetTile(18, GroundRow - 1));
        Assert.Equal(new ItemStack(ItemId.Wood, 2), sim.Inventory.Slots[2]);
        Assert.Contains(sim.Events, e => e.Kind == SimulationEventKind.TilePlaced);
    }

    [Fact]
    public void PlacingIsRefusedOutOfReachOnAFullTileAndInsideTheBody()
    {
        var sim = TestWorlds.StandingAt(PlayerColumn);
        sim.GrantItems(ItemId.Wood, 5);
        sim.Step([PlayerCommand.SelectSlot(2)]);

        sim.Step([PlayerCommand.Place(PlayerColumn + 6, GroundRow - 1)]);
        Assert.Equal(TileId.Air, sim.World.GetTile(PlayerColumn + 6, GroundRow - 1));

        sim.Step([PlayerCommand.Place(PlayerColumn + 4, GroundRow - 1)]);
        Assert.Equal(TileId.Planks, sim.World.GetTile(PlayerColumn + 4, GroundRow - 1));

        sim.Step([PlayerCommand.Place(PlayerColumn + 4, GroundRow - 1)]);
        Assert.Equal(4, sim.Inventory.Count(ItemId.Wood));

        sim.Step([PlayerCommand.Place(PlayerColumn, GroundRow - 2)]);
        Assert.Equal(TileId.Air, sim.World.GetTile(PlayerColumn, GroundRow - 2));
        Assert.Equal(4, sim.Inventory.Count(ItemId.Wood));
    }

    [Fact]
    public void ASolidBlockMayNotOverlapTheBodyButAPlantMay()
    {
        var sim = TestWorlds.StandingAt(PlayerColumn);
        sim.GrantItems(ItemId.Stone, 1);
        sim.GrantItems(ItemId.Flower, 1);
        sim.Step([PlayerCommand.SelectSlot(2), PlayerCommand.Place(PlayerColumn, GroundRow - 2)]);
        Assert.Equal(TileId.Air, sim.World.GetTile(PlayerColumn, GroundRow - 2));

        sim.Step([PlayerCommand.SelectSlot(3), PlayerCommand.Place(PlayerColumn, GroundRow - 2)]);
        Assert.Equal(TileId.Flower, sim.World.GetTile(PlayerColumn, GroundRow - 2));
    }

    [Fact]
    public void ToolsAndEmptySlotsPlaceNothing()
    {
        var sim = TestWorlds.StandingAt(PlayerColumn);
        sim.Step([PlayerCommand.SelectSlot(0), PlayerCommand.Place(18, GroundRow - 1)]);
        Assert.Equal(TileId.Air, sim.World.GetTile(18, GroundRow - 1));
        sim.Step([PlayerCommand.SelectSlot(7), PlayerCommand.Place(18, GroundRow - 1)]);
        Assert.Equal(TileId.Air, sim.World.GetTile(18, GroundRow - 1));
        Assert.True(sim.SelectedStack.IsEmpty);
    }

    [Fact]
    public void APlacedPlankBlockIsSolidAndMiningItReturnsTheWood()
    {
        var sim = TestWorlds.StandingAt(PlayerColumn);
        sim.GrantItems(ItemId.Wood, 1);
        sim.Step([PlayerCommand.SelectSlot(2), PlayerCommand.Place(18, GroundRow - 1)]);
        Assert.True(sim.World.IsSolid(18, GroundRow - 1));
        sim.Step([PlayerCommand.SelectSlot(1)]);
        Hold(sim, TileDefinition.Get(TileId.Planks).MiningTicks, 18, GroundRow - 1);
        Assert.Equal(TileId.Air, sim.World.GetTile(18, GroundRow - 1));
        Assert.Equal(1, sim.Inventory.Count(ItemId.Wood));
    }

    [Fact]
    public void SelectingASlotOutsideTheHotbarIsIgnored()
    {
        var sim = TestWorlds.StandingAt(PlayerColumn);
        sim.Step([PlayerCommand.SelectSlot(4)]);
        Assert.Equal(4, sim.SelectedSlot);
        sim.Step([PlayerCommand.SelectSlot(10)]);
        Assert.Equal(4, sim.SelectedSlot);
        sim.Step([PlayerCommand.SelectSlot(-1)]);
        Assert.Equal(4, sim.SelectedSlot);
    }

    [Fact]
    public void TheSameCommandsGiveTheSameStateEveryTime()
    {
        string Run()
        {
            var sim = GameSimulation.Create(42);
            var spawnX = sim.World.Spawn.X;
            sim.GrantItems(ItemId.Wood, 2);
            for (var i = 0; i < 300; i++)
            {
                sim.Step(i % 40 == 0 ? new[] { PlayerCommand.Place(spawnX + 2, sim.World.Spawn.Y) } : new[] { PlayerCommand.MoveRight });
            }

            return sim.ComputeStateDigest();
        }

        Assert.Equal(Run(), Run());
    }
}
