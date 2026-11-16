using Vocaterra.Game.Core.Navigation;

namespace Vocaterra.Game.Core.Scripted;

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

    /// <summary>Put a water source in a cell, which then flows (<c>--pour x,y,z</c>, a debug step for scripted runs and screenshots, water-plan H06).</summary>
    Pour,

    /// <summary>Hold the clock at an hour, 0 to 24, until <see cref="RunTime"/> (<c>--set-time &lt;hour&gt;</c>, E03).</summary>
    SetTime,

    /// <summary>Let a held clock run on from its held hour (<c>--set-time run</c>, E03).</summary>
    RunTime,

    /// <summary>Hold a weather kind by its id, until <see cref="RunWeather"/> (<c>--set-weather &lt;id&gt;</c>, E03).</summary>
    SetWeather,

    /// <summary>Let a held weather run on (<c>--set-weather run</c>, E03).</summary>
    RunWeather,

    /// <summary>Move items by hand over a container and slot (<c>--move left,bag,12</c>, <c>--move shift,bag,3</c>, <c>--move sort</c>, <c>--move return</c>, I02): one <see cref="Simulation.VoxelCommandKind.SlotMove"/> command, the same a click sends.</summary>
    SlotMove,
}
