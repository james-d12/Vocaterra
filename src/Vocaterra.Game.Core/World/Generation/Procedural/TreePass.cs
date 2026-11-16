using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>A tree that will stand: where, how tall, and what it is made of.</summary>
/// <param name="X">The trunk's x.</param>
/// <param name="Z">The trunk's z.</param>
/// <param name="BaseY">The y of the ground under the trunk.</param>
/// <param name="Length">The trunk's length in blocks.</param>
/// <param name="Species">The species.</param>
/// <param name="Crown">The crown shape.</param>
/// <param name="Shape">The tree's own random bits.</param>
/// <param name="Priority">The tree's hash priority: the order trees write their leaves in.</param>
/// <param name="CellX">The x of the cell the tree came from, in cells (a chunk holds 4).</param>
/// <param name="CellZ">The z of the cell.</param>
internal readonly record struct PlantedTree(int X, int Z, int BaseY, int Length, ITreeSpecies Species, ICrownShape Crown, ulong Shape, ulong Priority, int CellX, int CellZ);

/// <summary>
/// The trees (B03, biomes plan section 3.4). A chunk holds a jittered grid of 4 x 4 cells of 4 x 4 blocks, each with one candidate trunk at a jittered place, drawn from
/// <c>WorldRng.For(seed, "gen/trees", chunkX, chunkZ)</c> in a fixed order, so a candidate is a function of its own chunk alone. Spacing is settled by hash priority: a candidate stands down
/// when another within three blocks has the higher priority, whichever chunk it belongs to. A survivor grows where its biome's tree density says (a roll against the percent of cells that hold a
/// tree), the tree line allows it, and solid dry ground is under the trunk. A chunk takes the candidates of its eight neighbours too, so a crown that hangs over a border is the same on both sides,
/// and writes only the blocks inside itself: all the trunks first, then the leaves in the order of the priorities, each only into air, so no order of generation changes a block.
/// </summary>
internal sealed class TreePass : IChunkDecorator
{
    /// <summary>The pass's name, which is also the key of its random draws.</summary>
    public const string Name = "gen/trees";

    /// <inheritdoc />
    public string PassName => Name;

    /// <inheritdoc />
    public void Decorate(ColumnContext ctx, ChunkBuilder chunk)
    {
        var trees = ctx.Trees;
        foreach (var tree in trees)
        {
            if (tree.BaseY + 1 > chunk.MaxY || tree.BaseY + tree.Length < chunk.MinY)
            {
                continue;
            }

            for (var y = tree.BaseY + 1; y <= tree.BaseY + tree.Length; y++)
            {
                chunk.Set(tree.X, y, tree.Z, tree.Species.Trunk);
            }
        }

        foreach (var tree in trees)
        {
            var top = tree.BaseY + tree.Length;
            if (top + tree.Crown.Rise < chunk.MinY)
            {
                continue;
            }

            var profile = tree.Crown.Profile(tree.Length, tree.Shape);
            var first = top + profile.TopOffset;
            for (var k = 0; k < profile.Limits.Count; k++)
            {
                var y = first - k;
                if (y < chunk.MinY || y > chunk.MaxY)
                {
                    continue;
                }

                var limit = profile.Limits[k];
                var reach = StandardTrees.ReachOf(limit);
                for (var dz = -reach; dz <= reach; dz++)
                {
                    for (var dx = -reach; dx <= reach; dx++)
                    {
                        var d2 = (dx * dx) + (dz * dz);
                        if (d2 > limit || !chunk.Contains(tree.X + dx, y, tree.Z + dz) || chunk.Get(tree.X + dx, y, tree.Z + dz) != BlockId.Air
                            || StandardTrees.DropsRimLeaf(tree.Shape, dx, dz, k, limit, d2))
                        {
                            continue;
                        }

                        chunk.Set(tree.X + dx, y, tree.Z + dz, tree.Species.Leaves);
                    }
                }
            }
        }
    }

    // The trees whose crowns can reach the chunk column, in the order they write their leaves.
    internal static List<PlantedTree> Plan(ColumnContext ctx)
    {
        var world = ctx.World;
        var reach = world.Strategies.MaxCrownReach;
        return TreePlacement.Plan(world, ctx, ctx.ChunkX, ctx.ChunkZ, 1, 1, ctx.ChunkMinX - reach, ctx.ChunkMinZ - reach, ctx.ChunkMinX + VoxelChunk.Size + reach, ctx.ChunkMinZ + VoxelChunk.Size + reach);
    }
}
