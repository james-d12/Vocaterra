namespace Vocaterra.Game.Core.Voxel;

/// <summary>A set of block kinds as the bits of one 128-bit number (<see cref="UInt128"/>): bit <c>n</c> stands for the <see cref="BlockId"/> of value <c>n</c>.</summary>
public static class BlockKinds
{
    /// <summary>How many block ids a kind mask can hold (ids 0 to <c>Capacity - 1</c>).</summary>
    public const int Capacity = 128;

    /// <summary>The bit of a block kind.</summary>
    public static UInt128 Bit(BlockId id) => UInt128.One << (int)id;

    /// <summary>The bit of a block kind given as its number.</summary>
    public static UInt128 Bit(int kind) => UInt128.One << kind;
}
