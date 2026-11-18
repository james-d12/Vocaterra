using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Biomes;

namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>What the tree placement reads of the ground (B07c): the biome of a column, the layers of its surface and the water's top, either as the generator has shaped them (a <see cref="ColumnContext"/>) or as the height model alone gives them (the far view's sampler).</summary>
internal interface ITreeGround
{
    /// <summary>The biome of a column.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    BiomeColumn BiomeAt(int x, int z);

    /// <summary>The layers of a column.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    ColumnSurface SurfaceAt(int x, int z);

    /// <summary>The y of the water's top in a column, or -1 where there is none.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    int WaterLevelAt(int x, int z);

    /// <summary>Whether a cave entrance's passage, mouth or damp zone keeps trees off the column (B11b).</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    bool ClearsEntrance(int x, int z);
}

/// <summary>
/// Where the trees stand (B03, shared with the far view in B07c): a pure query over a block of chunk columns. A chunk holds a jittered grid of 4 x 4 cells of 4 x 4 blocks, each with one candidate trunk at a jittered place,
/// drawn from <c>WorldRng.For(seed, "gen/trees", chunkX, chunkZ)</c> in a fixed order, so a candidate is a function of its own chunk alone. Spacing is settled by hash priority: a candidate stands down when another within three
/// blocks has the higher priority, whichever chunk it belongs to. A survivor grows where its biome's tree density says (a roll against the percent of cells that hold a tree), the tree line allows it, and solid dry ground is under
/// the trunk. The tree pass calls this for a chunk column with the generator's own ground; the far view calls it for a tile's chunks with the height model's ground, so it knows every tree without generating a chunk.
/// </summary>
internal static class TreePlacement
{
    /// <summary>The cells along a chunk's side.</summary>
    public const int CellsPerSide = 4;

    private const int CellSize = VoxelChunk.Size / CellsPerSide;

    /// <summary>The trees of the chunk columns (<paramref name="chunkX"/>, <paramref name="chunkZ"/>) to the <paramref name="chunksX"/> by <paramref name="chunksZ"/> block of them whose trunk stands in the rectangle, in the order they write their leaves.</summary>
    /// <param name="world">The world.</param>
    /// <param name="ground">The ground the trees ask.</param>
    /// <param name="chunkX">The first chunk column's x.</param>
    /// <param name="chunkZ">The first chunk column's z.</param>
    /// <param name="chunksX">How many chunk columns along x.</param>
    /// <param name="chunksZ">How many chunk columns along z.</param>
    /// <param name="minX">The rectangle's first x (a trunk at it counts).</param>
    /// <param name="minZ">The rectangle's first z.</param>
    /// <param name="maxX">One past the rectangle's last x.</param>
    /// <param name="maxZ">One past the rectangle's last z.</param>
    public static List<PlantedTree> Plan(GenerationWorld world, ITreeGround ground, int chunkX, int chunkZ, int chunksX, int chunksZ, int minX, int minZ, int maxX, int maxZ)
    {
        var planted = new List<PlantedTree>();
        if (world.Strategies.MaxTreeHeight == 0)
        {
            return planted;
        }

        var width = (chunksX + 2) * CellsPerSide;
        var depth = (chunksZ + 2) * CellsPerSide;
        var grid = new Candidate[width * depth];
        for (var nz = 0; nz < chunksZ + 2; nz++)
        {
            for (var nx = 0; nx < chunksX + 2; nx++)
            {
                Draw(world.Seed, chunkX - 1 + nx, chunkZ - 1 + nz, nx * CellsPerSide, nz * CellsPerSide, width, grid);
            }
        }

        for (var gz = 1; gz < depth - 1; gz++)
        {
            for (var gx = 1; gx < width - 1; gx++)
            {
                var c = grid[(gz * width) + gx];
                if (c.X < minX || c.X >= maxX || c.Z < minZ || c.Z >= maxZ || StandsDown(grid, width, depth, gx, gz) || Grows(world, ground, c) is not { } tree)
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
    private static void Draw(ulong seed, int chunkX, int chunkZ, int gridX, int gridZ, int width, Candidate[] grid)
    {
        var rng = WorldRng.For(seed, TreePass.Name, chunkX, chunkZ);
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
                grid[((gridZ + cz) * width) + gridX + cx] = new Candidate((cellX * CellSize) + jx, (cellZ * CellSize) + jz, priority, roll, trunkRoll, shape, cellX, cellZ);
            }
        }
    }

    // Whether a candidate within the spacing has the higher priority (the cell breaks a tie): the lower of two neighbours stands down.
    private static bool StandsDown(Candidate[] grid, int width, int depth, int gx, int gz)
    {
        var c = grid[(gz * width) + gx];
        for (var dz = -1; dz <= 1; dz++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                if ((dx == 0 && dz == 0) || (uint)(gx + dx) >= width || (uint)(gz + dz) >= depth)
                {
                    continue;
                }

                var n = grid[((gz + dz) * width) + gx + dx];
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
    private static PlantedTree? Grows(GenerationWorld world, ITreeGround ctx, Candidate c)
    {
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

        if (ctx.ClearsEntrance(c.X, c.Z))
        {
            return null; // no trunk stands over a cave entrance's passage or at its mouth (B11b)
        }

        var ground = ctx.SurfaceAt(c.X, c.Z);
        if (ground.Water || ground.Bare || !BlockDefinition.IsSolid(ground.Top) || BesideWater(ctx, c.X, c.Z))
        {
            return null; // a trunk needs solid dry ground under it that no cliff, scree or river bed has bared (B11b), and none stands in the column a body leaves the water by
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

    // Whether a column of the eight round it holds water (the sea's, a river's or a lake's). The bank's own column must stay open: a trunk there would leave a body in the water no floor to climb out onto but the crown's top.
    private static bool BesideWater(ITreeGround ctx, int x, int z)
    {
        for (var dz = -1; dz <= 1; dz++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                if (ctx.WaterLevelAt(x + dx, z + dz) >= 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private readonly record struct Candidate(int X, int Z, ulong Priority, int Roll, int TrunkRoll, ulong Shape, int CellX, int CellZ);
}
