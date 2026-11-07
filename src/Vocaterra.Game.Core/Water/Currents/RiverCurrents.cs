using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Generation;
using Vocaterra.Game.Core.World.Layout;

namespace Vocaterra.Game.Core.Water.Currents;

/// <summary>
/// The current of the river (water-plan 4.6). Source water has no level gradient, so the river's still-looking water would not move; this gives each column
/// of the river the downstream direction of its stretch. Built once per scene from the layout's <see cref="RiverSpec"/> and the built grid, never saved (it is
/// rebuilt from them), so a river added to <c>scene.json</c> flows with no code change. Lakes and pools are not river columns and have no current.
/// <para>
/// A stretch runs between two neighbouring points of the river's smooth curve. It flows from its end with the higher water surface to its end with the lower;
/// when both ends are level, in path order (the layout lists the river from upstream to downstream, <c>content/README.md</c>).
/// </para>
/// </summary>
public sealed class RiverCurrents
{
    private readonly int _sizeX;
    private readonly sbyte[]? _x;
    private readonly sbyte[]? _z;

    private RiverCurrents(int sizeX, sbyte[]? x, sbyte[]? z)
    {
        _sizeX = sizeX;
        _x = x;
        _z = z;
    }

    /// <summary>No river, no current.</summary>
    public static RiverCurrents None { get; } = new(0, null, null);

    /// <summary>
    /// Works out the current of every column of the river: the columns within the river's half width of its curve. The surface heights are read from the
    /// built grid, at the curve's points.
    /// </summary>
    /// <param name="river">The river, or null for none.</param>
    /// <param name="grid">The built world.</param>
    public static RiverCurrents Build(RiverSpec? river, VoxelGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        if (river is null)
        {
            return None;
        }

        var line = Polyline.Curve(river.Path, 4);
        var size = grid.SizeX * grid.SizeZ;
        var dist = new int[size];
        Array.Fill(dist, int.MaxValue);
        var along = new int[size];
        var segment = new int[size];
        line.Rasterize(river.HalfWidth, grid.SizeX, grid.SizeZ, dist, along, segment);

        var heights = new int[line.Count];
        for (var k = 0; k < heights.Length; k++)
        {
            heights[k] = SurfaceY(grid, line.X16[k] >> 4, line.Z16[k] >> 4);
        }

        var dirX = new sbyte[line.Count - 1];
        var dirZ = new sbyte[line.Count - 1];
        for (var k = 0; k + 1 < line.Count; k++)
        {
            long dx = line.X16[k + 1] - line.X16[k];
            long dz = line.Z16[k + 1] - line.Z16[k];
            if (heights[k + 1] > heights[k])
            {
                dx = -dx; // the far end is the higher: the water runs towards the near end
                dz = -dz;
            }

            var length = IntMath.ISqrt((dx * dx) + (dz * dz));
            dirX[k] = length == 0 ? (sbyte)0 : (sbyte)Math.Clamp(dx * 127 / length, -127, 127);
            dirZ[k] = length == 0 ? (sbyte)0 : (sbyte)Math.Clamp(dz * 127 / length, -127, 127);
        }

        var cx = new sbyte[size];
        var cz = new sbyte[size];
        var limit = river.HalfWidth * 16;
        for (var i = 0; i < size; i++)
        {
            if (dist[i] <= limit)
            {
                cx[i] = dirX[segment[i]];
                cz[i] = dirZ[segment[i]];
            }
        }

        return new RiverCurrents(grid.SizeX, cx, cz);
    }

    /// <summary>The downstream direction of the river's water in the column (a horizontal unit vector, or zero away from the river).</summary>
    /// <param name="x">The column.</param>
    /// <param name="z">The row.</param>
    public FlowVector At(int x, int z)
    {
        if (_x is null || _z is null || x < 0 || z < 0 || x >= _sizeX)
        {
            return FlowVector.Zero;
        }

        var i = (z * _sizeX) + x;
        if (i >= _x.Length)
        {
            return FlowVector.Zero;
        }

        // Stored with 127 as one, so the vector's horizontal part is a unit in FlowVector's scale (rounding aside, within one part in 127).
        return new FlowVector(_x[i] * FlowVector.One / 127, 0, _z[i] * FlowVector.One / 127);
    }

    // The y of the highest water cell in the column (the layout's river surface), or -1 when there is none.
    private static int SurfaceY(VoxelGrid grid, int x, int z)
    {
        for (var y = grid.SizeY - 1; y >= 0; y--)
        {
            if (grid.GetBlock(x, y, z) == BlockId.Water)
            {
                return y;
            }
        }

        return -1;
    }
}
