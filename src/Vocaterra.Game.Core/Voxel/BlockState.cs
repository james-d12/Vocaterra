namespace Vocaterra.Game.Core.Voxel;

/// <summary>The axis an oak log lies along.</summary>
public enum LogAxis
{
    /// <summary>Along X (east to west). State 0, so a log written without a state lies this way.</summary>
    X = 0,

    /// <summary>Upright.</summary>
    Y = 1,

    /// <summary>Along Z (north to south).</summary>
    Z = 2,
}

/// <summary>Which part of its cell an oak slab fills.</summary>
public enum SlabKind
{
    /// <summary>The lower half (y 0 to 8 sixteenths). State 0.</summary>
    Bottom = 0,

    /// <summary>The upper half (y 8 to 16).</summary>
    Top = 1,

    /// <summary>Both halves: a full cube.</summary>
    Double = 2,
}

/// <summary>
/// How a block's state byte (K01) is read, one static helper per layout of the building plan (3.1). The state means nothing without the block's id: the helpers
/// pack and unpack the bits, and <see cref="IsValid"/> says which states a block may hold (every unused bit is 0). The fence keeps no state.
/// </summary>
public static class BlockState
{
    private const int FacingMask = 3;

    /// <summary>The state of an oak log: bits 0 and 1 hold the <see cref="LogAxis"/> (3 is invalid).</summary>
    public static class Log
    {
        /// <summary>Packs the axis.</summary>
        public static byte Pack(LogAxis axis) => (byte)axis;

        /// <summary>The axis of a state.</summary>
        public static LogAxis Axis(byte state) => (LogAxis)(state & 3);
    }

    /// <summary>The state of oak stairs: bits 0 and 1 the facing (the side the high step lies towards), bit 2 upside down.</summary>
    public static class Stairs
    {
        /// <summary>Packs the facing and whether the stairs are upside down.</summary>
        public static byte Pack(Facing facing, bool upsideDown) => (byte)((int)facing | (upsideDown ? 4 : 0));

        /// <summary>The facing of a state.</summary>
        public static Facing FacingOf(byte state) => (Facing)(state & FacingMask);

        /// <summary>Whether a state is upside down.</summary>
        public static bool UpsideDown(byte state) => (state & 4) != 0;
    }

    /// <summary>The state of an oak slab: bits 0 and 1 hold the <see cref="SlabKind"/> (3 is invalid).</summary>
    public static class Slab
    {
        /// <summary>Packs the kind.</summary>
        public static byte Pack(SlabKind kind) => (byte)kind;

        /// <summary>The kind of a state.</summary>
        public static SlabKind Kind(byte state) => (SlabKind)(state & 3);
    }

    /// <summary>The state of an oak fence gate: bits 0 and 1 the facing (the side it opens to), bit 2 open.</summary>
    public static class Gate
    {
        /// <summary>Packs the facing and whether the gate is open.</summary>
        public static byte Pack(Facing facing, bool open) => (byte)((int)facing | (open ? 4 : 0));

        /// <summary>The facing of a state.</summary>
        public static Facing FacingOf(byte state) => (Facing)(state & FacingMask);

        /// <summary>Whether a state is open.</summary>
        public static bool Open(byte state) => (state & 4) != 0;
    }

    /// <summary>The state of an oak door (one id for both cells): bits 0 and 1 the facing, bit 2 open, bit 3 hinge on the right, bit 4 the upper cell.</summary>
    public static class Door
    {
        /// <summary>Packs the facing, open, hinge and which cell of the door it is.</summary>
        public static byte Pack(Facing facing, bool open, bool hingeRight, bool upper) =>
            (byte)((int)facing | (open ? 4 : 0) | (hingeRight ? 8 : 0) | (upper ? 16 : 0));

        /// <summary>The facing of a state.</summary>
        public static Facing FacingOf(byte state) => (Facing)(state & FacingMask);

        /// <summary>Whether a state is open.</summary>
        public static bool Open(byte state) => (state & 4) != 0;

        /// <summary>Whether the hinge is on the right (seen from the side the closed panel is on, looking along the facing).</summary>
        public static bool HingeRight(byte state) => (state & 8) != 0;

        /// <summary>Whether a state is the door's upper cell.</summary>
        public static bool Upper(byte state) => (state & 16) != 0;
    }

    /// <summary>The state of an oak trapdoor: bits 0 and 1 the facing, bit 2 top half, bit 3 open.</summary>
    public static class Trapdoor
    {
        /// <summary>Packs the facing, the half and whether it is open.</summary>
        public static byte Pack(Facing facing, bool top, bool open) => (byte)((int)facing | (top ? 4 : 0) | (open ? 8 : 0));

        /// <summary>The facing of a state.</summary>
        public static Facing FacingOf(byte state) => (Facing)(state & FacingMask);

        /// <summary>Whether a state is the top half.</summary>
        public static bool Top(byte state) => (state & 4) != 0;

        /// <summary>Whether a state is open.</summary>
        public static bool Open(byte state) => (state & 8) != 0;
    }

    /// <summary>How many low bits of the state a block reads (0 for a block with no state).</summary>
    public static int BitsOf(BlockId id) => id switch
    {
        BlockId.OakLog or BlockId.OakSlab => 2,
        BlockId.OakStairs or BlockId.OakFenceGate => 3,
        BlockId.OakTrapdoor => 4,
        BlockId.OakDoor => 5,
        _ => 0,
    };

    /// <summary>Whether <paramref name="state"/> is a state the block may hold: every unused bit is 0, a log's axis is 0 to 2 and a slab never reads 3. Every block with no state holds only 0.</summary>
    public static bool IsValid(BlockId id, byte state)
    {
        if (state >> BitsOf(id) != 0)
        {
            return false;
        }

        return id switch
        {
            BlockId.OakLog or BlockId.OakSlab => (state & 3) != 3,
            _ => true,
        };
    }

    /// <summary>Every valid state of the block, in ascending order (just 0 for a block with no state).</summary>
    public static IReadOnlyList<byte> ValidStates(BlockId id)
    {
        var list = new List<byte>();
        for (var state = 0; state < 1 << BitsOf(id); state++)
        {
            if (IsValid(id, (byte)state))
            {
                list.Add((byte)state);
            }
        }

        return list;
    }
}
