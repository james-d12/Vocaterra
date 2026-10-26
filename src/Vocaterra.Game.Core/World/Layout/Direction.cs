namespace Vocaterra.Game.Core.World.Layout;

/// <summary>A compass direction on the ground plane: north is -Z, east is +X, south is +Z, west is -X.</summary>
public enum Direction
{
    /// <summary>Towards -Z (the top of a top-down map).</summary>
    North = 0,

    /// <summary>Towards +X.</summary>
    East = 1,

    /// <summary>Towards +Z.</summary>
    South = 2,

    /// <summary>Towards -X.</summary>
    West = 3,
}

