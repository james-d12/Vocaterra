using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.Graphics.World;

namespace Vocaterra.Game.Tests.Graphics.World;

/// <summary>The decorative grass layer (B18): where a tuft stands, that it stands still, what carries none, and how the slider's share, the biome's weight and the quality's cap fall out of one hash.</summary>
public sealed class GrassMesherTests
{
    private static readonly GrassBiomes Data = GrassBiomes.Shipped;

    private static MapBlocks Meadow(BlockId top = BlockId.Grass)
    {
        var map = new MapBlocks();
        map.Fill(0, 0, 0, 15, 4, 15, BlockId.Dirt);
        map.Fill(0, 5, 0, 15, 5, 15, top);
        return map;
    }

    private static GrassLayerOptions Options(float cap = 1f, Func<int, int, string?>? biomeAt = null) => new(cap, Data, biomeAt, null, LightBand.Day, false);

    private static GrassMesh Layer(IBlockSource map, float cap = 1f, Func<int, int, string?>? biomeAt = null) =>
        GrassMesher.Build(map, 0, 0, 0, Options(cap, biomeAt)) ?? throw new InvalidOperationException("no grass");

    private static List<(float X, float Y, float Z)> Positions(GrassMesh layer) => layer.Mesh.Vertices.ToArray().Select(static v => (v.X, v.Y, v.Z)).ToList();

    [Fact]
    public void Given_AGrassField_When_TheLayerIsBuiltTwice_Then_EveryTuftStandsInTheSamePlace()
    {
        var first = Layer(Meadow());
        var second = Layer(Meadow());
        Assert.Equal(Positions(first), Positions(second));
        Assert.Equal(first.QuadrantStart, second.QuadrantStart);
        Assert.Equal(first.Cumulative, second.Cumulative);
        Assert.Equal(512, first.Quads); // the unplaced weight is 1: every one of the 256 columns holds a tuft of two quads
    }

    [Fact]
    public void Given_ACell_When_ItsHashIsAsked_Then_ItIsAFixedNumberOfThePositionAlone()
    {
        // Pinned: a tuft's place, height and tint come from this number, so a change of it moves every tuft in every world (and is a decision, not a refactor).
        Assert.Equal(GrassMesher.CellHash(1, 2, 3), GrassMesher.CellHash(1, 2, 3));
        Assert.NotEqual(GrassMesher.CellHash(1, 2, 3), GrassMesher.CellHash(3, 2, 1));
        Assert.Equal(GoldenHash, GrassMesher.CellHash(1, 2, 3));
        Assert.InRange(GrassMesher.RankOf(GrassMesher.CellHash(-40, 7, 912)), 0f, 0.99999f);
    }

    private const uint GoldenHash = 1850047449u;

    [Fact]
    public void Given_ATuftInAChunkAndTheSameCellInAnother_When_Built_Then_ItIsTheSameTuftWhateverTheChunkIsBuiltFirst()
    {
        var map = new MapBlocks();
        map.Fill(0, 0, 0, 31, 4, 15, BlockId.Dirt);
        map.Fill(0, 5, 0, 31, 5, 15, BlockId.Grass);
        var left = GrassMesher.Build(map, 0, 0, 0, Options())!;
        var right = GrassMesher.Build(map, 1, 0, 0, Options())!;
        Assert.All(Positions(left), static p => Assert.InRange(p.X, 0f, 16f));
        Assert.All(Positions(right), static p => Assert.InRange(p.X, 16f, 32f)); // each chunk holds its own columns' tufts only
        Assert.Equal(left.Quads, right.Quads);
        // Rebuilt in the other order, the right chunk's tufts are exactly as before.
        Assert.Equal(Positions(right), Positions(GrassMesher.Build(map, 1, 0, 0, Options())!));
    }

    [Theory]
    [InlineData(BlockId.Sand)]
    [InlineData(BlockId.Path)]
    [InlineData(BlockId.Farmland)]
    [InlineData(BlockId.Snow)]
    [InlineData(BlockId.Stone)]
    [InlineData(BlockId.Gravel)]
    [InlineData(BlockId.Dirt)]
    [InlineData(BlockId.Planks)]
    public void Given_ATopThatIsNotGrass_When_TheLayerIsBuilt_Then_ThereIsNone(BlockId top)
    {
        Assert.Null(GrassMesher.Build(Meadow(top), 0, 0, 0, Options()));
    }

    [Theory]
    [InlineData(BlockId.TallGrass)]
    [InlineData(BlockId.Fern)]
    [InlineData(BlockId.YellowFlower)]
    [InlineData(BlockId.Wheat)]
    [InlineData(BlockId.Snow)]
    [InlineData(BlockId.Door)]
    [InlineData(BlockId.Bush)]
    [InlineData(BlockId.Leaves)]
    [InlineData(BlockId.Water)]
    [InlineData(BlockId.Torch)]
    [InlineData(BlockId.Planks)]
    public void Given_AnythingOverTheGrass_When_TheLayerIsBuilt_Then_NoTuftStandsThere(BlockId above)
    {
        var map = Meadow();
        map.Fill(0, 6, 0, 15, 6, 15, above);
        Assert.Null(GrassMesher.Build(map, 0, 0, 0, Options()));
        // One column left open: exactly its tuft.
        map.Set(7, 6, 7, BlockId.Air);
        var layer = GrassMesher.Build(map, 0, 0, 0, Options())!;
        Assert.Equal(2, layer.Quads);
        Assert.All(Positions(layer), static p => Assert.InRange(p.X, 7f, 8f));
    }

