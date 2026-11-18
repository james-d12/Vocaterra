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

    /// <summary>
    /// Collects kinds in a hot loop: two plain 64-bit halves, so adding a block costs one shift and one or, with no <see cref="UInt128"/> operator call
    /// (those are slow in a Debug build, and a chunk adds 4 096 of them).
    /// </summary>
    public struct Accumulator
    {
        private ulong _low;
        private ulong _high;

        /// <summary>Adds the kind of a block.</summary>
        public void Add(BlockId id)
        {
            var n = (int)id;
            if (n < 64)
            {
                _low |= 1UL << n;
            }
            else
            {
                _high |= 1UL << (n - 64);
            }
        }

        /// <summary>The kinds added so far.</summary>
        public readonly UInt128 Mask => new(_high, _low);
    }
}
