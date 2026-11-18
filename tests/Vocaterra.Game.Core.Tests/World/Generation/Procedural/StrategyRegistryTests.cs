using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Tests.World.Biomes;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Biomes;
using Vocaterra.Game.Core.World.Climate;
using Vocaterra.Game.Core.World.Generation.Procedural;

namespace Vocaterra.Game.Core.Tests.World.Generation.Procedural;

/// <summary>
/// The strategy registry (B03, D-game-34 item 13): a biome row names code strategies for its own ground and plants, as it names tree species; the names are checked when the data is validated
/// and resolved when the world loads; a strategy fades out at its biome's border; and a biome with a strategy, a species and a crown that only a test defines generates with no change to the code.
/// </summary>
public sealed class StrategyRegistryTests
{
    private const string TestBiome = "test-dune-field";
    private const int Rise = 6;
    private const int Fade = 6;

    /// <summary>A surface strategy only the tests define: the ground rises by <see cref="Rise"/> blocks, fading in over <see cref="Fade"/> columns.</summary>
    private sealed class TestDunes : ISurfaceStrategy
    {
        public string Name => "test-dunes";

        public int FadeWidth => Fade;

        public int HeightDelta(in SurfaceColumn column) => Rise;
    }

    /// <summary>A decoration strategy only the tests define: tall grass on every column of sand.</summary>
    private sealed class TestTufts : IDecorationStrategy
    {
        public string Name => "test-tufts";

        public BlockId PlantAt(in DecorationColumn column) => column.Ground == BlockId.Sand ? BlockId.TallGrass : BlockId.Air;
    }

    private sealed class TestTuft : ICrownShape
    {
        public string Name => "test-tuft";

        public int Reach => 1;

        public int Rise => 2;

        public CrownProfile Profile(int trunkLength, ulong shape) => new(2, [0, 2]);
    }

    private static StrategyRegistry TestRegistry => StrategyRegistry.Default
        .WithSurface("test-dunes", static _ => new TestDunes())
        .WithDecoration("test-tufts", static _ => new TestTufts())
        .WithCrown(new TestTuft())
        .WithSpecies(new TreeSpecies("test-palm", "test-tuft", BlockId.Timber, BlockId.Canvas, 5, 5));

    // The shipped rows with one more only the tests define, tried before the plains: a dry, low land of sand with the strategies above and a thick stand of palms.
    private static BiomeData WithTestBiome(string surface = "test-dunes")
    {
        var shipped = TestGame.Data.Biomes;
        var row = BiomeTestData.Row(TestBiome, BiomeTestData.Where(humidity: new IntRange(int.MinValue, -300), height: new IntRange(34, 90)), top: "sand", filler: "sand") with
        {
            Trees = new TreeRule(100, [new TreeEntry("test-palm", 1, null)]),
            Decor = new DecorRule(1000, [new DecorEntry("tall-grass", 1)]),
            SurfaceStrategy = surface,
            DecorStrategy = "test-tufts",
        };
        var rows = shipped.Rows.ToList();
        rows.Insert(rows.FindIndex(static r => r.Id == "plains"), row);
        return new BiomeData(shipped.DitherTemperature, shipped.DitherHumidity, [.. rows.Select(static (r, i) => r with { Index = i })]);
    }

    // The chunk columns (in rings of 16 blocks outwards from the origin) with at least half of their sampled columns in the test biome, nearest first.
    private static IEnumerable<(int X, int Z)> TestBiomeChunks(BiomeData biomes, ulong seed)
    {
        var world = TestGame.Data.WorldGen;
        var climate = new ClimateSampler(world, seed);
        var model = new HeightModel(world, seed);
        var table = new BiomeTable(biomes, world, seed);
        for (var ring = 0; ring < 600; ring++)
        {
            for (var gz = -ring; gz <= ring; gz++)
            {
                for (var gx = -ring; gx <= ring; gx++)
                {
                    if (Math.Max(Math.Abs(gx), Math.Abs(gz)) != ring)
                    {
                        continue;
                    }

                    var inside = 0;
                    for (var i = 0; i < 16; i += 3)
                    {
                        for (var j = 0; j < 16; j += 3)
                        {
                            var (x, z) = ((gx * 16) + i, (gz * 16) + j);
                            var c = climate.At(x, z);
                            inside += table.Classify(c, model.Shape(x, z, c).Height, x, z, model.Shape(x, z, c).Water).Row.Id == TestBiome ? 1 : 0;
                        }
                    }

                    if (inside >= 18)
                    {
                        yield return (gx, gz);
                    }
                }
            }
        }
    }

