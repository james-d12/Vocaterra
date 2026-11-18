using System.Collections.Concurrent;
using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Streaming;

namespace Vocaterra.Game.Core.Tests.World.Regions;

/// <summary>The composite source (B06): the region's chunks inside it, the procedural world under and outside it, air above it, whole columns through one window, and the same chunk from any thread.</summary>
public sealed class CompositeChunkSourceTests
{
    private static CompositeChunkSource Source => RegionTestWorld.Composition().Source;

    [Fact]
    public void Given_ChunksInsideTheValley_When_TheyAreGenerated_Then_TheyAreTheRegionsOwn()
    {
        var region = RegionTestWorld.Valley();
        foreach (var coord in new[] { new VoxelChunkCoord(0, 0, 0), new VoxelChunkCoord(12, 2, 13), new VoxelChunkCoord(23, 7, 23), new VoxelChunkCoord(22, 3, 13), new VoxelChunkCoord(5, 5, 20) })
        {
            Assert.Same(region.Copy(coord), Source.Generate(coord));
        }
    }

    [Fact]
    public void Given_TheColumnsOfTheValley_When_TheLayersAboveItAreGenerated_Then_TheyAreAir()
    {
        foreach (var (cx, cz) in new[] { (0, 0), (12, 12), (23, 23), (22, 13) })
        {
            for (var cy = 8; cy < Source.MaxChunkY; cy++)
            {
                Assert.True(Source.Generate(new VoxelChunkCoord(cx, cy, cz)).IsEmpty, $"chunk {cx},{cy},{cz}");
            }
        }
    }

    [Fact]
    public void Given_TheColumnsOfTheValley_When_TheLayersUnderItAreGenerated_Then_TheyAreTheProceduralUndergroundNotSolidStone()
    {
        // B11: under the valley the procedural strata, ores, caves and floor fill y -64 to 0 (the valley's own stone is B13's).
        Assert.Equal(-4, Source.MinChunkY);
        var seen = new HashSet<BlockId>();
        foreach (var (cx, cz) in new[] { (0, 0), (12, 12), (23, 23), (22, 13), (6, 9) })
        {
            for (var cy = Source.MinChunkY; cy < 0; cy++)
            {
                var chunk = Source.Generate(new VoxelChunkCoord(cx, cy, cz));
                Assert.Equal(RegionTestWorld.Composition().Procedural.Generate(new VoxelChunkCoord(cx, cy, cz)).ComputeDigestHex(), chunk.ComputeDigestHex());
                seen.UnionWith(chunk.Blocks.ToArray());
            }
        }

        Assert.Contains(BlockId.Slate, seen);
        Assert.Contains(BlockId.Bedrock, seen);
        Assert.Contains(BlockId.Air, seen); // caves
        Assert.DoesNotContain(BlockId.Stone, seen); // stone starts at y 8, in the valley's own layers
    }

    [Fact]
    public void Given_ChunksOutsideTheValley_When_TheyAreGenerated_Then_TheyAreTheBlendedProceduralWorld()
    {
        var composition = RegionTestWorld.Composition();
        foreach (var coord in new[] { new VoxelChunkCoord(24, 3, 13), new VoxelChunkCoord(-1, 2, 5), new VoxelChunkCoord(10, 4, 24), new VoxelChunkCoord(40, 2, -7), new VoxelChunkCoord(24, 8, 13), new VoxelChunkCoord(100, 1, 100) })
        {
            Assert.Equal(composition.Procedural.Generate(coord).ComputeDigestHex(), Source.Generate(coord).ComputeDigestHex());
        }
    }

    [Fact]
    public void Given_Columns_When_TheyAreGeneratedWholeAndChunkByChunk_Then_TheyAreEqual()
    {
        var layers = Source.MaxChunkY - Source.MinChunkY;
        foreach (var (cx, cz) in new[] { (10, 10), (23, 13), (24, 13), (-1, -1), (50, 7), (0, 24) })
        {
            var column = Source.GenerateColumn(cx, cz);
            Assert.Equal(layers, column.Count);
            for (var i = 0; i < layers; i++)
            {
                var coord = new VoxelChunkCoord(cx, Source.MinChunkY + i, cz);
                Assert.Equal(coord, column[i].Coord);
                Assert.Equal(Source.Generate(coord).ComputeDigestHex(), column[i].ComputeDigestHex());
            }
        }
    }

    [Fact]
    public void Given_ManyThreads_When_TheyGenerateTheSameChunksInDifferentOrders_Then_EveryChunkIsTheSameAsOnOneThread()
    {
        var coords = new List<VoxelChunkCoord>();
        for (var cz = 10; cz <= 14; cz++)
        {
            for (var cx = 22; cx <= 27; cx++)
            {
                for (var cy = 0; cy < 8; cy++)
                {
                    coords.Add(new VoxelChunkCoord(cx, cy, cz));
                }
            }
        }

        var alone = new Dictionary<VoxelChunkCoord, string>();
        foreach (var coord in coords)
        {
            alone[coord] = RegionTestWorld.Composition().Source.Generate(coord).ComputeDigestHex();
        }

        var fresh = Vocaterra.Game.Core.World.WorldFactory.Compose(TestGame.Data, RegionTestWorld.Seed, RegionTestWorld.Composition().Source.Regions); // a second source, so the threads share nothing but the pure parts
        var results = new ConcurrentDictionary<(int Thread, VoxelChunkCoord Coord), string>();
        var rng = new WorldRng(9);
        var orders = Enumerable.Range(0, 8).Select(t => coords.OrderBy(_ => rng.NextInt(1 << 20)).ToList()).ToList();
        Parallel.For(0, orders.Count, new ParallelOptions { MaxDegreeOfParallelism = 8 }, t =>
        {
            var source = t % 2 == 0 ? fresh.Source : RegionTestWorld.Composition().Source;
            foreach (var coord in orders[t])
            {
                results[(t, coord)] = source.Generate(coord).ComputeDigestHex();
            }
        });

        Assert.Equal(orders.Count * coords.Count, results.Count);
        Assert.All(results, r => Assert.Equal(alone[r.Key.Coord], r.Value));
    }
}
