using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Tests.Graphics.Weather;

/// <summary>The world the rain tests share: flat stone ground and, where a test asks, a roof over a part of it.</summary>
internal static class WeatherTestWorld
{
    /// <summary>The y of the highest ground block (its top face is at 64).</summary>
    public const int GroundBlock = 63;

    /// <summary>The y of the roof slab a test adds (its top face is at 71).</summary>
    public const int RoofBlock = 70;

    /// <summary>A grid of 128 by 128 blocks, 96 high, with ground over all of it.</summary>
    public static VoxelGrid Flat()
    {
        var grid = new VoxelGrid(128, 96, 128);
        for (var z = 0; z < 128; z++)
        {
            for (var x = 0; x < 128; x++)
            {
                grid.FillColumn(x, z, 0, GroundBlock, BlockId.Stone);
            }
        }

        return grid;
    }

    /// <summary>Lays a one-block roof of the square [x0, x1] by [z0, z1] at <see cref="RoofBlock"/>.</summary>
    public static void Roof(VoxelGrid grid, int x0, int z0, int x1, int z1)
    {
        for (var z = z0; z <= z1; z++)
        {
            for (var x = x0; x <= x1; x++)
            {
                grid.SetBlock(x, RoofBlock, z, BlockId.Stone);
            }
        }
    }
}
