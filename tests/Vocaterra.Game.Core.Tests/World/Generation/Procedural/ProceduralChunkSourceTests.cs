using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Generation.Procedural;

namespace Vocaterra.Game.Core.Tests.World.Generation.Procedural;

/// <summary>
/// A chunk is a function of the seed, the data and its coordinates (B03): the same alone and among its neighbours, in any order, from any instance of the source, with any window wider than the
/// one it needs, and through the column shortcut or the single chunk.
/// </summary>
public sealed class ProceduralChunkSourceTests
{
    public static TheoryData<ulong, Feature> Places => new()
    {
        { 42, Feature.Forest },
        { 7, Feature.Coast },
        { 2026, Feature.River },
        { 42, Feature.Mountain },
    };

    private static List<VoxelChunkCoord> Neighbourhood(int cx, int cz, int firstLayer, int lastLayer)
    {
        var coords = new List<VoxelChunkCoord>();
        for (var z = cz - 1; z <= cz + 1; z++)
        {
            for (var x = cx - 1; x <= cx + 1; x++)
            {
                for (var y = firstLayer; y <= lastLayer; y++)
                {
                    coords.Add(new VoxelChunkCoord(x, y, z));
                }
            }
        }

        return coords;
    }

    // A fixed shuffle: the order is a function of the seed, so a failure is the same failure on the next run.
    private static List<VoxelChunkCoord> Shuffled(List<VoxelChunkCoord> coords, ulong seed)
    {
        var rng = new Vocaterra.Game.Core.Mathematics.WorldRng(seed);
        var list = new List<VoxelChunkCoord>(coords);
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.NextInt(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }

        return list;
    }

    [Theory]
    [MemberData(nameof(Places))]
    public void Given_APlaceAndItsNeighbours_When_TheyAreGeneratedInAnyOrderOnOneSourceAndOneByOneOnOthers_Then_EveryChunkEqualsTheSameChunkGeneratedAlone(ulong seed, Feature feature)
    {
        var (cx, cz) = ProceduralTestWorld.Find(seed, feature);
        var coords = Neighbourhood(cx, cz, feature == Feature.Mountain ? 0 : -1, feature == Feature.Mountain ? 19 : 7);
        var among = new ProceduralChunkSource(TestGame.Data.WorldGen, TestGame.Data.Biomes, seed);
        var generated = new Dictionary<VoxelChunkCoord, string>();
        foreach (var coord in Shuffled(coords, seed ^ 0x5eed))
        {
            generated[coord] = among.Generate(coord).ComputeDigestHex();
        }

        foreach (var coord in coords)
        {
            var alone = new ProceduralChunkSource(TestGame.Data.WorldGen, TestGame.Data.Biomes, seed).Generate(coord);
            Assert.Equal(alone.ComputeDigestHex(), generated[coord]);
            Assert.Equal(coord, alone.Coord);
        }
    }

    [Theory]
    [MemberData(nameof(Places))]
    public void Given_ASourceWithAWiderWindow_When_TheSameChunksAreGenerated_Then_TheyAreUnchangedSoTheMarginIsEnough(ulong seed, Feature feature)
    {
        var (cx, cz) = ProceduralTestWorld.Find(seed, feature);
        var normal = ProceduralTestWorld.Source(seed);
        var wide = new ProceduralChunkSource(TestGame.Data.WorldGen, TestGame.Data.Biomes, seed, null, 6);

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
    public void Given_AColumnOfAnyPlace_When_ItsLayersAreRead_Then_TheBottomIsSolidStoneAndTheTopIsAir()
    {
        var source = ProceduralTestWorld.Source(7);
        foreach (var feature in new[] { Feature.Plain, Feature.Coast, Feature.Mountain })
        {
            var (cx, cz) = ProceduralTestWorld.Find(7, feature);
            var column = source.GenerateColumn(cx, cz);

            Assert.True(column[0].Blocks.ToArray().All(static b => b == BlockId.Stone), $"the lowest chunk of {feature} is stone to the floor at y -64");
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
