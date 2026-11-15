using System.Collections.Concurrent;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Biomes;
using Vocaterra.Game.Core.World.Climate;
using Vocaterra.Game.Core.World.Generation.Procedural;
using Vocaterra.Game.Core.World.Streaming;

namespace Vocaterra.Game.Core.Tests.TestSupport;

/// <summary>What a test looks for in the procedural world (B03).</summary>
public enum Feature
{
    River,
    Lake,
    Coast,
    Forest,
    Mountain,
    Plain,
}

/// <summary>Shared set-up of the procedural tests: sources of the shipped data (pure, so one per seed is shared), a search for a place with a feature, and a small grid built from generated chunks.</summary>
internal static class ProceduralTestWorld
{
    private static readonly ConcurrentDictionary<ulong, ProceduralChunkSource> Sources = new();

    /// <summary>The seeds the procedural goldens and the water checks run over.</summary>
    public static readonly ulong[] Seeds = [42, 7, 2026];

    /// <summary>The shipped data's source for a seed.</summary>
    public static ProceduralChunkSource Source(ulong seed) => Sources.GetOrAdd(seed, static s => new ProceduralChunkSource(TestGame.Data.WorldGen, TestGame.Data.Biomes, s));

    /// <summary>
    /// The chunk column nearest the origin (searched in rings of 64 blocks outwards) that has the feature: a river or lake in it, a coast (sea and a beach), a woodland with
    /// trees, a mountain above 190, or plain open grass.
    /// </summary>
    public static (int X, int Z) Find(ulong seed, Feature feature, int skip = 0)
    {
        var data = TestGame.Data;
        var climate = new ClimateSampler(data.WorldGen, seed);
        var model = new HeightModel(data.WorldGen, seed);
        var table = new BiomeTable(data.Biomes, data.WorldGen, seed);
        for (var ring = 0; ring < 400; ring++)
        {
            for (var gz = -ring; gz <= ring; gz++)
            {
                for (var gx = -ring; gx <= ring; gx++)
                {
                    if (Math.Max(Math.Abs(gx), Math.Abs(gz)) != ring)
                    {
                        continue;
                    }

                    var (x, z) = (gx * 64, gz * 64);
                    var c = climate.At(x, z);
                    var shape = model.Shape(x, z, c);
                    var id = table.Classify(c, shape.Height, x, z).Row.Id;
                    var hit = feature switch
                    {
                        Feature.River => id == "river-channel" && shape.RiverWeight > 3000,
                        Feature.Lake => id == "lake-basin" && shape.LakeWeight > 3000,
                        Feature.Coast => id == "beach" && Around(model, climate, x, z, shape.Height, static (_, s) => s.Water, 24),
                        Feature.Forest => id == "dense-woodland" && shape.Height is > 34 and < 70,
                        Feature.Mountain => shape.Height >= 200,
                        _ => id == "plains" && shape.Height is > 36 and < 50,
                    };
                    if (hit && skip-- == 0)
                    {
                        return (x >> 4, z >> 4);
                    }
                }
            }
        }

        throw new InvalidOperationException($"No {feature} found near the origin for seed {seed}.");
    }

    private static bool Around(HeightModel model, ClimateSampler climate, int x, int z, int height, Func<int, ColumnShape, bool> test, int radius)
    {
        foreach (var (dx, dz) in new[] { (radius, 0), (-radius, 0), (0, radius), (0, -radius), (radius, radius), (-radius, -radius) })
        {
            if (test(height, model.Shape(x + dx, z + dz, climate.At(x + dx, z + dz))))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// A bounded grid of the chunks of an area, the chunk columns (x0, z0) to (x0 + width - 1, z0 + depth - 1) from the layer 0 up to <paramref name="layers"/> layers, with the area's own corner as the grid's
    /// origin (so a world position is the grid's plus 16 x chunk). Chunks come from the source one by one, as a streamed world would install them.
    /// </summary>
    public static VoxelGrid Grid(ProceduralChunkSource source, int x0, int z0, int width, int depth, int layers)
    {
        var grid = new VoxelGrid(width * 16, layers * 16, depth * 16);
        for (var cz = 0; cz < depth; cz++)
        {
            for (var cx = 0; cx < width; cx++)
            {
                for (var cy = 0; cy < layers; cy++)
                {
                    Install(grid, source.Generate(new VoxelChunkCoord(x0 + cx, cy, z0 + cz)), cx * 16, cy * 16, cz * 16);
                }
            }
        }

        return grid;
    }

    private static void Install(VoxelGrid grid, ChunkData chunk, int ox, int oy, int oz)
    {
        if (chunk.IsEmpty)
        {
            return;
        }

        var blocks = chunk.Blocks;
        for (var ly = 0; ly < 16; ly++)
        {
            for (var lz = 0; lz < 16; lz++)
            {
                for (var lx = 0; lx < 16; lx++)
                {
                    var block = blocks[ChunkData.Index(lx, ly, lz)];
                    if (block != BlockId.Air)
                    {
                        _ = grid.SetBlock(ox + lx, oy + ly, oz + lz, block);
                    }
                }
            }
        }
    }
}
