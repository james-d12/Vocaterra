namespace Vocaterra.Game.Core.Voxel;

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

