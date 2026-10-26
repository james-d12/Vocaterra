namespace Vocaterra.Game.Core.Voxel;

/// <summary>
/// A block position. X grows to the east, Y grows up and Z grows to the south (so row 0 of a top-down map is the north edge). Block
/// (x, y, z) fills the cube from (x, y, z) to (x + 1, y + 1, z + 1) in block units.
/// </summary>
/// <param name="X">The block column towards the east.</param>
/// <param name="Y">The block layer, up.</param>
/// <param name="Z">The block row towards the south.</param>
public readonly record struct BlockPos(int X, int Y, int Z)
{
    /// <summary>The position one block away along <paramref name="face"/>'s outward normal.</summary>
    public BlockPos Offset(BlockFace face)
    {
        var (dx, dy, dz) = face.Normal();
        return new BlockPos(X + dx, Y + dy, Z + dz);
    }
}

