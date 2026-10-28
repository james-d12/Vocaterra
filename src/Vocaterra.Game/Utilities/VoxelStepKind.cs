using Vocaterra.Game.Core.Navigation;

namespace Vocaterra.Game.Utilities;

/// <summary>The kinds of step the scripted 3D run takes.</summary>
public enum VoxelStepKind
{
    /// <summary>Face a direction (<c>--look yaw,pitch</c> in degrees).</summary>
    Look,

    /// <summary>Walk to a column (<c>--walk-to x,z</c>), by the cheapest way the world allows (<see cref="WalkPlanner"/>).</summary>
    WalkTo,

    /// <summary>Put the player at a column, standing on the ground (<c>--teleport x,z</c>, a debug step for screenshots, S13).</summary>
    Teleport,

    /// <summary>Hold the gather command on a block (<c>--interact x,y,z</c>).</summary>
    Interact,

    /// <summary>Place an item in a cell (<c>--place item@x,y,z</c>).</summary>
    Place,

    /// <summary>Select a hotbar slot (<c>--select 1..10</c>).</summary>
    Select,

    /// <summary>Craft the recipe that makes an item, with the station in reach (<c>--craft madera</c>, <c>--craft antorcha</c>).</summary>
    Craft,
}
