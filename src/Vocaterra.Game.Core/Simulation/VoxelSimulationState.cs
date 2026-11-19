using Vocaterra.Game.Core.Atmosphere;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.Water.Flow;

namespace Vocaterra.Game.Core.Simulation;

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
/// <param name="Slots">The inventory's active slots, in order: as many as the base and the worn bag give (<see cref="Inventory.SlotCount"/>), so the list's length is the active count (I03b).</param>
/// <param name="Air">The player's air in ticks (water-plan H03); optional, full when missing, so older saves load.</param>
/// <param name="Flow">The cells waiting for a flow update, in queue order (water-plan H06); optional, none when missing, so older saves load.</param>
/// <param name="Atmosphere">The day clock's offset and held hour (E01) and the weather's state (E04); <see langword="null"/> is a new game's, so older saves load.</param>
/// <param name="Worn">The worn items, one stack per slot in the data's order (I03a); <see langword="null"/> when nothing is worn. The save writes it by group, slot and item name (version 10, I03b).</param>
public sealed record VoxelSimulationState(
    long Tick,
    int SelectedSlot,
    BlockPos? GatherTarget,
    BlockId GatherBlock,
    int GatherProgress,
    BlockPos? NoBottleHint,
    PlayerBodyState Player,
    IReadOnlyList<ItemStack> Slots,
    int Air = Water.Breath.MaxAir,
    IReadOnlyList<FlowEntry>? Flow = null,
    AtmosphereState? Atmosphere = null,
    IReadOnlyList<ItemStack>? Worn = null);

