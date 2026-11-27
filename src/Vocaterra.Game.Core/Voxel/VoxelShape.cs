namespace Vocaterra.Game.Core.Voxel;

/// <summary>
/// The boxes of one block in one state: what is <see cref="Drawn"/> (and picked by the ray) and what a body <see cref="Collides"/> with, with the cover of each face
/// worked out from the drawn boxes. Instances are cached by <see cref="BlockShapes"/> and never change; reading one allocates nothing.
/// </summary>
public sealed class VoxelShape
{
    private readonly ShapeBox[] _drawn;
    private readonly ShapeBox[] _collides;
    private readonly FaceCover[] _covers;

    internal VoxelShape(ShapeBox[] drawn, ShapeBox[] collides)
    {
        _drawn = drawn;
        _collides = collides;
        _covers = new FaceCover[6];
        foreach (var face in Enum.GetValues<BlockFace>())
        {
            _covers[(int)face] = ComputeCover(drawn, face);
        }
    }

    /// <summary>The shape of a cell with nothing in it.</summary>
    public static VoxelShape Empty { get; } = new([], []);

    /// <summary>The full cube, drawn and collided with as one box.</summary>
    public static VoxelShape Cube { get; } = new([ShapeBox.Full], [ShapeBox.Full]);

    /// <summary>The boxes the mesher draws and the ray selects.</summary>
    public ReadOnlySpan<ShapeBox> Drawn => _drawn;

    /// <summary>The boxes a body bumps into (empty for an open gate and for a block nothing collides with).</summary>
    public ReadOnlySpan<ShapeBox> Collides => _collides;

    /// <summary>How much of the cell's face the drawn boxes cover.</summary>
    public FaceCover CoverOf(BlockFace face) => _covers[(int)face];

    private static FaceCover ComputeCover(ShapeBox[] boxes, BlockFace face)
    {
        // Mark the sixteenth-squares of the face's plane that a box touching the plane covers; the two axes of the plane are (a, b).
        Span<bool> covered = stackalloc bool[256];
        var touched = false;
        foreach (var box in boxes)
        {
            var (touches, a0, a1, b0, b1) = face switch
            {
                BlockFace.East => (box.MaxX == 16, box.MinY, box.MaxY, box.MinZ, box.MaxZ),
                BlockFace.West => (box.MinX == 0, box.MinY, box.MaxY, box.MinZ, box.MaxZ),
                BlockFace.Up => (box.MaxY == 16, box.MinX, box.MaxX, box.MinZ, box.MaxZ),
                BlockFace.Down => (box.MinY == 0, box.MinX, box.MaxX, box.MinZ, box.MaxZ),
                BlockFace.South => (box.MaxZ == 16, box.MinX, box.MaxX, box.MinY, box.MaxY),
                _ => (box.MinZ == 0, box.MinX, box.MaxX, box.MinY, box.MaxY),
            };
            if (!touches || a1 <= a0 || b1 <= b0)
            {
                continue;
            }

            touched = true;
            for (var a = Math.Max(a0, 0); a < Math.Min(a1, 16); a++)
            {
                for (var b = Math.Max(b0, 0); b < Math.Min(b1, 16); b++)
                {
                    covered[(a * 16) + b] = true;
                }
            }
        }

        if (!touched)
        {
            return FaceCover.None;
        }

        foreach (var square in covered)
        {
            if (!square)
            {
                return FaceCover.Partial;
            }
        }

        return FaceCover.Full;
    }
}
