using System.Text.Json.Serialization;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Save;

/// <summary>
/// One chunk that differs from the world as generated (<see cref="VoxelChunk.IsChanged"/>): its chunk coordinates and the chunk's full content as runs of
/// equal blocks (run-length encoded in storage order, so a chunk of air and stone is a few runs and a chunk with trees a few dozen), and the water that is
/// not source (a flood, water-plan H06), and the state bytes other than 0 (K01).
/// </summary>
/// <param name="X">The chunk's column (block x divided by 16, rounded down).</param>
/// <param name="Y">The chunk's layer (block y divided by 16, rounded down); negative below y 0.</param>
/// <param name="Z">The chunk's row (block z divided by 16, rounded down).</param>
/// <param name="Runs">The chunk's blocks, in storage order; their lengths add up to <see cref="VoxelChunk.Volume"/>.</param>
/// <param name="Water">The water cells that hold a level other than source, as runs in storage order; <see langword="null"/>, and left out of the file, when there are none.</param>
/// <param name="States">The cells that hold a state byte other than 0, as runs in storage order; <see langword="null"/>, and left out of the file, when there are none.</param>
public sealed record ChunkEdit(
    int X,
    int Y,
    int Z,
    IReadOnlyList<BlockRun> Runs,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<WaterRun>? Water = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<StateRun>? States = null);
