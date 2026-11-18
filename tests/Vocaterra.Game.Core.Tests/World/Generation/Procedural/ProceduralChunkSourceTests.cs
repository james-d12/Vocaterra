using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Generation.Procedural;

namespace Vocaterra.Game.Core.Tests.World.Generation.Procedural;

/// <summary>The rest of ProceduralChunkSourceTests's tests (B28 split the slow ones into classes of their own, sharing ProceduralChunkSourceTestsBase).</summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class ProceduralChunkSourceTests : ProceduralChunkSourceTestsBase
{
    [Theory]
    [MemberData(nameof(Places))]
    public void Given_ASourceWithAWiderWindow_When_TheSameChunksAreGenerated_Then_TheyAreUnchangedSoTheMarginIsEnough(ulong seed, Feature feature)
    {
        var (cx, cz) = ProceduralTestWorld.Find(seed, feature);
        var normal = ProceduralTestWorld.Source(seed);
        var wide = new ProceduralChunkSource(TestGame.Data.WorldGen, TestGame.Data.Biomes, seed, null, 6, TestGame.Data.Underground);

        Assert.Equal(normal.Margin + 6, wide.Margin);
        foreach (var coord in Neighbourhood(cx, cz, feature == Feature.Mountain ? 8 : 0, feature == Feature.Mountain ? 19 : 3))
        {
            Assert.Equal(normal.Generate(coord).ComputeDigestHex(), wide.Generate(coord).ComputeDigestHex());
        }
    }

    [Fact]
    public void Given_AColumn_When_ItIsGeneratedWholeAndChunkByChunk_Then_EveryLayerEqualsTheSingleChunkAndTheShortcutsChangeNothing()
    {
        var (cx, cz) = ProceduralTestWorld.Find(2026, Feature.Forest);
        var source = ProceduralTestWorld.Source(2026);

        var column = source.GenerateColumn(cx, cz);

        Assert.Equal(source.MaxChunkY - source.MinChunkY, column.Count);
        for (var i = 0; i < column.Count; i++)
        {
            var coord = new VoxelChunkCoord(cx, source.MinChunkY + i, cz);
            Assert.Equal(coord, column[i].Coord);
            Assert.Equal(source.Generate(coord).ComputeDigestHex(), column[i].ComputeDigestHex());
            Assert.Equal(source.GenerateWithoutShortcuts(coord).ComputeDigestHex(), column[i].ComputeDigestHex());
        }
    }

    [Fact]
    public void Given_ChunksAboveAndBelowTheWorld_When_TheyAreGenerated_Then_TheyAreAir()
    {
        var source = ProceduralTestWorld.Source(42);

        Assert.True(source.Generate(new VoxelChunkCoord(0, source.MaxChunkY, 0)).IsEmpty);
        Assert.True(source.Generate(new VoxelChunkCoord(0, source.MinChunkY - 1, 0)).IsEmpty);
        Assert.Equal(-4, source.MinChunkY);
        Assert.Equal(20, source.MaxChunkY);
    }

    [Fact]
    public void Given_AColumnOfAnyPlace_When_ItsLayersAreRead_Then_TheBottomIsSolidRockToTheBedrockFloorAndTheTopIsAir()
    {
        var source = ProceduralTestWorld.Source(7);
        foreach (var feature in new[] { Feature.Plain, Feature.Coast, Feature.Mountain })
        {
            var (cx, cz) = ProceduralTestWorld.Find(7, feature);
            var column = source.GenerateColumn(cx, cz);

            Assert.True(column[0].Blocks[..256].ToArray().All(static b => b == BlockId.Bedrock), $"the floor of {feature} at y -64 is bedrock"); // a cave may reach the lowest chunk above the floor (B11), so the floor row is the check
            Assert.True(column[0].Blocks.ToArray().All(static b => b is not (BlockId.Water or BlockId.Stone)), $"the lowest chunk of {feature} is rock, bedrock, ore, lava or the air of a cave, with no water and no plain stone");
            Assert.True(column[^1].IsEmpty || feature == Feature.Mountain, "the highest chunk of the world is air (a range may reach into it)");
        }
    }

    [Fact]
    public void Given_ASource_When_ManyThreadsGenerateTheSameChunk_Then_TheyAllGetTheSameBlocks()
    {
        var (cx, cz) = ProceduralTestWorld.Find(42, Feature.Forest);
        var source = ProceduralTestWorld.Source(42);
        var coord = new VoxelChunkCoord(cx, 2, cz);
        var expected = source.Generate(coord).ComputeDigestHex();

        var digests = Enumerable.Range(0, 8).AsParallel().WithDegreeOfParallelism(4).Select(_ => source.Generate(coord).ComputeDigestHex()).ToList();

        Assert.All(digests, d => Assert.Equal(expected, d));
    }
}
