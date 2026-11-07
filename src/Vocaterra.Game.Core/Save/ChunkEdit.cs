using System.Text.Json.Serialization;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Save;

/// <summary>
/// One chunk that differs from the freshly built scene: its index in <see cref="VoxelGrid.Chunks"/> and the chunk's full content as runs of
/// equal blocks (run-length encoded in storage order, so a chunk of air and stone is a few runs and a chunk with trees a few dozen), and the water that is
/// not source (a flood, water-plan H06).
/// </summary>
/// <param name="Chunk">The index of the chunk in <see cref="VoxelGrid.Chunks"/> (the grid's chunk order: x fastest, then z, then y).</param>
/// <param name="Runs">The chunk's blocks, in storage order; their lengths add up to <see cref="VoxelChunk.Volume"/>.</param>
/// <param name="Water">The water cells that hold a level other than source, as runs in storage order; <see langword="null"/>, and left out of the file, when there are none.</param>
public sealed record ChunkEdit(
    int Chunk,
    IReadOnlyList<BlockRun> Runs,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<WaterRun>? Water = null);
