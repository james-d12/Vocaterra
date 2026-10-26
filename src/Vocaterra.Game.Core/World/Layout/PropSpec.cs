using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.World.Layout;

/// <summary>
/// A prop placed on the terrain. <see cref="Type"/> is one of <c>workbench</c>, <c>fire_ring</c>, <c>stall</c>, <c>house</c>, <c>signpost</c>,
/// <c>bridge</c>, <c>bush</c>, <c>berry_bush</c>, <c>tree</c>, <c>paving</c> and <c>stamp</c>; the other members are the type's parameters, 0 or
/// <see langword="null"/> when unset. A layout gives the position as a site and an offset; the parser turns it into the column.
/// </summary>
/// <param name="Type">The prop type.</param>
/// <param name="X">The anchor column: the centre for a fire ring, a sign, a bush and a tree; the first block of a workbench; the north-west corner of a stall, a house, a paving and a stamp; the centre line's first column of a bridge.</param>
/// <param name="Z">The anchor row (see <paramref name="X"/>).</param>
/// <param name="Name">The named point the prop registers (at its reference cell), or <see langword="null"/>.</param>
/// <param name="Width">The size along X of a stall, a house or a paving; the number of lanes of a bridge (default 3).</param>
/// <param name="Depth">The size along Z of a stall, a house or a paving.</param>
/// <param name="Height">The wall height of a house (default 3); the trunk height of an explicit tree (default 4).</param>
/// <param name="Length">The number of blocks of a workbench (default 2) or the span of a bridge.</param>
/// <param name="Facing">The side a house's door is on, the side a stall's counter is on, the direction a bridge runs, or the way a stamp is turned (north: as drawn).</param>
/// <param name="AlongZ">For a workbench: whether it extends along Z instead of X.</param>
/// <param name="Stamp">For a stamp: the name of the stamp.</param>
/// <param name="Block">For a paving: the block laid.</param>
public sealed record PropSpec(
    string Type, int X, int Z, string? Name, int Width, int Depth, int Height, int Length, Direction? Facing, bool AlongZ, string? Stamp = null, BlockId Block = BlockId.Path);

