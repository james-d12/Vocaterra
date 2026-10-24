using Vocaterra.Game.Core.Items;

namespace Vocaterra.Game.Core.Voxel;

/// <summary>
/// The player's body as plain values, in 1/4096-block units and 1/4096-turn angles (the fixed-point units of <see cref="PlayerBody3D"/>).
/// </summary>
/// <param name="MinX">The west edge of the box.</param>
/// <param name="MinY">The bottom of the box (the feet).</param>
/// <param name="MinZ">The north edge of the box.</param>
/// <param name="VelocityX">The velocity along X.</param>
/// <param name="VelocityY">The vertical velocity.</param>
/// <param name="VelocityZ">The velocity along Z.</param>
/// <param name="Grounded">Whether the body stands on a block.</param>
/// <param name="SteppedUp">Whether the last tick stepped up a block (the camera's flag).</param>
/// <param name="Yaw">The facing, in 1/4096 turns (0 north, clockwise).</param>
/// <param name="Pitch">The look angle, positive up.</param>
public readonly record struct PlayerBodyState(
    int MinX,
    int MinY,
    int MinZ,
    int VelocityX,
    int VelocityY,
    int VelocityZ,
    bool Grounded,
    bool SteppedUp,
    int Yaw,
    int Pitch);

/// <summary>
/// Everything the simulation keeps besides its grid (which the scene diff covers): the tick, the hotbar selection, the gathering progress, the
/// last "no bottle" hint, the body and the inventory. A save writes it and <see cref="VoxelSimulation.Restore"/> puts it back.
/// </summary>
/// <param name="Tick">The ticks stepped so far.</param>
/// <param name="SelectedSlot">The selected hotbar slot, 0 to 9.</param>
/// <param name="GatherTarget">The block being gathered, or <see langword="null"/>.</param>
/// <param name="GatherBlock">The kind of that block.</param>
/// <param name="GatherProgress">The ticks of holding already spent on it.</param>
/// <param name="NoBottleHint">The water cell the "no bottle" event was last raised for, or <see langword="null"/>.</param>
/// <param name="Player">The body.</param>
/// <param name="Slots">The inventory's slots, in order (<see cref="Inventory.SlotCount"/> of them).</param>
public sealed record VoxelSimulationState(
    long Tick,
    int SelectedSlot,
    BlockPos? GatherTarget,
    BlockId GatherBlock,
    int GatherProgress,
    BlockPos? NoBottleHint,
    PlayerBodyState Player,
    IReadOnlyList<ItemStack> Slots);