    [Fact]
    public void Given_TheDefaultRegistry_When_ItsNamesAreListed_Then_ItHasThePlainSurfaceTheUndergrowthTheThreeSpeciesAndTheFourCrowns()
    {
        var registry = StrategyRegistry.Default;

        Assert.Equal(["plain"], registry.SurfaceNames);
        Assert.Equal(["undergrowth"], registry.DecorationNames);
        Assert.Equal(["birch", "oak", "spruce"], registry.SpeciesNames);
        Assert.Equal(["cone", "narrow", "round", "small-round"], registry.CrownNames);
    }

    [Fact]
    public void Given_ARegistry_When_AStrategyIsAdded_Then_ANewRegistryHasItAndTheOldOneDoesNot()
    {
        var registry = StrategyRegistry.Default;
        var more = registry.WithSurface("test-dunes", static _ => new TestDunes());

        Assert.Equal(["plain", "test-dunes"], more.SurfaceNames);
        Assert.Equal(["plain"], registry.SurfaceNames);
        Assert.Throws<ArgumentException>(() => more.WithSurface("test-dunes", static _ => new TestDunes()));
        Assert.Throws<ArgumentException>(() => registry.WithSpecies(new TreeSpecies("oak", "round", BlockId.Trunk, BlockId.Leaves, 4, 5)));
    }

    [Fact]
    public void Given_ShippedRows_When_TheyNameNoStrategy_Then_TheyUseThePlainSurfaceAndTheUndergrowth()
    {
        Assert.All(TestGame.Data.Biomes.Rows, row =>
        {
            Assert.Equal(StrategyRegistry.PlainSurface, row.SurfaceStrategy);
            Assert.Equal(StrategyRegistry.UndergrowthDecoration, row.DecorStrategy);
        });
    }

    [Fact]
    public void Given_ARowThatNamesStrategies_When_ItsJsonIsParsed_Then_TheNamesAreRead()
    {
        const string Json = """
            { "schemaVersion": 1, "dither": { "temperature": 10, "humidity": 10 }, "biomes": [
              { "id": "sandy", "top": "sand", "filler": "sand", "fillerDepth": 2, "surfaceStrategy": "dunes", "decorStrategy": "cacti" } ] }
            """;

        var row = BiomeData.Parse(Json).Rows[0];

        Assert.Equal("dunes", row.SurfaceStrategy);
        Assert.Equal("cacti", row.DecorStrategy);
    }

    [Fact]
    public void Given_ARowThatNamesAnUnknownStrategy_When_TheDataIsValidated_Then_TheValidatorNamesTheProblemAndTheKnownOnes()
    {
        var data = WithTestBiome();

        var withoutTheTestCode = BiomeValidator.Validate(data, TestGame.Data.WorldGen);
        var withTheTestCode = BiomeValidator.Validate(data, TestGame.Data.WorldGen, TestRegistry);

        Assert.Contains(withoutTheTestCode, static p => p.Contains("unknown surfaceStrategy 'test-dunes'", StringComparison.Ordinal) && p.Contains("plain", StringComparison.Ordinal));
        Assert.Contains(withoutTheTestCode, static p => p.Contains("unknown decorStrategy 'test-tufts'", StringComparison.Ordinal));
        Assert.Contains(withoutTheTestCode, static p => p.Contains("unknown tree species 'test-palm'", StringComparison.Ordinal));
        Assert.Empty(withTheTestCode);
    }

