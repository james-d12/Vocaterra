using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.Water;

namespace Vocaterra.Game.Core.Tests.Component;

/// <summary>Breath in the shipped scene: at the bottom of a deep column the air lasts 15 seconds, then runs out once a second; at the surface it refills.</summary>
public sealed class DrowningInTheSceneTests
{
    private const int U = VoxelPhysics.UnitsPerBlock;

    private static (int X, int Z, int Floor, int Top)? FindDeepColumn(VoxelGrid grid)
    {
        for (var z = 1; z < grid.SizeZ - 1; z++)
        {
            for (var x = 1; x < grid.SizeX - 1; x++)
            {
                var top = grid.HighestBlock(x, z);
                if (top < 0 || grid.GetBlock(x, top, z) != BlockId.Water)
                {
                    continue;
                }

                var floor = top;
                while (floor > 0 && grid.GetBlock(x, floor - 1, z) == BlockId.Water)
                {
                    floor--;
                }

                if (top - floor + 1 >= 4 && grid.IsSolid(x, floor - 1, z))
                {
                    return (x, z, floor, top);
                }
            }
        }

        return null;
    }

    [Fact]
    public void Given_TheBottomOfADeepColumn_When_TheBodyStaysDown_Then_TheAirRunsOutAt15SecondsAndOutOfAirComesOnceASecond()
    {
        var scene = TestGame.Route();
        var column = FindDeepColumn(scene.Grid);
        Assert.NotNull(column);
        var (x, z, floor, top) = column.Value;
        var sim = VoxelSimulation.Create(scene);
        sim.TeleportTo(new BlockPos(x, floor, z));
        var outOfAir = new List<long>();

        for (var i = 0; i < 900 + 180; i++)
        {
            sim.Step([]);
            if (sim.Events.Any(static e => e.Kind == SimulationEventKind.OutOfAir))
            {
                outOfAir.Add(sim.Tick);
            }

            if (i == 898)
            {
                Assert.Equal(1, sim.Breath.Air);
            }
        }

        Assert.Equal(0, sim.Breath.Air);
        Assert.Equal(3, outOfAir.Count);
        Assert.Equal(900, outOfAir[0]);
        Assert.Equal(60, outOfAir[1] - outOfAir[0]);
        Assert.Equal(60, outOfAir[2] - outOfAir[1]);

        // Surface: the body is placed at the water's top; the air refills within two seconds.
        var surface = (top * U) + WaterSurface.Units(scene.Grid.GetWater(x, top, z), false);
        sim.TeleportTo(new BlockPos(x, top + 1, z));
        var restored = false;
        for (var i = 0; i < 150 && !sim.Breath.IsFull; i++)
        {
            sim.Step([]);
            restored |= sim.Events.Any(static e => e.Kind == SimulationEventKind.BreathRestored);
        }

        Assert.True(sim.Player.EyeY > surface);
        Assert.True(sim.Breath.IsFull);
        Assert.True(restored);
    }
}
