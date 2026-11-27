namespace Vocaterra.Game.Core.Voxel;

/// <summary>
/// What a block ray hit: the block, the face it entered through, the cell next to that face (where a block would be placed) and the point of the hit inside the block's cell (K03).
/// </summary>
/// <param name="Block">The block that was hit.</param>
/// <param name="Id">The kind of that block.</param>
/// <param name="Face">The face the ray entered through (its outward normal points back towards the viewer): the cell's own face for a full cube, the face of the box it met for a shaped block (an open gate's bar, the top of a bottom slab).</param>
/// <param name="Adjacent">
/// The cell on the viewer's side of <paramref name="Face"/>: <paramref name="Block"/> offset by the face's normal, when the face lies on the cell's boundary (every face of a full cube, the side of a slab that fills the cell's side).
/// When the face lies inside the cell (the top of a bottom slab, the step of a stair, the side of a fence post) it is <paramref name="Block"/> itself: the click was on the cell's own open part, and placement decides (a slab merges into a double slab there).
/// </param>
/// <param name="DistanceUnits">The distance from the ray's origin to the face, in units of 1/4096 block (rounded down).</param>
/// <param name="HitX">The x of the hit point, in units of 1/4096 block from the cell's west edge (0 to 4096; a sixteenth is 256). The entry face's own axis is exactly on its plane.</param>
/// <param name="HitY">The y of the hit point, in units of 1/4096 block from the cell's floor (0 to 4096: 2048 and above is the upper half).</param>
/// <param name="HitZ">The z of the hit point, in units of 1/4096 block from the cell's north edge (0 to 4096).</param>
public readonly record struct BlockRayHit(BlockPos Block, BlockId Id, BlockFace Face, BlockPos Adjacent, int DistanceUnits, int HitX = 0, int HitY = 0, int HitZ = 0);
