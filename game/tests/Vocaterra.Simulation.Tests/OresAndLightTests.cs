using Vocaterra.Simulation.Items;
using Vocaterra.Simulation.Lighting;
using Vocaterra.Simulation.Tiles;

namespace Vocaterra.Simulation.Tests;

/// <summary>Ore mining drops, the torch, and the light map (sky, torches, sealed caves, changes by mining), on hand-built worlds.</summary>
public sealed class OresAndLightTests
{
    private const int GroundRow = TestWorlds.GroundRow;
    private const int PickaxeSlot = 1;

    public static TheoryData<TileId, ItemId> Ores => new()
    {
        { TileId.CoalOre, ItemId.Coal },
        { TileId.CopperOre, ItemId.Copper },
        { TileId.IronOre, ItemId.Iron },
        { TileId.GoldOre, ItemId.Gold },
    };

    [Theory]
    [MemberData(nameof(Ores))]
    public void ThePickaxeMinesAnOreIntoItsItem(TileId ore, ItemId item)
    {
        var sim = TestWorlds.StandingAt(20, grid => grid.SetTile(22, GroundRow - 1, ore));
        sim.Step([PlayerCommand.SelectSlot(PickaxeSlot)]);
        var ticks = TileDefinition.Get(ore).MiningTicks;
        for (var i = 0; i < ticks - 1; i++)
        {
            sim.Step([PlayerCommand.Mine(22, GroundRow - 1)]);
        }

        Assert.Equal(ore, sim.World.GetTile(22, GroundRow - 1));
        sim.Step([PlayerCommand.Mine(22, GroundRow - 1)]);
        Assert.Equal(TileId.Air, sim.World.GetTile(22, GroundRow - 1));
        Assert.Equal(1, sim.Inventory.Count(item));
    }

    [Fact]
    public void TheAxeCannotMineOreAndTheHarderOresTakeLonger()
    {
        var sim = TestWorlds.StandingAt(20, grid => grid.SetTile(22, GroundRow - 1, TileId.IronOre));
        for (var i = 0; i < 200; i++)
        {
            sim.Step([PlayerCommand.Mine(22, GroundRow - 1)]);
        }

        Assert.Equal(TileId.IronOre, sim.World.GetTile(22, GroundRow - 1));
        Assert.Equal(0, sim.Inventory.Count(ItemId.Iron));

        var ticks = new[] { TileDefinition.Get(TileId.CoalOre).MiningTicks, TileDefinition.Get(TileId.CopperOre).MiningTicks, TileDefinition.Get(TileId.IronOre).MiningTicks, TileDefinition.Get(TileId.GoldOre).MiningTicks };
        Assert.Equal(ticks.OrderBy(t => t), ticks);
    }

    [Fact]
    public void ATorchIsPlacedFromTheInventoryAndMinedBackIntoOne()
    {
        var sim = TestWorlds.StandingAt(20);
        sim.GrantItems(ItemId.Torch, 2);
        sim.Step([PlayerCommand.SelectSlot(2)]);
        Assert.Equal(ItemId.Torch, sim.SelectedStack.Item);
        sim.Step([PlayerCommand.Place(22, GroundRow - 1)]);
        Assert.Equal(TileId.Torch, sim.World.GetTile(22, GroundRow - 1));
        Assert.Equal(1, sim.Inventory.Count(ItemId.Torch));
        Assert.False(TileDefinition.IsSolid(TileId.Torch));

        for (var i = 0; i < TileDefinition.Get(TileId.Torch).MiningTicks; i++)
        {
            sim.Step([PlayerCommand.Mine(22, GroundRow - 1)]);
        }

        Assert.Equal(TileId.Air, sim.World.GetTile(22, GroundRow - 1));
        Assert.Equal(2, sim.Inventory.Count(ItemId.Torch));
    }

    [Fact]
    public void TheSkyLightsTheOpenAirAndTheSurfaceRockFaceButNotTheRockBelow()
    {
        var sim = TestWorlds.StandingAt(20);
        Assert.Equal(LightMap.MaxLevel, sim.Light.Level(50, 10));
        Assert.Equal(LightMap.MaxLevel, sim.Light.Level(50, GroundRow));
        Assert.Equal(0, sim.Light.Level(50, GroundRow + 5));
        Assert.Equal(LightMap.RockFloor, sim.Light.Brightness(50, GroundRow + 5));
        Assert.Equal(LightMap.MaxLevel, sim.Light.Brightness(50, 10));
    }

