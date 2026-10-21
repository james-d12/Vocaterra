using Vocaterra.Simulation.Generation;
using Vocaterra.Simulation.Physics;
using Vocaterra.Simulation.Tiles;

namespace Vocaterra.Simulation.Tests;

/// <summary>Hand-built worlds for physics tests.</summary>
internal static class TestWorlds
{
    public const int Width = 256;
    public const int Height = 128;
    public const int GroundRow = 64;
    public const int Units = PhysicsConstants.UnitsPerTile;

    /// <summary>A flat world: air above row 64, stone from row 64 down; the spawn is at x = 20.</summary>
    public static World Flat(Action<TileGrid>? customise = null)
    {
        var grid = new TileGrid(Width, Height);
        for (var y = GroundRow; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                grid.SetTile(x, y, TileId.Stone);
            }
        }

        customise?.Invoke(grid);
        return new World(
            new WorldInfo(0, new WorldSize(Width, Height), 0),
            grid,
            new TilePoint(20, GroundRow - 1),
            new GenerationRecord(Width));
    }

    /// <summary>A simulation on a flat world with the player at the given tile column, standing on the ground.</summary>
    public static GameSimulation StandingAt(int tileX, Action<TileGrid>? customise = null)
    {
        var sim = GameSimulation.Create(Flat(customise));
        sim.PlacePlayer((tileX * Units) + (Units / 2) - (PhysicsConstants.PlayerWidth / 2), (GroundRow * Units) - PhysicsConstants.PlayerHeight);
        return sim;
    }

    /// <summary>Steps <paramref name="ticks"/> times with the same commands.</summary>
    public static void Run(GameSimulation sim, int ticks, params PlayerCommand[] commands)
    {
        for (var i = 0; i < ticks; i++)
        {
            sim.Step(commands);
        }
    }
}
