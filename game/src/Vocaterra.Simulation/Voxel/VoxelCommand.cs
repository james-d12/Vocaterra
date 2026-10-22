using Vocaterra.Simulation.Items;

namespace Vocaterra.Simulation.Voxel;

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

/// <summary>
/// One input for one tick: a kind and up to four integer arguments (meaning per kind, see <see cref="VoxelCommandKind"/>). Use the factory
/// methods. A kind the simulation does not know yet is ignored, so commands added later never break an older build.
/// </summary>
/// <param name="Kind">What the command asks for.</param>
/// <param name="A">The first argument.</param>
/// <param name="B">The second argument.</param>
/// <param name="C">The third argument.</param>
/// <param name="D">The fourth argument.</param>
public readonly record struct VoxelCommand(VoxelCommandKind Kind, int A = 0, int B = 0, int C = 0, int D = 0)
{
    /// <summary>Walk along the world axes: <paramref name="dx"/> towards the east, <paramref name="dz"/> towards the south (each -1, 0 or 1).</summary>
    public static VoxelCommand Move(int dx, int dz) => new(VoxelCommandKind.Move, dx, dz);

    /// <summary>Walk relative to the facing: <paramref name="strafe"/> to the right and <paramref name="forward"/> ahead (each -1, 0 or 1).</summary>
    public static VoxelCommand MoveRelative(int strafe, int forward) => new(VoxelCommandKind.MoveRelative, strafe, forward);

    /// <summary>Face <paramref name="yaw"/> and <paramref name="pitch"/> (in <see cref="Angles"/> steps).</summary>
    public static VoxelCommand Look(int yaw, int pitch) => new(VoxelCommandKind.Look, yaw, pitch);

    /// <summary>Jump button down.</summary>
    public static VoxelCommand Jump { get; } = new(VoxelCommandKind.Jump);

    /// <summary>Gather the block at (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>) this tick.</summary>
    public static VoxelCommand Interact(int x, int y, int z) => new(VoxelCommandKind.Interact, x, y, z);

    /// <summary>Place <paramref name="item"/> at (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>).</summary>
    public static VoxelCommand Place(ItemId item, int x, int y, int z) => new(VoxelCommandKind.Place, (int)item, x, y, z);

    /// <summary>Select the hotbar slot <paramref name="slot"/> (0 to 9).</summary>
    public static VoxelCommand SelectSlot(int slot) => new(VoxelCommandKind.SelectSlot, slot);

    /// <summary>Craft the recipe at <paramref name="recipeIndex"/> of the simulation's recipe list.</summary>
    public static VoxelCommand Craft(int recipeIndex) => new(VoxelCommandKind.Craft, recipeIndex);
}
