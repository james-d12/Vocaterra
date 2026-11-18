using System.Collections.Concurrent;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Far;
using Vocaterra.Game.Core.World.Lod;
using Vocaterra.Game.Core.World.Regions;
using Vocaterra.Game.Core.World.Streaming;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Far;

/// <summary>The shared parts of the test classes split out of LodStoreTests (B28: xUnit runs the tests of one class one after another, so the slow ones are in classes of their own): the fields and helpers.</summary>
public abstract class LodStoreTestsBase(ITestOutputHelper output)
{
    internal sealed class MemoryDisk : ILodPersistence
    {
        public ConcurrentDictionary<LodSectionKey, byte[]> Files { get; } = new();

        public byte[]? Load(LodSectionKey key) => Files.TryGetValue(key, out var blob) ? blob : null;

        public int SaveDelayMilliseconds { get; init; }

        public void Save(LodSectionKey key, byte[] blob)
        {
            if (SaveDelayMilliseconds > 0)
            {
                Thread.Sleep(SaveDelayMilliseconds); // a slow disk keeps a store step open longer, so the threads of a test overlap
            }

            Files[key] = blob;
        }
    }

    internal static LodWorld WorldOf(ulong seed, LodStore? store = null, int treeLevels = 2)
    {
        var sampler = new FarTerrainSampler(ProceduralTestWorld.Source(seed).World, RegionCatalog.Empty);
        return sampler.FarWorld(store ?? new LodStore(), treeLevels);
    }

    internal static LodSection Mixed(Func<int, int, int, BlockId> block)
    {
        var section = LodSection.Air();
        for (var y = 0; y < 32; y++)
        {
            for (var z = 0; z < 32; z++)
            {
                for (var x = 0; x < 32; x++)
                {
                    section.SetBlock(LodSection.Index(x, y, z), block(x, y, z));
                }
            }
        }

        return section;
    }

    // A sample of the model's sea: a block at least 40 blocks from any land, found on a grid (the water's own level is the sea's).
    internal static (int X, int Z, int Level) OpenSea(FarTerrainSampler sampler)
    {
        for (var z = -9000; z <= 9000; z += 397)
        {
            for (var x = -9000; x <= 9000; x += 401)
            {
                var all = true;
                for (var k = 0; k < 9 && all; k++)
                {
                    all = sampler.At(x + (((k % 3) - 1) * 40), z + (((k / 3) - 1) * 40)).IsWater;
                }

                if (all)
                {
                    return (x, z, sampler.At(x, z).WaterLevel);
                }
            }
        }

        throw new InvalidOperationException("no open sea");
    }

    // The generator's stack as a ring ingest makes it (stone under chunk row 4, a column of planks in each footprint) and an edit's chunk (a house block in the second section up).
    internal static ChunkData? GeneratorChunk(int cx, int cy, int cz) => cy < 4 ? new ChunkData(new VoxelChunkCoord(cx, cy, cz), Enumerable.Repeat(BlockId.Timber, ChunkData.Volume).ToArray(), null) : null; // a block the model never makes, so a section the model made is told from one the generator did

    internal static BlockId[] HouseChunk()
    {
        var blocks = new BlockId[ChunkData.Volume];
        blocks[(((3 << 4) | 5) << 4) | 7] = BlockId.Planks;
        return blocks;
    }

    internal static void AssertSameBlocks(LodSection expected, LodSection actual, string where)
    {
        for (var i = 0; i < LodSection.Volume; i++)
        {
            if (expected.BlockAt(i) != actual.BlockAt(i))
            {
                Assert.Fail($"{where}: voxel {i} is {actual.BlockAt(i)}, not {expected.BlockAt(i)}");
            }
        }
    }

    protected ITestOutputHelper Output => output;
}
