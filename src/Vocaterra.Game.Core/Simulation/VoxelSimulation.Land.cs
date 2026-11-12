using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Land;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Simulation;

// The land rules' part (Y09): placing is refused on a protected column (a torch is always allowed) and planks come back only on owned land. The rules are the host's
// (IBuildRules, as WalkPermille is a host setting): not state, not in the digest.
public sealed partial class VoxelSimulation
{
    /// <summary>The ticks it takes to take a placed plank back by hand (half a second; the axe is quicker, as on any wood).</summary>
    public const int PlankTakeBackTicks = 30;

    private BlockPos? _takeBackRefusedAt;

    /// <summary>
    /// What the owner of the land allows (Y09): the session sets it. With none, placing and gathering go as they did before the land rules (a plank is never taken back).
    /// </summary>
    public IBuildRules? BuildRules { get; set; }

    // The definition a gather works with: a plank block is gatherable only where the rules exist (the take-back), giving a plank's worth of wood.
    private BlockDefinition GatherDefinition(BlockId id)
    {
        var definition = BlockDefinition.Get(id);
        return id == BlockId.Planks && BuildRules is not null
            ? definition with { GatherTicks = PlankTakeBackTicks, Drop = ItemId.Wood, DropCount = 1 }
            : definition;
    }

    // Whether the rules refuse the player this plank block; says so once per hold.
    private bool TakeBackRefused(BlockId id, BlockPos cell)
    {
        if (id != BlockId.Planks || BuildRules is not { } rules || rules.MayTakeBack(cell))
        {
            return false;
        }

        if (_takeBackRefusedAt != cell)
        {
            _takeBackRefusedAt = cell;
            _events.Add(new SimulationEvent(SimulationEventKind.TakeBackRefused, Tick, cell.X, cell.Y, cell.Z));
        }

        return true;
    }

    // Whether the rules refuse this block on the cell's column; says why in an event. A torch is always allowed.
    private bool PlaceRefusedByLand(BlockId placed, BlockPos cell)
    {
        if (placed == BlockId.Torch || BuildRules?.ProtectionAt(cell.X, cell.Z) is not { } protection || protection == Protection.None)
        {
            return false;
        }

        _events.Add(new SimulationEvent(SimulationEventKind.PlaceRefused, Tick, cell.X, cell.Y, cell.Z, (int)protection));
        return true;
    }
}