    [Fact]
    public void Given_APathAndFarmlandBesideGrass_When_TheLayerIsBuilt_Then_NoTuftReachesOverThem()
    {
        var map = Meadow();
        map.Fill(0, 5, 6, 15, 5, 9, BlockId.Path);
        map.Fill(10, 5, 0, 12, 5, 15, BlockId.Farmland);
        var layer = Layer(map);
        foreach (var (x, _, z) in Positions(layer))
        {
            var blockX = (int)MathF.Floor(x);
            var blockZ = (int)MathF.Floor(z);
            Assert.Equal(BlockId.Grass, map.Get(blockX, 5, blockZ)); // every vertex is over a grass cell: a tuft stays inside its own cell
        }
    }

    [Fact]
    public void Given_ATuft_When_ItsQuadsAreRead_Then_ItStandsInItsOwnCellOnTheTopOfTheBlockWithinTheThreeHeights()
    {
        var map = new MapBlocks().Fill(0, 0, 0, 15, 5, 15, BlockId.Dirt).Set(4, 5, 9, BlockId.Grass);
        var layer = Layer(map);
        Assert.Equal(2, layer.Quads);
        foreach (var vertex in layer.Mesh.Vertices)
        {
            Assert.InRange(vertex.X, 4f, 5f);
            Assert.InRange(vertex.Z, 9f, 10f);
            Assert.InRange(vertex.Y, 5.9f, 6.9f); // from a hair into the top (the grass block's top is at y 6) to the tallest tuft
        }
    }

    [Fact]
    public void Given_ManyTufts_When_TheirHeightsAreRead_Then_TheyComeInThreeFamiliesAndTheTintsVary()
    {
        var layer = Layer(Meadow());
        var vertices = layer.Mesh.Vertices.ToArray();
        var heights = new HashSet<int>();
        var tips = new HashSet<uint>();
        for (var i = 0; i < vertices.Length; i += 4)
        {
            heights.Add((int)MathF.Floor((vertices[i + 2].Y - vertices[i].Y) / 0.13f));
            tips.Add(vertices[i + 2].Color);
        }

        Assert.True(heights.Count >= 3, "heights vary");
        Assert.True(tips.Count >= 3, "tints vary");
    }

    [Fact]
    public void Given_NoCap_When_TheLayerIsBuilt_Then_ThereIsNone()
    {
        Assert.Null(GrassMesher.Build(Meadow(), 0, 0, 0, Options(0f)));
    }

    [Fact]
    public void Given_TheChunkMesher_When_AGrassFieldIsMeshed_Then_TheGrassIsNeverInItsMeshes()
    {
        // The layer is a pass of its own: with the slider at 0 (or never asked) the chunk's meshes are what they were, and they hold no plant quad for the grass blocks.
        var meshes = ChunkMesher.Build(Meadow(), 0, 0, 0);
        Assert.Equal(0, meshes.PlantMesh.FaceCount);
        Assert.Equal(ChunkMesher.Build(Meadow(), 0, 0, 0).FaceCount, meshes.FaceCount);
    }

    [Fact]
    public void Given_TheBiomes_When_TheirWeightsAreRead_Then_PlainsAreLushAndTheTaigaAndOtherLandAreSparserAndTheShippedDataIsSane()
    {
        Assert.Equal(1f, Data.WeightOf("plains"));
        Assert.Equal(1f, Data.WeightOf("meadow"));
        Assert.True(Data.WeightOf("woodland") > Data.WeightOf("taiga"));
        Assert.True(Data.WeightOf("taiga") > 0f);
        Assert.True(Data.WeightOf("some-dry-biome") < Data.WeightOf("woodland"));
        Assert.Equal(1f, Data.WeightOf(null)); // the valley: no place query, all meadow
        Assert.True(Data.IsTop(BlockId.Grass));
        foreach (var kind in new[] { BlockId.Sand, BlockId.Snow, BlockId.Gravel, BlockId.Stone, BlockId.Path, BlockId.Farmland, BlockId.Dirt })
        {
            Assert.False(Data.IsTop(kind), kind.ToString());
        }
    }

    [Fact]
    public void Given_ABiomeData_When_ParsedWithWildWeights_Then_TheyAreLimitedToZeroToOne()
    {
        var data = GrassBiomes.Parse("""{"tops":["grass","Snow"],"weights":{"a":4.0,"b":-2},"other":9,"unplaced":0.5}""");
        Assert.Equal(1f, data.WeightOf("a"));
        Assert.Equal(0f, data.WeightOf("b"));
        Assert.Equal(1f, data.WeightOf("c"));
        Assert.Equal(0.5f, data.WeightOf(null));
        Assert.True(data.IsTop(BlockId.Snow));
    }

