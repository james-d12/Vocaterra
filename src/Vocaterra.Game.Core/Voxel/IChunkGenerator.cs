namespace Vocaterra.Game.Core.Voxel;

/// <summary>
/// The generate-on-read hook of a streamed <see cref="VoxelGrid"/>: asked for a chunk that is not resident when a read, a write or <see cref="VoxelGrid.GetChunk"/>
/// needs it. It lives here so that <c>Voxel/</c> never depends on <c>World/</c>: B06's chunk source implements it. An implementation is pure (the same chunk every
/// time, a function of the coordinate alone), reads nothing from the grid and may be asked about any chunk inside the limits.
/// </summary>
public interface IChunkGenerator
{
    /// <summary>The blocks of the chunk, or <see langword="null"/> for a chunk of air.</summary>
    ChunkContent? Generate(VoxelChunkCoord coord);

    /// <summary>
    /// The highest layer of a chunk column that can hold anything but air (B40), or <see langword="null"/> when the generator cannot say cheaply: <see cref="Generate"/> gives <see langword="null"/> for every chunk of the column above it, so a reader of the
    /// column's highest block need not ask for those layers (each ask costs a window of the world's noise).
    /// </summary>
    /// <param name="chunkX">The chunk column's x.</param>
    /// <param name="chunkZ">The chunk column's z.</param>
    int? HighestPossibleLayer(int chunkX, int chunkZ) => null;
}
