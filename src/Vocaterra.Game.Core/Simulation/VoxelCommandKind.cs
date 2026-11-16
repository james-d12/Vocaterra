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

    /// <summary>
    /// Wear the item in bag slot <see cref="VoxelCommand.A"/> in the equipment slot of group <see cref="VoxelCommand.B"/>, slot <see cref="VoxelCommand.C"/> (indexes into <c>game/equipment.json</c>), swapping with what is worn there:
    /// the old item takes the bag slot the new one left (I03a). A press; a refusal changes nothing and raises <see cref="Event.SimulationEventKind.EquipRefused"/>.
    /// </summary>
    Equip = 8,

    /// <summary>
    /// Take off the item worn in group <see cref="VoxelCommand.A"/>, slot <see cref="VoxelCommand.B"/>, into bag slot <see cref="VoxelCommand.C"/>, or into the first free one when C is -1 (I03a). A press; a refusal (the bag is
    /// full, the slot asked for is taken) changes nothing and raises <see cref="Event.SimulationEventKind.EquipRefused"/>.
    /// </summary>
    Unequip = 9,

    /// <summary>
    /// Move items by hand (I02, <see cref="Items.Containers.InventoryOps"/>). <see cref="VoxelCommand.A"/> is the <see cref="Items.Containers.MoveAction"/> (0 left click, 1 right click, 2 shift-click, 3 sort, 4 return the held stack),
    /// <see cref="VoxelCommand.B"/> the container's id (<see cref="Items.Containers.ContainerIds"/>: 0 bag, 1 equipment, 2 trash) and <see cref="VoxelCommand.C"/> the slot in it (the equipment's is its flat index, group by group in
    /// <c>game/equipment.json</c> order). B and C are ignored by sort and return. A press; a refused move changes nothing and raises <see cref="Event.SimulationEventKind.MoveRefused"/>.
    /// </summary>
    SlotMove = 10,
}
