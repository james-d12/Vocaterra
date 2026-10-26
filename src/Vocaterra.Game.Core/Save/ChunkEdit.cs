using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Save;

/// <summary>
/// One chunk that differs from the freshly built scene: its index in <see cref="VoxelGrid.Chunks"/> and the chunk's full content as runs of
/// equal blocks (run-length encoded in storage order, so a chunk of air and stone is a few runs and a chunk with trees a few dozen).
/// </summary>
/// <param name="Chunk">The index of the chunk in <see cref="VoxelGrid.Chunks"/> (the grid's chunk order: x fastest, then z, then y).</param>
/// <param name="Runs">The chunk's blocks, in storage order; their lengths add up to <see cref="VoxelChunk.Volume"/>.</param>
public sealed record ChunkEdit(int Chunk, IReadOnlyList<BlockRun> Runs);

