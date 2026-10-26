namespace Vocaterra.Game.Core.Voxel;

/// <summary>A set of block kinds as the bits of one number: bit <c>n</c> stands for the <see cref="BlockId"/> of value <c>n</c>.</summary>
public static class BlockKinds
{
    /// <summary>The bit of a block kind.</summary>
    public static ulong Bit(BlockId id) => 1UL << (int)id;
}
