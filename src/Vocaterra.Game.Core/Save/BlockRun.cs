using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Save;

/// <summary>A run of equal blocks inside a chunk: <see cref="Length"/> blocks of <see cref="Block"/>, in the chunk's storage order.</summary>
/// <param name="Block">The block kind of the run.</param>
/// <param name="Length">How many blocks the run holds (at least 1).</param>
public readonly record struct BlockRun(BlockId Block, int Length);

