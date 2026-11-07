using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Water;

/// <summary>
/// How deep in water a body is, read three ways. <paramref name="Feet"/>: the water at the feet reaches above the soles. <paramref name="Body"/>:
/// the water in the body's centre column reaches at least half the body's height above its feet (the swim threshold). <paramref name="Eye"/>:
/// the eye point is inside water, below that cell's surface.
/// </summary>
/// <param name="Feet">Water covers the feet.</param>
/// <param name="Body">Water reaches the waist or higher.</param>
/// <param name="Eye">The eye is under the surface.</param>
public readonly record struct Immersion(bool Feet, bool Body, bool Eye)
{
    private const int U = VoxelPhysics.UnitsPerBlock;

    /// <summary>Nothing wet.</summary>
    public static Immersion Dry => default;

    /// <summary>Reads the immersion of a body in a grid. Pure: the same grid and body always give the same answer.</summary>
    public static Immersion Of(VoxelGrid grid, PlayerBody body)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(body);

        var cx = VoxelCollider.FloorDiv(body.CenterX, U);
        var cz = VoxelCollider.FloorDiv(body.CenterZ, U);

        var feetTop = SurfaceTop(grid, cx, VoxelCollider.FloorDiv(body.MinY, U), cz);
        var feet = feetTop is { } top && top > body.MinY;

        // The highest water surface reached by contiguous water from the feet cell upwards, in the centre column.
        var bodyWet = false;
        var waistY = body.MinY + (VoxelPhysics.PlayerHeight / 2);
        var cy = VoxelCollider.FloorDiv(body.MinY, U);
        var reach = int.MinValue;
        while (SurfaceTop(grid, cx, cy, cz) is { } surface)
        {
            reach = surface;
            if (surface < (cy + 1) * U)
            {
                break;
            }

            cy++;
        }

        if (reach != int.MinValue && reach >= waistY)
        {
            bodyWet = true;
        }

        var eyeCy = VoxelCollider.FloorDiv(body.EyeY, U);
        var eyeTop = SurfaceTop(grid, VoxelCollider.FloorDiv(body.EyeX, U), eyeCy, VoxelCollider.FloorDiv(body.EyeZ, U));
        var eye = eyeTop is { } eyeSurface && body.EyeY < eyeSurface;

        return new Immersion(feet, bodyWet, eye);
    }

    /// <summary>The world height (in units) of the water's top in the cell, or null when the cell is not water (or lies outside the grid).</summary>
    private static int? SurfaceTop(VoxelGrid grid, int x, int y, int z)
    {
        if (grid.GetBlock(x, y, z) != BlockId.Water)
        {
            return null;
        }

        var aboveIsWater = grid.GetBlock(x, y + 1, z) == BlockId.Water;
        return (y * U) + WaterSurface.Units(grid.GetWater(x, y, z), aboveIsWater);
    }
}
