using Vocaterra.Game.Core.Physics;

namespace Vocaterra.Game.Core.Voxel;

/// <summary>
/// The rules by which a body meets the blocks' shapes, in one place (K03, building plan 3.3): whether a cell blocks a walking body for planning, whether a body can stand on a cell, and
/// whether a block's collision overlaps a box. <see cref="VoxelGrid"/>, the walk planner, the sleep walk and placement call it, so none keeps its own copy. The moving body
/// itself (<see cref="PlayerBody"/>) is resolved against the exact boxes by the collider.
/// </summary>
/// <remarks>
/// <para><b>Planning rule (conservative).</b> A cell blocks a body when its block is solid and has collision boxes (a slab, a stair, a trapdoor, a door panel, a closed gate and a fence all block, even though a
/// player can climb a slab; an open gate has no collision and does not block), or when the cell below is a <see cref="BlockDefinition.Tall"/> block or has a collision box reaching above its top (a fence,
/// a closed gate), or when the block stops a body (lava). Nothing generates the oak pieces in the scene and NPCs never place them, so keeping an NPC off every partial shape is enough.</para>
/// <para><b>Standing rule for planning.</b> A cell can be stood on when it is solid, not tall and its collision is the full cube. A partial shape is never an NPC's floor.</para>
/// </remarks>
public static class BodyCollision
{
    private const int Sixteenth = VoxelPhysics.UnitsPerBlock / 16; // 256 units

    /// <summary>Whether the cell blocks a walking body (the planning rule above; the valley's wall is the grid's own and not read here).</summary>
    /// <typeparam name="T">The world reader (a type parameter so that a struct view is not boxed).</typeparam>
    /// <param name="grid">The world.</param>
    /// <param name="x">The cell's x.</param>
    /// <param name="y">The cell's y.</param>
    /// <param name="z">The cell's z.</param>
    public static bool BlocksBody<T>(T grid, int x, int y, int z)
        where T : IVoxelReader
    {
        var id = grid.GetBlock(x, y, z);
        var below = grid.GetBlock(x, y - 1, z);
        if (BlockDefinition.StopsBodyOf(id) || BlockDefinition.IsTall(below))
        {
            return true;
        }

        if (BlockShapes.HasShape(id))
        {
            if (BlockShapes.Of(id, grid.GetState(x, y, z)).Collides.Length > 0)
            {
                return true;
            }
        }
        else if (grid.IsSolid(x, y, z))
        {
            return true;
        }

        return BlockShapes.HasShape(below) && BlockShapes.Of(below, grid.GetState(x, y - 1, z)).ReachesAboveCell;
    }

    /// <summary>Whether a walking body can stand on the cell (the standing rule above).</summary>
    /// <typeparam name="T">The world reader.</typeparam>
    /// <param name="grid">The world.</param>
    /// <param name="x">The cell's x.</param>
    /// <param name="y">The cell's y.</param>
    /// <param name="z">The cell's z.</param>
    public static bool CanStandOn<T>(T grid, int x, int y, int z)
        where T : IVoxelReader
    {
        var id = grid.GetBlock(x, y, z);
        return BlockShapes.HasShape(id)
            ? BlockShapes.Of(id, grid.GetState(x, y, z)).CollidesAsCube
            : grid.IsSolid(x, y, z) && !BlockDefinition.IsTall(id);
    }

    /// <summary>Whether the collision of the block <paramref name="id"/> in <paramref name="state"/> at the cell shares volume with the box (in units; touching does not count). A solid block with no shape of its own is the full cube.</summary>
    public static bool Overlaps(BlockId id, byte state, BlockPos cell, int minX, int minY, int minZ, int maxX, int maxY, int maxZ)
    {
        var ox = cell.X * VoxelPhysics.UnitsPerBlock;
        var oy = cell.Y * VoxelPhysics.UnitsPerBlock;
        var oz = cell.Z * VoxelPhysics.UnitsPerBlock;
        foreach (var box in BlockShapes.Of(id, state).Collides)
        {
            if (minX < ox + (box.MaxX * Sixteenth) && maxX > ox + (box.MinX * Sixteenth)
                && minY < oy + (box.MaxY * Sixteenth) && maxY > oy + (box.MinY * Sixteenth)
                && minZ < oz + (box.MaxZ * Sixteenth) && maxZ > oz + (box.MinZ * Sixteenth))
            {
                return true;
            }
        }

        return false;
    }
}
