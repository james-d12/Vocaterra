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
}
