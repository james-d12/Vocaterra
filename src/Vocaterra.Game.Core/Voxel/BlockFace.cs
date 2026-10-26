namespace Vocaterra.Game.Core.Voxel;

/// <summary>The six faces of a block; the value names the direction of the face's outward normal.</summary>
public enum BlockFace
{
    /// <summary>The face towards +X (east).</summary>
    East = 0,

    /// <summary>The face towards -X (west).</summary>
    West = 1,

    /// <summary>The top face, towards +Y (up).</summary>
    Up = 2,

    /// <summary>The bottom face, towards -Y (down).</summary>
    Down = 3,

    /// <summary>The face towards +Z (south).</summary>
    South = 4,

    /// <summary>The face towards -Z (north).</summary>
    North = 5,
}

