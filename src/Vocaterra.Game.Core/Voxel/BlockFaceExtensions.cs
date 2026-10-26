namespace Vocaterra.Game.Core.Voxel;

/// <summary>Helpers for <see cref="BlockFace"/>.</summary>
public static class BlockFaceExtensions
{
    /// <summary>The outward unit normal of the face as (dx, dy, dz).</summary>
    public static (int Dx, int Dy, int Dz) Normal(this BlockFace face) => face switch
    {
        BlockFace.East => (1, 0, 0),
        BlockFace.West => (-1, 0, 0),
        BlockFace.Up => (0, 1, 0),
        BlockFace.Down => (0, -1, 0),
        BlockFace.South => (0, 0, 1),
        _ => (0, 0, -1),
    };
}