    [Fact]
    public void Given_ABiomePerColumn_When_TheLayerIsBuilt_Then_EachBiomeHasItsOwnShareAndNoWeightMeansNoTuft()
    {
        var map = Meadow();
        string? Biome(int x, int z) => x < 8 ? "plains" : z < 8 ? "taiga" : "desert";
        var layer = Layer(map, 1f, Biome);
        var plains = layer.QuadsIn(0, GrassMesher.Tiers) + layer.QuadsIn(2, GrassMesher.Tiers);
        var taiga = layer.QuadsIn(1, GrassMesher.Tiers);
        var other = layer.QuadsIn(3, GrassMesher.Tiers);
        Assert.Equal(256, plains); // 128 columns, all with a tuft
        Assert.InRange(taiga, 20, 90); // 64 columns at 0.4
        Assert.InRange(other, 8, 70); // 64 columns at 0.3
        Assert.True(plains / 2 > taiga / 2 && taiga > other);
        var none = GrassMesher.Build(map, 0, 0, 0, Options(1f, static (_, _) => "lava-flats")) ?? throw new InvalidOperationException();
        Assert.True(none.Quads < 512);
        Assert.Null(GrassMesher.Build(map, 0, 0, 0, new GrassLayerOptions(1f, GrassBiomes.Parse("""{"tops":["grass"],"weights":{},"other":0,"unplaced":1}"""), static (_, _) => "x", null, LightBand.Day, false)));
    }

    [Fact]
    public void Given_ASliderShare_When_TheTiersAreCounted_Then_ThePrefixIsExactlyTheTuftsWhoseRankIsBelowTheShare()
    {
        var map = Meadow();
        var layer = Layer(map);
        for (var tiers = 0; tiers <= GrassMesher.Tiers; tiers += 2) // the slider moves in tens: ten percent is two tiers
        {
            var share = tiers / (float)GrassMesher.Tiers;
            var expected = 0;
            for (var x = 0; x < 16; x++)
            {
                for (var z = 0; z < 16; z++)
                {
                    if (GrassMesher.RankOf(GrassMesher.CellHash(x, 5, z)) < share)
                    {
                        expected += 2;
                    }
                }
            }

            var got = Enumerable.Range(0, GrassMesher.Quadrants).Sum(q => layer.QuadsIn(q, tiers));
            Assert.Equal(expected, got);
        }
    }

    [Fact]
    public void Given_ALowerCap_When_TheLayerIsBuilt_Then_ItsTuftsAreAmongThoseOfTheHigherCap()
    {
        var map = Meadow();
        var low = Positions(Layer(map, 0.4f)).ToHashSet();
        var high = Positions(Layer(map, 1f)).ToHashSet();
        Assert.True(low.Count < high.Count);
        Assert.True(low.IsSubsetOf(high), "raising the cap only adds tufts; none moves");
        var tenth = Positions(Layer(map, 0.1f)).ToHashSet();
        Assert.True(tenth.IsSubsetOf(low));
    }

    [Fact]
    public void Given_TheQualityPresets_When_TheirGrassIsRead_Then_LowIsCheaperThanMediumThanHigh()
    {
        var low = QualityPreset.For(Vocaterra.Game.Core.Menu.QualityLevel.Low);
        var medium = QualityPreset.For(Vocaterra.Game.Core.Menu.QualityLevel.Medium);
        var high = QualityPreset.For(Vocaterra.Game.Core.Menu.QualityLevel.High);
        Assert.True(low.GrassDistance < medium.GrassDistance && medium.GrassDistance < high.GrassDistance);
        Assert.True(low.GrassCapPercent < medium.GrassCapPercent && medium.GrassCapPercent < high.GrassCapPercent);
        Assert.Equal(100, high.GrassCapPercent);
    }

    [Fact]
    public void Given_GreyTufts_When_TheLayerIsBuilt_Then_TheyReadFromTheGreyCopyOfTheAtlas()
    {
        var map = Meadow();
        var colour = GrassMesher.Build(map, 0, 0, 0, Options())!;
        var grey = GrassMesher.Build(map, 0, 0, 0, Options() with { Grey = true })!;
        Assert.NotEqual(colour.Mesh.Vertices[0].V, grey.Mesh.Vertices[0].V);
    }

    [Fact]
    public void Given_ADuskBandAndACaveRoof_When_TheLayerIsBuilt_Then_TheTuftsAreDarkerAsTheFacesAre()
    {
        var map = Meadow();
        var day = GrassMesher.Build(map, 0, 0, 0, Options())!.Mesh.Vertices[2].Color;
        var night = GrassMesher.Build(map, 0, 0, 0, Options() with { Band = LightBand.Night })!.Mesh.Vertices[2].Color;
        Assert.True((day & 0xFF) > (night & 0xFF), "the band's sun is in the colours");
        var buried = new SkySurface(0, 0, static (_, _) => 20);
        var dark = GrassMesher.Build(map, 0, 0, 0, Options() with { Sky = buried })!.Mesh.Vertices[2].Color;
        Assert.True((day & 0xFF) > (dark & 0xFF), "a tuft under the ground's roof is darkened");
    }
}
