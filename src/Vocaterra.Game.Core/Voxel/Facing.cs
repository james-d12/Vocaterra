namespace Vocaterra.Game.Core.Voxel;

/// <summary>
/// A horizontal direction stored in a block's state (K02): the two low bits. North is -Z, east is +X, south is +Z, west is -X, and the values run clockwise
/// seen from above (a quarter turn to the right adds one), so a rotation is an addition modulo four.
/// </summary>
public enum Facing
{
    /// <summary>Towards -Z.</summary>
    North = 0,

    /// <summary>Towards +X.</summary>
    East = 1,

    /// <summary>Towards +Z.</summary>
    South = 2,

    /// <summary>Towards -X.</summary>
    West = 3,
}
