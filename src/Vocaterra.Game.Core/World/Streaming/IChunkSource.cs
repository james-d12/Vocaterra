using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.World.Streaming;

/// <summary>
/// Where a chunk's blocks come from (B03, biomes plan section 3.1): a pure function of the seed, the world data and the chunk's coordinates, so a chunk is the same whenever and wherever it
/// is asked for, and a reader that finds a chunk missing can generate it on the spot. Every implementation is immutable and safe to call from any thread.
/// </summary>
public interface IChunkSource
{
    /// <summary>The chunk at a position in the chunk grid.</summary>
    /// <param name="coord">The chunk's position (block position divided by 16, rounded down).</param>
    ChunkData Generate(VoxelChunkCoord coord);
}
