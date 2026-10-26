using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Mathematics;

namespace Vocaterra.Game.Core.Simulation;

/// <summary>The kinds of command a 3D front-end sends. New kinds are appended; an unknown kind is ignored by older builds.</summary>
public enum VoxelCommandKind
{
    /// <summary>Walk this tick along the world axes: <see cref="VoxelCommand.A"/> is dx (+1 east), <see cref="VoxelCommand.B"/> is dz (+1 south). A level: send it every tick it is held.</summary>
    Move = 0,

    /// <summary>Walk this tick relative to the facing: <see cref="VoxelCommand.A"/> strafes (+1 right), <see cref="VoxelCommand.B"/> goes forward (+1). A level.</summary>
    MoveRelative = 1,

    /// <summary>Face a direction: <see cref="VoxelCommand.A"/> is the yaw and <see cref="VoxelCommand.B"/> the pitch, in <see cref="Angles"/> steps. Absolute, applied before any other command of the tick uses the facing.</summary>
    Look = 2,

    /// <summary>The jump button is down this tick. A level: a body on the ground jumps while it is held.</summary>
    Jump = 3,

    /// <summary>
    /// Gather the block at (<see cref="VoxelCommand.A"/>, <see cref="VoxelCommand.B"/>, <see cref="VoxelCommand.C"/>) by hand. Held: send it every
    /// tick the button is down; progress builds while the target stays the same, and a tick without it resets the progress. On a water block with
    /// an empty bottle in the inventory it fills the bottle (S6, <see cref="VoxelSimulation.BottleFillTicks"/> ticks); without a bottle it does nothing.
    /// </summary>
    Interact = 4,

    /// <summary>
    /// Place one item (<see cref="VoxelCommand.A"/> is its <see cref="ItemId"/>) in the empty cell (<see cref="VoxelCommand.B"/>,
    /// <see cref="VoxelCommand.C"/>, <see cref="VoxelCommand.D"/>). A press: one block per command.
    /// </summary>
    Place = 5,

    /// <summary>Select the hotbar slot <see cref="VoxelCommand.A"/> (0 to 9).</summary>
    SelectSlot = 6,

    /// <summary>Craft the recipe at index <see cref="VoxelCommand.A"/> of <see cref="VoxelSimulation.Recipes"/>. A press.</summary>
    Craft = 7,
}

