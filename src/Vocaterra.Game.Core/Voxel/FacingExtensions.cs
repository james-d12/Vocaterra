namespace Vocaterra.Game.Core.Voxel;

/// <summary>Helpers for <see cref="Facing"/>.</summary>
public static class FacingExtensions
{
    /// <summary>The facing a quarter turn to the right (clockwise seen from above) of <paramref name="facing"/>.</summary>
    public static Facing Right(this Facing facing) => (Facing)(((int)facing + 1) & 3);

    /// <summary>The facing a quarter turn to the left of <paramref name="facing"/>.</summary>
    public static Facing Left(this Facing facing) => (Facing)(((int)facing + 3) & 3);

    /// <summary>The facing turned <paramref name="quarterTurns"/> quarter turns to the right (any integer).</summary>
    public static Facing Turn(this Facing facing, int quarterTurns) => (Facing)(((int)facing + (quarterTurns & 3)) & 3);

    /// <summary>The opposite facing.</summary>
    public static Facing Opposite(this Facing facing) => (Facing)(((int)facing + 2) & 3);

    /// <summary>The block face that looks the same way.</summary>
    public static BlockFace ToFace(this Facing facing) => facing switch
    {
        Facing.North => BlockFace.North,
        Facing.East => BlockFace.East,
        Facing.South => BlockFace.South,
        _ => BlockFace.West,
    };

    /// <summary>The facing of a horizontal block face, or <see langword="null"/> for the top and the bottom.</summary>
    public static Facing? FromFace(BlockFace face) => face switch
    {
        BlockFace.North => Facing.North,
        BlockFace.East => Facing.East,
        BlockFace.South => Facing.South,
        BlockFace.West => Facing.West,
        _ => null,
    };

    /// <summary>The step of the facing on the ground plane as (dx, dz).</summary>
    public static (int Dx, int Dz) Step(this Facing facing) => facing switch
    {
        Facing.North => (0, -1),
        Facing.East => (1, 0),
        Facing.South => (0, 1),
        _ => (-1, 0),
    };
}
