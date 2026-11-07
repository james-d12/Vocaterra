using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.World.Generation;

/// <summary>
/// Builds the scene's falling water so that it is at rest under the flow rules (water-plan 4.5 and 4.7): waking every water cell changes nothing.
/// A source that rests on source water spreads sideways into any air beside it, so a ridge of water, or a river whose surface steps down, would spill
/// over what it borders. Falling water does not: a cell under water goes down, and its neighbours skip it. So every drop is built as falling water:
/// <list type="bullet">
/// <item>A <b>waterfall</b> is one source at the top over falling water down to the pool's surface layer (the sheet's bottom cell has pool sources beside it).</item>
/// <item>A <b>rapid</b> is where a river column meets a lower water column: the lower column's surface cell and the cells up to the higher surface turn
/// into falling water, topped by one cell of thin flowing water fed by the higher reach, so the last source of the upper reach no longer spills.</item>
/// </list>
/// Nothing here is special to a place: any river or waterfall of any layout comes out the same way.
/// </summary>
internal static class WaterAtRest
{
    private static readonly (int Dx, int Dz)[] Sides = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    /// <summary>Writes the waterfalls and the rapids of the terrain into the grid, whose water columns are already filled with sources.</summary>
    public static void Apply(VoxelGrid grid, Terrain terrain)
    {
        foreach (var fall in terrain.Falls)
        {
            for (var y = fall.Bottom; y < fall.Top; y++)
            {
                _ = grid.SetWater(fall.X, y, fall.Z, WaterLevel.Flowing(1, falling: true));
            }
        }

        var width = terrain.SizeX;
        var falling = new HashSet<int>(terrain.Falls.Select(f => (f.Z * width) + f.X));
        for (var z = 0; z < terrain.SizeZ; z++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = (z * width) + x;
                if (falling.Contains(i) || !terrain.IsWater(x, z))
                {
                    continue;
                }

                // The highest river surface beside this column, if it is above this column's own.
                var own = terrain.WaterTop[i];
                var higher = own;
                foreach (var (dx, dz) in Sides)
                {
                    var (nx, nz) = (x + dx, z + dz);
                    if (nx < 0 || nx >= width || nz < 0 || nz >= terrain.SizeZ)
                    {
                        continue;
                    }

                    var j = (nz * width) + nx;
                    if ((terrain.Flags[j] & Terrain.RiverWaterFlag) != 0 && terrain.IsWater(nx, nz) && !falling.Contains(j))
                    {
                        higher = Math.Max(higher, terrain.WaterTop[j]);
                    }
                }

                if (higher == own)
                {
                    continue;
                }

                // Falling water from the surface up to just under the higher reach's surface, and its first cell of flow on top.
                for (var y = own; y < higher; y++)
                {
                    _ = grid.SetBlock(x, y, z, BlockId.Water);
                    _ = grid.SetWater(x, y, z, WaterLevel.Flowing(1, falling: true));
                }

                _ = grid.SetBlock(x, higher, z, BlockId.Water);
                _ = grid.SetWater(x, higher, z, WaterLevel.Flowing(1));
            }
        }
    }
}