    [Fact]
    public void Given_ARowThatNamesAnUnknownStrategy_When_ASourceIsMade_Then_TheNameIsRefusedAtLoadWithTheKnownOnes()
    {
        var ex = Assert.Throws<ArgumentException>(() => new ProceduralChunkSource(TestGame.Data.WorldGen, WithTestBiome(), 42));

        Assert.Contains("test-dunes", ex.Message, StringComparison.Ordinal);
        Assert.Contains("plain", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait(TestSpeed.Trait, TestSpeed.Slow)]
    public void Given_ABiomeAStrategyASpeciesAndACrownOnlyTheTestsDefine_When_ItsLandIsGenerated_Then_ItAppearsWithNoChangeToTheCode()
    {
        const ulong Seed = 42;
        var data = WithTestBiome();
        var source = new ProceduralChunkSource(TestGame.Data.WorldGen, data, Seed, TestRegistry);
        var (cx, cz) = TestBiomeChunks(data, Seed).First();

        var region = new GeneratedRegion(source, cx - 1, cz - 1, 3, 3);
        var counts = new Dictionary<BlockId, int>();
        foreach (var chunk in region.Chunks)
        {
            foreach (var block in chunk.Blocks)
            {
                counts[block] = counts.GetValueOrDefault(block) + 1;
            }
        }

        Assert.True(counts.GetValueOrDefault(BlockId.Timber) > 4, "the test species' trunks stand in the test biome");
        Assert.True(counts.GetValueOrDefault(BlockId.Canvas) > 8, "and carry the test crown's leaves");
        Assert.True(counts.GetValueOrDefault(BlockId.TallGrass) > 20, "and the test decoration strategy's plants grow on its sand");
        Assert.True(counts.GetValueOrDefault(BlockId.Sand) > 100, "on the sand the row names");
    }

    // What a 3 x 3 block of chunk columns shows of the strategy: the columns raised by the full rise, the pairs of neighbours across the biome's border and the steepest step across it, and the steepest
    // step of the rise itself between any two neighbouring columns.
    private static (int Raised, int Deepest, int Border, int Steepest, int PlainSteepest, int Lowest, int LiftStep) Measure(ProceduralChunkSource withDunes, ProceduralChunkSource plain, int cx, int cz)
    {
        var sea = TestGame.Data.WorldGen.Vertical.SeaLevel;
        var (raised, deepest, border, steepest, plainSteepest, lowest, liftStep) = (0, 0, 0, 0, 0, 0, 0);
        for (var dz = -1; dz <= 1; dz++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                var shaped = withDunes.ShapedColumn(cx + dx, cz + dz);
                var flat = plain.ShapedColumn(cx + dx, cz + dz);
                for (var z = (cz + dz) * 16; z < ((cz + dz) * 16) + 16; z++)
                {
                    for (var x = (cx + dx) * 16; x < ((cx + dx) * 16) + 16; x++)
                    {
                        if (flat.HeightAt(x, z) >= sea + 3 && shaped.HeightAt(x, z) >= sea + 3)
                        {
                            var lift = shaped.HeightAt(x, z) - flat.HeightAt(x, z);
                            lowest = Math.Min(lowest, lift);
                            deepest = Math.Max(deepest, lift);
                            raised += lift == Rise ? 1 : 0;
                        }

                        foreach (var (ox, oz) in new[] { (1, 0), (0, 1) })
                        {
                            if (flat.HeightAt(x, z) >= sea + 3 && flat.HeightAt(x + ox, z + oz) >= sea + 3 && shaped.HeightAt(x, z) >= sea + 3 && shaped.HeightAt(x + ox, z + oz) >= sea + 3)
                            {
                                liftStep = Math.Max(liftStep, Math.Abs((shaped.HeightAt(x, z) - flat.HeightAt(x, z)) - (shaped.HeightAt(x + ox, z + oz) - flat.HeightAt(x + ox, z + oz))));
                            }

                            if (shaped.BiomeAt(x, z).Row.Index != shaped.BiomeAt(x + ox, z + oz).Row.Index)
                            {
                                border++;
                                steepest = Math.Max(steepest, Math.Abs(shaped.HeightAt(x, z) - shaped.HeightAt(x + ox, z + oz)));
                                plainSteepest = Math.Max(plainSteepest, Math.Abs(flat.HeightAt(x, z) - flat.HeightAt(x + ox, z + oz)));
                            }
                        }
                    }
                }
            }
        }

        return (raised, deepest, border, steepest, plainSteepest, lowest, liftStep);
    }

    [Fact]
    [Trait(TestSpeed.Trait, TestSpeed.Slow)]
    public void Given_ATestStrategyThatRaisesItsBiome_When_TheBiomeIsGenerated_Then_ItsHeartStandsItsRiseHigherAndTheBorderFadesWithNoCliff()
    {
        const ulong Seed = 42;
        var withDunes = new ProceduralChunkSource(TestGame.Data.WorldGen, WithTestBiome(), Seed, TestRegistry);
        var plain = new ProceduralChunkSource(TestGame.Data.WorldGen, WithTestBiome("plain"), Seed, TestRegistry);

        // The first place where the test biome has a heart (columns more than a fade width from any other biome) and a border within three chunk columns.
        var found = TestBiomeChunks(WithTestBiome(), Seed).Take(300).Select(c => (Place: c, Seen: Measure(withDunes, plain, c.X, c.Z))).FirstOrDefault(static r => r.Seen.Raised > 10 && r.Seen.Border > 10);

        Assert.NotEqual(default, found.Place);
        var seen = found.Seen;
        Assert.Equal(Rise, seen.Deepest);
        Assert.True(seen.Lowest >= 0, "the strategy never lowers the ground here");
        Assert.True(seen.LiftStep <= 1, $"the rise arrives by steps of at most 1 block between neighbouring columns (it steps {seen.LiftStep})");
        Assert.True(seen.Steepest <= seen.PlainSteepest + 1 + (Rise / (Fade + 1)), $"the border steps {seen.Steepest} blocks with the strategy and {seen.PlainSteepest} without");
    }
}
