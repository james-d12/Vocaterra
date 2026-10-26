namespace Vocaterra.Game.Core.Voxel;

/// <summary>What a block ray hit: the block, the face it entered through and the cell in front of that face (where a block would be placed).</summary>
/// <param name="Block">The block that was hit.</param>
/// <param name="Id">The kind of that block.</param>
/// <param name="Face">The face of the block the ray entered through (its outward normal points back towards the viewer).</param>
/// <param name="Adjacent">The cell on the viewer's side of <paramref name="Face"/>: <paramref name="Block"/> offset by the face's normal.</param>
/// <param name="DistanceUnits">The distance from the ray's origin to the face, in units of 1/4096 block (rounded down).</param>
public readonly record struct BlockRayHit(BlockPos Block, BlockId Id, BlockFace Face, BlockPos Adjacent, int DistanceUnits);

