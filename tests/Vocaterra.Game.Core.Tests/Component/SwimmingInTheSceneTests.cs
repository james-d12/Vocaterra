using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.Water;

namespace Vocaterra.Game.Core.Tests.Component;

/// <summary>Swimming in the shipped scene: in a deep column of its water, held jump keeps the head at the surface and letting go sinks.</summary>
public sealed class SwimmingInTheSceneTests
{
    private const int U = VoxelPhysics.UnitsPerBlock;
    private const int MinDepth = 4;

    /// <summary>
    /// Finds a column of the built scene with at least <see cref="MinDepth"/> water cells on a solid floor, air above, and the same water
    /// in the eight columns around it (so the body touches no wall). Scans the grid: no coordinates are fixed.
    /// </summary>
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

                if (top - floor + 1 >= MinDepth && grid.IsSolid(x, floor - 1, z) && SurroundedByWater(grid, x, z, floor, top))
                {
                    return (x, z, floor, top);
                }
            }
        }

        return null;
    }

    private static bool SurroundedByWater(VoxelGrid grid, int x, int z, int floor, int top)
    {
        for (var dz = -1; dz <= 1; dz++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                for (var y = floor; y <= top; y++)
                {
                    if (grid.GetBlock(x + dx, y, z + dz) != BlockId.Water)
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    [Fact]
    public void Given_ADeepWaterColumnOfTheScene_When_JumpIsHeldAndThenReleased_Then_TheHeadStaysAtTheSurfaceAndThenSinks()
    {
        var scene = TestGame.Route();
        var column = FindDeepColumn(scene.Grid);
        Assert.NotNull(column);
        var (x, z, floor, top) = column.Value;
        var surface = (top * U) + WaterSurface.Units(scene.Grid.GetWater(x, top, z), false);
        var sim = VoxelSimulation.Create(scene);
        sim.TeleportTo(new BlockPos(x, floor + 1, z));
        var jump = new VoxelCommand(VoxelCommandKind.Jump);

        for (var i = 0; i < 200; i++)
        {
            sim.Step([jump]);
        }

        int min = int.MaxValue, max = int.MinValue;
        for (var i = 0; i < 200; i++)
        {
            sim.Step([jump]);
            min = Math.Min(min, sim.Player.EyeY);
            max = Math.Max(max, sim.Player.EyeY);
        }

        Assert.True(min > surface, $"the head stays out of the water (min {min}, surface {surface})");
        Assert.True(max < surface + U, $"the body does not leave the water (max {max}, surface {surface})");

        for (var i = 0; i < 90; i++)
        {
            sim.Step([]);
        }

        Assert.True(sim.Player.EyeY < surface, "letting go sinks the head under the surface");
        Assert.True(sim.Player.VelocityY < 0);
    }
}
