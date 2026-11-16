using Vocaterra.Game.Core.Mathematics;
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

    private const int CellsPerSide = 4;
    private const int CellSize = VoxelChunk.Size / CellsPerSide;
    private const int GridSide = 3 * CellsPerSide;

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
        var planted = new List<PlantedTree>();
        var reach = world.Strategies.MaxCrownReach;
        if (world.Strategies.MaxTreeHeight == 0)
        {
            return planted;
        }

        var grid = new Candidate[GridSide * GridSide];
        for (var nz = 0; nz < 3; nz++)
        {
            for (var nx = 0; nx < 3; nx++)
            {
                Draw(world.Seed, ctx.ChunkX - 1 + nx, ctx.ChunkZ - 1 + nz, nx * CellsPerSide, nz * CellsPerSide, grid);
            }
        }

        var (minX, minZ) = (ctx.ChunkMinX - reach, ctx.ChunkMinZ - reach);
        var (maxX, maxZ) = (ctx.ChunkMinX + VoxelChunk.Size + reach, ctx.ChunkMinZ + VoxelChunk.Size + reach);
        for (var gz = 1; gz < GridSide - 1; gz++)
        {
            for (var gx = 1; gx < GridSide - 1; gx++)
            {
                var c = grid[(gz * GridSide) + gx];
                if (c.X < minX || c.X >= maxX || c.Z < minZ || c.Z >= maxZ || StandsDown(grid, gx, gz) || Grows(ctx, c) is not { } tree)
                {
                    continue;
                }

                planted.Add(tree);
            }
        }

        planted.Sort(static (a, b) => a.Priority != b.Priority ? a.Priority.CompareTo(b.Priority) : a.CellZ != b.CellZ ? a.CellZ.CompareTo(b.CellZ) : a.CellX.CompareTo(b.CellX));
        return planted;
    }

    // The 16 candidates of a chunk, always the same draws in the same order whatever becomes of them.
    private static void Draw(ulong seed, int chunkX, int chunkZ, int gridX, int gridZ, Candidate[] grid)
    {
        var rng = WorldRng.For(seed, Name, chunkX, chunkZ);
        for (var cz = 0; cz < CellsPerSide; cz++)
        {
            for (var cx = 0; cx < CellsPerSide; cx++)
            {
                var cellX = (chunkX * CellsPerSide) + cx;
                var cellZ = (chunkZ * CellsPerSide) + cz;
                var jx = rng.NextInt(CellSize);
                var jz = rng.NextInt(CellSize);
                var roll = rng.NextInt(100);
                var trunkRoll = rng.NextInt(1 << 16);
                var shape = rng.NextUInt64();
                var priority = rng.NextUInt64();
                grid[((gridZ + cz) * GridSide) + gridX + cx] = new Candidate((cellX * CellSize) + jx, (cellZ * CellSize) + jz, priority, roll, trunkRoll, shape, cellX, cellZ);
            }
        }
    }

    // Whether a candidate within the spacing has the higher priority (the cell breaks a tie): the lower of two neighbours stands down.
    private static bool StandsDown(Candidate[] grid, int gx, int gz)
    {
        var c = grid[(gz * GridSide) + gx];
        for (var dz = -1; dz <= 1; dz++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                if ((dx == 0 && dz == 0) || (uint)(gx + dx) >= GridSide || (uint)(gz + dz) >= GridSide)
                {
                    continue;
                }

                var n = grid[((gz + dz) * GridSide) + gx + dx];
                var (ex, ez) = (n.X - c.X, n.Z - c.Z);
                if ((ex * ex) + (ez * ez) < GenerationWorld.TreeSpacingSquared && (n.Priority > c.Priority || (n.Priority == c.Priority && (dz > 0 || (dz == 0 && dx > 0)))))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // The tree a surviving candidate grows into, or null when its biome, the tree line or its ground says no.
    private static PlantedTree? Grows(ColumnContext ctx, Candidate c)
    {
        var world = ctx.World;
        var biome = ctx.BiomeAt(c.X, c.Z);
        var trees = world.Strategies.Trees[biome.Row.Index];
        if (trees.Density <= 0 || trees.Entries.Length == 0 || c.Roll >= trees.Density || !biome.TreesGrow)
        {
            return null;
        }

        if (world.Blended is { } blended && !blended.TreeAllowed(c.X, c.Z, c.Priority))
        {
            return null; // inside an authored region, beside its border or on an exit road: no trunk, so no crown crosses into the region (B06)
        }

        if (world.Underground is { } underground && underground.Entrances.InZone(c.X, c.Z))
        {
            return null; // no trunk stands in an entrance zone, whether or not the zone opens, so none is left over a shaft (B11)
        }

        var ground = ctx.SurfaceAt(c.X, c.Z);
        if (ground.Water || !BlockDefinition.IsSolid(ground.Top) || BesideWater(ctx, c.X, c.Z))
        {
            return null; // a trunk needs solid dry ground under it (the caves keep a cover of rock under the ground, and none stands in an entrance zone, B11), and none stands in the column a body leaves the water by
        }

        var pick = (int)((c.Shape >> 8) % (ulong)trees.TotalWeight);
        var entry = trees.Entries[^1];
        foreach (var candidate in trees.Entries)
        {
            if (pick < candidate.Weight)
            {
                entry = candidate;
                break;
            }

            pick -= candidate.Weight;
        }

        var species = entry.Species;
        var length = species.MinTrunk + (c.TrunkRoll % (species.MaxTrunk - species.MinTrunk + 1));
        if (ground.Height + length + entry.Crown.Rise >= world.MaxY - 1)
        {
            return null;
        }

        return new PlantedTree(c.X, c.Z, ground.Height, length, species, entry.Crown, c.Shape, c.Priority, c.CellX, c.CellZ);
    }

    // Whether a column of the eight round it is under the sea. The bank's own column must stay open: a trunk there would leave a body in the water no floor to climb out onto but the crown's top.
    private static bool BesideWater(ColumnContext ctx, int x, int z)
    {
        for (var dz = -1; dz <= 1; dz++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                if (ctx.HeightAt(x + dx, z + dz) < ctx.World.SeaLevel)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private readonly record struct Candidate(int X, int Z, ulong Priority, int Roll, int TrunkRoll, ulong Shape, int CellX, int CellZ);
}
