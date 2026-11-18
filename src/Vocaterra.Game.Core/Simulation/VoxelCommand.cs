using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Mathematics;

namespace Vocaterra.Game.Core.Simulation;

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

    /// <summary>Descend button down (a flying body sinks).</summary>
    public static VoxelCommand Descend { get; } = new(VoxelCommandKind.Descend);

    /// <summary>Gather the block at (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>) this tick.</summary>
    public static VoxelCommand Interact(int x, int y, int z) => new(VoxelCommandKind.Interact, x, y, z);

    /// <summary>Place <paramref name="item"/> at (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>).</summary>
    public static VoxelCommand Place(ItemId item, int x, int y, int z) => new(VoxelCommandKind.Place, (int)item, x, y, z);

    /// <summary>Select the hotbar slot <paramref name="slot"/> (0 to 9).</summary>
    public static VoxelCommand SelectSlot(int slot) => new(VoxelCommandKind.SelectSlot, slot);

    /// <summary>Craft the recipe at <paramref name="recipeIndex"/> of the simulation's recipe list.</summary>
    public static VoxelCommand Craft(int recipeIndex) => new(VoxelCommandKind.Craft, recipeIndex);
}

