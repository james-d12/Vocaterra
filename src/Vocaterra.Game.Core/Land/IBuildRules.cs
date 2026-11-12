using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Land;

/// <summary>
/// The rules hook of <see cref="Simulation.VoxelSimulation"/> (Y09, as <c>WalkPermille</c> is a host setting): what the world's owner of the land allows. The simulation asks it before
/// it places a block or takes a placed plank back; the answer is a rule, not state, so it is not in the simulation's digest. A simulation nobody sets a hook on allows what it always did.
/// </summary>
public interface IBuildRules
{
    /// <summary>The protection of a column, or <see cref="Protection.None"/> when blocks may be placed on it. A torch is always allowed and never asks.</summary>
    /// <param name="x">The column's x.</param>
    /// <param name="z">The column's z.</param>
    Protection ProtectionAt(int x, int z);

    /// <summary>Whether the player may take back the planks of a cell (only on owned land).</summary>
    /// <param name="cell">The cell of the plank.</param>
    bool MayTakeBack(BlockPos cell);

    /// <summary>Whether the cell holds a block of the camp the player set up (Y12): holding the gather button on it packs the whole camp back into the kit instead of gathering the block.</summary>
    /// <param name="cell">The cell aimed at.</param>
    bool IsCampBlock(BlockPos cell);
}
