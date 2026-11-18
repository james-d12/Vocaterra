namespace Vocaterra.Game.Core.Voxel;

/// <summary>
/// Told when a chunk of a streamed <see cref="VoxelGrid"/> becomes resident or leaves memory. Installing and evicting are not edits: they do not move
/// <see cref="VoxelGrid.EditCount"/> or raise <see cref="IBlockChangeListener"/>, so the flow system sleeps through them. The renderer, the rain cover and the light index
/// listen here; <see cref="VoxelGrid.ResidencyVersion"/> counts the same events for a reader that polls.
/// </summary>
public interface IChunkResidencyListener
{
    /// <summary>Called after the chunk was installed (generated on read, installed by the streamer, or made empty for a first write).</summary>
    void OnChunkInstalled(VoxelChunkCoord coord);

    /// <summary>Called after the chunk was evicted.</summary>
    void OnChunkEvicted(VoxelChunkCoord coord);
}
