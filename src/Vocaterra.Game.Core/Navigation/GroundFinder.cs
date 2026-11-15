using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Navigation;

/// <summary>Where a standing player's feet go in a column of the world: the one rule of <c>--teleport</c> and of the menu's teleport.</summary>
public static class GroundFinder
{
    /// <summary>
    /// The cell the feet go to for the ground of a column: the highest block that is not a tree, not water and not air, with two free cells over it. <see langword="null"/>
    /// when the column lies outside the world or has no such ground (a roof of solid blocks over the first ground found counts as none).
    /// </summary>
    /// <param name="grid">The world.</param>
    /// <param name="x">The block column's x.</param>
    /// <param name="z">The block column's z.</param>
    public static BlockPos? StandingAt(IVoxelReader grid, int x, int z)
    {
        ArgumentNullException.ThrowIfNull(grid);
        if (!grid.InBounds(x, grid.Limits.MinY, z))
        {
            return null;
        }

        for (var y = grid.Limits.EndY - 3; y > grid.Limits.MinY; y--)
        {
            var block = grid.GetBlock(x, y, z);
            if (block is BlockId.Air or BlockId.Water || BlockDefinition.IsLeaves(block) || BlockDefinition.IsTrunk(block) || !grid.IsSolid(x, y, z))
            {
                continue;
            }

            // BlocksBody, not IsSolid: the cell over a tall block (a fence, #33) blocks the body too, so nothing stands on a fence.
            return grid.BlocksBody(x, y + 1, z) || grid.BlocksBody(x, y + 2, z) ? null : new BlockPos(x, y + 1, z);
        }

        return null;
    }
}