    [Fact]
    public void AClosedCaveIsDarkUntilLight()
    {
        var sim = TestWorlds.StandingAt(108, grid => CarveSealedCave(grid));
        for (var x = 110; x <= 116; x++)
        {
            for (var y = 66; y <= 68; y++)
            {
                Assert.Equal(0, sim.Light.Level(x, y));
                Assert.Equal(TileId.Air, sim.World.GetTile(x, y));
            }
        }
    }

    [Fact]
    public void ATorchLightsItsCaveWithAFalloffOfTwoPerTileAndReachesEightTiles()
    {
        var sim = TestWorlds.StandingAt(108, grid =>
        {
            CarveSealedCave(grid);
            grid.SetTile(113, 67, TileId.Torch);
        });
        Assert.Equal(LightMap.MaxLevel, sim.Light.Level(113, 67));
        for (var distance = 1; distance <= 3; distance++)
        {
            Assert.Equal(LightMap.MaxLevel - (LightMap.Falloff * distance), sim.Light.Level(113 + distance, 67));
            Assert.Equal(LightMap.MaxLevel - (LightMap.Falloff * distance), sim.Light.Level(113 - distance, 67));
        }

        Assert.Equal(2, sim.Light.Level(113 + LightMap.TorchRadius - 1, 67));
        Assert.Equal(0, sim.Light.Level(113 + LightMap.TorchRadius, 67));
        Assert.Equal(LightMap.TorchRadius, 8);
    }

    [Fact]
    public void MiningAHoleToTheSkyOpensTheCaveAndTheLightFallsOffSideways()
    {
        var sim = TestWorlds.StandingAt(108, grid => CarveSealedCave(grid));
        Assert.Equal(0, sim.Light.Level(110, 67));

        // Mine the roof: the stone at (110, 65) and (110, 64). The player stands at column 108, within reach of both.
        sim.Step([PlayerCommand.SelectSlot(PickaxeSlot)]);
        Hold(sim, TileDefinition.Get(TileId.Stone).MiningTicks, 110, 65);
        Hold(sim, TileDefinition.Get(TileId.Stone).MiningTicks, 110, 64);
        Assert.Equal(TileId.Air, sim.World.GetTile(110, 64));
        Assert.Equal(TileId.Air, sim.World.GetTile(110, 65));

        Assert.Equal(LightMap.MaxLevel, sim.Light.Level(110, 67));
        Assert.Equal(LightMap.MaxLevel - (LightMap.Falloff * 4), sim.Light.Level(114, 67));
        Assert.Equal(LightMap.MaxLevel - (LightMap.Falloff * 6), sim.Light.Level(116, 67));
    }

    [Fact]
    public void AnIncrementalChangeGivesTheSameLightAsAFullRecompute()
    {
        var sim = TestWorlds.StandingAt(108, grid =>
        {
            CarveSealedCave(grid);
            grid.SetTile(113, 67, TileId.Torch);
        });
        var light = sim.Light;
        sim.Step([PlayerCommand.SelectSlot(PickaxeSlot)]);
        Hold(sim, TileDefinition.Get(TileId.Stone).MiningTicks, 110, 65);
        Hold(sim, TileDefinition.Get(TileId.Stone).MiningTicks, 110, 64);

        var full = new LightMap(sim.World.Tiles);
        for (var y = 0; y < sim.World.Height; y++)
        {
            for (var x = 0; x < sim.World.Width; x++)
            {
                Assert.Equal(full.Level(x, y), light.Level(x, y));
            }
        }
    }

    [Fact]
    public void TheLightMapIsAPureFunctionOfTheTiles()
    {
        var first = new LightMap(TestWorlds.Flat(grid => CarveSealedCave(grid)).Tiles);
        var second = new LightMap(TestWorlds.Flat(grid => CarveSealedCave(grid)).Tiles);
        for (var y = 0; y < TestWorlds.Height; y++)
        {
            for (var x = 0; x < TestWorlds.Width; x++)
            {
                Assert.Equal(first.Level(x, y), second.Level(x, y));
            }
        }
    }

    private static void Hold(GameSimulation sim, int ticks, int x, int y)
    {
        for (var i = 0; i < ticks; i++)
        {
            sim.Step([PlayerCommand.Mine(x, y)]);
        }
    }

    // A sealed cave of air at columns 110 to 125, rows 66 to 68, in stone (the roof is row 65, the sky-facing stone row 64).
    private static void CarveSealedCave(TileGrid grid)
    {
        for (var x = 110; x <= 125; x++)
        {
            for (var y = 66; y <= 68; y++)
            {
                grid.SetTile(x, y, TileId.Air);
            }
        }
    }
}
