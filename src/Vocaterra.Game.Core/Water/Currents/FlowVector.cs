using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Water.Currents;

/// <summary>
/// Which way the water at a cell runs (water-plan 4.6): a direction in fixed point, <see cref="One"/> to the unit, east is +X, south is +Z, up is +Y. The
/// horizontal part is unit length (or zero) and falling water adds <c>-One</c> straight down. A still cell (a source, a puddle level with its neighbours) has the
/// zero vector. A value, so it compares and hashes by its three parts.
/// </summary>
/// <param name="X">East, in 1/<see cref="One"/>.</param>
/// <param name="Y">Up, in 1/<see cref="One"/>.</param>
/// <param name="Z">South, in 1/<see cref="One"/>.</param>
public readonly record struct FlowVector(int X, int Y, int Z)
{
    /// <summary>One, the length of a unit direction.</summary>
    public const int One = 256;

    private const int Full = 8;

    // The four horizontal directions in the same order as the flow rules: north (-z), east (+x), south (+z), west (-x).
    private static readonly int[] StepX = [0, 1, 0, -1];
    private static readonly int[] StepZ = [-1, 0, 1, 0];

    /// <summary>No current.</summary>
    public static FlowVector Zero => default;

    /// <summary>Whether this is the zero vector.</summary>
    public bool IsZero => X == 0 && Y == 0 && Z == 0;

    /// <summary>Whether the vector has a horizontal part.</summary>
    public bool IsHorizontal => X != 0 || Z != 0;

    /// <summary>
    /// The vector of the cell, as Minecraft works it out: for flowing water, the sum over the four sides of the direction times how much lower the water there is
    /// (a side of air with water beneath counts as a drop of the level difference plus a full block; a side of solid block counts for nothing), made a unit; falling
    /// water adds straight down. A source, and a cell that is not water, give <see cref="Zero"/>.
    /// </summary>
    /// <param name="grid">The world.</param>
    /// <param name="x">The cell's column.</param>
    /// <param name="y">The cell's layer.</param>
    /// <param name="z">The cell's row.</param>
    public static FlowVector At(IVoxelReader grid, int x, int y, int z)
    {
        ArgumentNullException.ThrowIfNull(grid);
        if (grid.GetBlock(x, y, z) != BlockId.Water)
        {
            return Zero;
        }

        var level = grid.GetWater(x, y, z);
        if (level.IsSource)
        {
            return Zero;
        }

        var own = HeightOf(level);
        long sumX = 0;
        long sumZ = 0;
        for (var i = 0; i < 4; i++)
        {
            var nx = x + StepX[i];
            var nz = z + StepZ[i];
            var neighbour = grid.GetBlock(nx, y, nz);
            int difference;
            if (neighbour == BlockId.Water)
            {
                difference = own - HeightOf(grid.GetWater(nx, y, nz));
            }
            else if (IsOpen(neighbour) && grid.GetBlock(nx, y - 1, nz) == BlockId.Water)
            {
                difference = own - (HeightOf(grid.GetWater(nx, y - 1, nz)) - Full);
            }
            else
            {
                continue;
            }

            sumX += (long)StepX[i] * difference;
            sumZ += (long)StepZ[i] * difference;
        }

        // The sums are scaled up before the integer square root, so a diagonal keeps its length within a part in a thousand.
        const int Scale = 1024;
        var length = IntMath.ISqrt((sumX * sumX * Scale * Scale) + (sumZ * sumZ * Scale * Scale));
        var vx = length == 0 ? 0 : (int)(sumX * Scale * One / length);
        var vz = length == 0 ? 0 : (int)(sumZ * Scale * One / length);
        return new FlowVector(vx, level.Falling ? -One : 0, vz);
    }

    /// <summary>The vector of the cell at a block position.</summary>
    public static FlowVector At(IVoxelReader grid, BlockPos cell) => At(grid, cell.X, cell.Y, cell.Z);

    // Eighths of a block: a source and falling water are full; flowing water thins by one eighth a cell (distance 1 is 7, 7 is 1).
    private static int HeightOf(WaterLevel level) => level.IsSource || level.Falling ? Full : Full - level.Distance;

    private static bool IsOpen(BlockId block) => block == BlockId.Air || BlockDefinition.IsDecor(block);
}
