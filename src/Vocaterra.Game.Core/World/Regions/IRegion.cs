using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Streaming;

namespace Vocaterra.Game.Core.World.Regions;

/// <summary>
/// A hand-made piece of the world (B06, biomes plan section 3.2): it supplies its own chunks and the height of its edge, and the procedural land round it is eased to that edge. Every
/// implementation is immutable and safe to call from any thread: a chunk it hands out is the same whenever it is asked for.
/// </summary>
public interface IRegion
{
    /// <summary>The region's name.</summary>
    string Id { get; }

    /// <summary>The chunks the region fills.</summary>
    RegionBounds Bounds { get; }

    /// <summary>Whether the chunk is one of the region's.</summary>
    /// <param name="chunk">The chunk.</param>
    bool Covers(VoxelChunkCoord chunk);

    /// <summary>The region's chunk (immutable; nothing is copied, nothing can change it).</summary>
    /// <param name="chunk">The chunk, which <see cref="Covers"/> says is the region's.</param>
    /// <exception cref="ArgumentOutOfRangeException">The chunk is not the region's.</exception>
    ChunkData Copy(VoxelChunkCoord chunk);

    /// <summary>
    /// The ground height of the region's footprint column nearest to the column (x, z): the height of the edge the land outside eases towards, and the column's own ground when it is inside.
    /// Trees are not part of it.
    /// </summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    int EdgeHeight(int x, int z);

    /// <summary>
    /// The edge's height blurred over the data's radius (<see cref="EdgeSmoothingSpec"/>): the ground of the footprint column nearest to the column, averaged with the columns round it. The land outside
    /// eases from <see cref="EdgeHeight"/> at the border to this a few blocks out, so the edge's roughness does not comb the land into ridges.
    /// </summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    int SmoothedEdgeHeight(int x, int z);
}
