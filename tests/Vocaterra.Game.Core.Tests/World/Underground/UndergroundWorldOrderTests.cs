using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Generation.Procedural;

namespace Vocaterra.Game.Core.Tests.World.Underground;

/// <summary>Tests split out of UndergroundWorldTests (B28), so they run beside the others: AnyOrderAndOnManyThreads.</summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class UndergroundWorldOrderTests : UndergroundWorldTestsBase
{
    [Theory]
    [InlineData(42UL, Feature.Forest)]
    [InlineData(7UL, Feature.Mountain)]
    [InlineData(2026UL, Feature.Plain)]
    public void Given_ACaveRichPlace_When_ItsChunksAreGeneratedInAnyOrderAndOnManyThreads_Then_EveryChunkEqualsTheSameChunkAlone(ulong seed, Feature feature)
    {
        var (cx, cz) = ProceduralTestWorld.Find(seed, feature);
        var coords = new List<VoxelChunkCoord>();
        for (var z = cz - 1; z <= cz + 1; z++)
        {
            for (var x = cx - 1; x <= cx + 1; x++)
            {
                for (var y = -4; y <= (feature == Feature.Mountain ? 12 : 3); y++)
                {
                    coords.Add(new VoxelChunkCoord(x, y, z));
                }
            }
        }

        var rng = new Vocaterra.Game.Core.Mathematics.WorldRng(seed);
        var shuffled = new List<VoxelChunkCoord>(coords);
        for (var i = shuffled.Count - 1; i > 0; i--)
        {
            var j = rng.NextInt(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        var among = new ProceduralChunkSource(TestGame.Data.WorldGen, TestGame.Data.Biomes, seed, null, Data);
        var amongDigests = shuffled.ToDictionary(static c => c, c => among.Generate(c).ComputeDigestHex());
        var threaded = coords.AsParallel().WithDegreeOfParallelism(8).Select(c => (c, ProceduralTestWorld.Source(seed).Generate(c).ComputeDigestHex())).ToDictionary(static p => p.c, static p => p.Item2);
        foreach (var coord in coords)
        {
            var alone = new ProceduralChunkSource(TestGame.Data.WorldGen, TestGame.Data.Biomes, seed, null, Data).Generate(coord).ComputeDigestHex();
            Assert.Equal(alone, amongDigests[coord]);
            Assert.Equal(alone, threaded[coord]);
        }
    }
}
