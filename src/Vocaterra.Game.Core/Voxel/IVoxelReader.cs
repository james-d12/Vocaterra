namespace Vocaterra.Game.Core.Voxel;

/// <summary>
/// The read side of a voxel world: what physics, rays, navigation, the guide's facts and the rain read. Reads inside <see cref="Limits"/> are exact (a streamed grid
/// generates what it does not hold yet); outside it every cell is solid for collision and air for lookups, as an edge wall. Writing stays inside the simulation
/// (<see cref="IVoxelWriter"/>).
/// </summary>
public interface IVoxelReader
{
    /// <summary>The box the world holds.</summary>
    WorldLimits Limits { get; }

    /// <summary>Whether the block position lies inside <see cref="Limits"/>.</summary>
    bool InBounds(int x, int y, int z);

    /// <summary>The block at the position; <see cref="BlockId.Air"/> outside the limits.</summary>
    BlockId GetBlock(int x, int y, int z);

    /// <summary>The water level at the position (<see cref="WaterLevel.Source"/> where there is none to read: check <see cref="GetBlock"/> first).</summary>
    WaterLevel GetWater(int x, int y, int z);

    /// <summary>Whether bodies collide at the position; everything outside the limits counts as solid.</summary>
    bool IsSolid(int x, int y, int z);

    /// <summary>Whether a body's box collides with the cell: a solid block, or the space just above a tall block.</summary>
    bool BlocksBody(int x, int y, int z);

    /// <summary>Whether the invisible wall for the body (<see cref="BodyBarrier"/>) stands in the column now: a cell there blocks a body whatever it holds, lava included. A reader with no wall answers no.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    bool IsWalled(int x, int z) => false;

    /// <summary>Whether a body can stand on the block at the position.</summary>
    bool CanStandOn(int x, int y, int z);

    /// <summary>The y of the highest block in the column that is not air, or <c>Limits.MinY - 1</c> when the column is empty or outside the limits (-1 on a bounded grid).</summary>
    int HighestBlock(int x, int z);
}
