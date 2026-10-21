namespace Vocaterra.Simulation.Voxel;

/// <summary>
/// A block position. X grows to the east, Y grows up and Z grows to the south (so row 0 of a top-down map is the north edge). Block
/// (x, y, z) fills the cube from (x, y, z) to (x + 1, y + 1, z + 1) in block units.
/// </summary>
/// <param name="X">The block column towards the east.</param>
/// <param name="Y">The block layer, up.</param>
/// <param name="Z">The block row towards the south.</param>
public readonly record struct BlockPos(int X, int Y, int Z)
{
    /// <summary>The position one block away along <paramref name="face"/>'s outward normal.</summary>
    public BlockPos Offset(BlockFace face)
    {
        var (dx, dy, dz) = face.Normal();
        return new BlockPos(X + dx, Y + dy, Z + dz);
    }
}

/// <summary>A chunk position in the 3D chunk grid (block position divided by 16, rounded down).</summary>
/// <param name="X">The chunk column towards the east.</param>
/// <param name="Y">The chunk layer, up.</param>
/// <param name="Z">The chunk row towards the south.</param>
public readonly record struct VoxelChunkCoord(int X, int Y, int Z)
{
    /// <summary>The chunk that holds the block at (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>).</summary>
    public static VoxelChunkCoord FromBlock(int x, int y, int z) => new(x >> VoxelChunk.Shift, y >> VoxelChunk.Shift, z >> VoxelChunk.Shift);

    /// <summary>The block at the minimum corner of this chunk.</summary>
    public BlockPos Origin => new(X << VoxelChunk.Shift, Y << VoxelChunk.Shift, Z << VoxelChunk.Shift);
}

/// <summary>The six faces of a block; the value names the direction of the face's outward normal.</summary>
public enum BlockFace
{
    /// <summary>The face towards +X (east).</summary>
    East = 0,

    /// <summary>The face towards -X (west).</summary>
    West = 1,

    /// <summary>The top face, towards +Y (up).</summary>
    Up = 2,

    /// <summary>The bottom face, towards -Y (down).</summary>
    Down = 3,

    /// <summary>The face towards +Z (south).</summary>
    South = 4,

    /// <summary>The face towards -Z (north).</summary>
    North = 5,
}

/// <summary>Helpers for <see cref="BlockFace"/>.</summary>
public static class BlockFaceExtensions
{
    /// <summary>The outward unit normal of the face as (dx, dy, dz).</summary>
    public static (int Dx, int Dy, int Dz) Normal(this BlockFace face) => face switch
    {
        BlockFace.East => (1, 0, 0),
        BlockFace.West => (-1, 0, 0),
        BlockFace.Up => (0, 1, 0),
        BlockFace.Down => (0, -1, 0),
        BlockFace.South => (0, 0, 1),
        _ => (0, 0, -1),
    };
}
