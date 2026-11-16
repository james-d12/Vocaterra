using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Generation.Procedural;
using Vocaterra.Game.Core.World.Streaming;
using Vocaterra.Game.Core.World.Underground;

namespace Vocaterra.Game.Core.Tests.World.Underground;

/// <summary>The ores (B11): each inside its rows' ranges with the shape of its distribution, veins inside their chunk and their band, and an ore never replacing air, water, lava, bedrock or another ore.</summary>
public sealed class OreTableTests
{
    private static readonly UndergroundData Data = TestGame.Data.Underground!;

    private static OreTable Table(ulong seed = 42) => new(Data.Ores, seed);

    private static Dictionary<int, int> CentreHeights(OreTable table, int row, int columns)
    {
        var counts = new Dictionary<int, int>();
        for (var cz = 0; cz < columns; cz++)
        {
            for (var cx = 0; cx < columns; cx++)
            {
                for (var cy = -4; cy < 20; cy++)
                {
                    foreach (var vein in table.Veins(cx, cy, cz).Where(v => v.Row == row))
                    {
                        var y = (cy << 4) + (vein.Cells[0] >> 8); // the first cell of a vein is its centre
                        counts[y] = counts.GetValueOrDefault(y) + 1;
                    }
                }
            }
        }

        return counts;
    }

    [Theory]
    [InlineData("copper-veins")]
    [InlineData("iron-veins")]
    [InlineData("tin-veins")]
    public void Given_AnOre_When_ManyChunksAreSampled_Then_EveryCellLiesInOneOfItsBands(string id)
    {
        var table = Table();
        var row = table.Rows.ToList().FindIndex(o => o.Id == id);
        var bands = table.Rows[row].Bands;
        var cells = 0;
        for (var cz = 0; cz < 12; cz++)
        {
            for (var cx = 0; cx < 12; cx++)
            {
                for (var cy = -4; cy < 20; cy++)
                {
                    foreach (var vein in table.Veins(cx, cy, cz).Where(v => v.Row == row))
                    {
                        foreach (var cell in vein.Cells)
                        {
                            var y = (cy << 4) + (cell >> 8);
                            cells++;
                            Assert.Contains(bands, b => y >= b.MinY && y <= b.MaxY);
                        }
                    }
                }
            }
        }

        Assert.True(cells > 1000);
    }

    [Fact]
    public void Given_CopperAndTin_When_TheCentresOfManyVeinsAreCounted_Then_TheSpreadIsTriangularWithItsPeak()
    {
        var table = Table();
        foreach (var (id, min, max, peak) in new[] { ("copper-veins", -16, 112, 48), ("tin-veins", -32, 48, 8) })
        {
            var row = table.Rows.ToList().FindIndex(o => o.Id == id);
            var counts = CentreHeights(table, row, 40);
            var total = counts.Values.Sum();
            Assert.True(total > 3000, $"{id}: {total} veins");
            Assert.InRange(counts.Keys.Min(), min, min + 12);
            Assert.InRange(counts.Keys.Max(), max - 12, max);
            var mean = counts.Sum(p => (double)p.Key * p.Value) / total;
            Assert.InRange(mean, ((min + max + peak) / 3.0) - 3, ((min + max + peak) / 3.0) + 3); // the mean of a triangular spread
            int Near(int y, int reach) => counts.Where(p => Math.Abs(p.Key - y) <= reach).Sum(p => p.Value);
            Assert.True(Near(peak, 8) > 2.5 * Near(min + 8, 8), $"{id}: more veins at the peak than at the foot");
            Assert.True(Near(peak, 8) > 2.5 * Near(max - 8, 8), $"{id}: more veins at the peak than at the top");
        }
    }

    [Fact]
    public void Given_Iron_When_TheCentresAreCounted_Then_ThereAreTwoBandsEachPeakingWhereTheDataSays()
    {
        var table = Table();
        var row = table.Rows.ToList().FindIndex(o => o.Id == "iron-veins");
        var counts = CentreHeights(table, row, 40);
        int Near(int y, int reach) => counts.Where(p => Math.Abs(p.Key - y) <= reach).Sum(p => p.Value);

        Assert.True(Near(16, 8) > 2.5 * Near(-20, 8));
        Assert.True(Near(160, 12) > 2.5 * Near(100, 12));
        Assert.True(Near(160, 12) > 2.5 * Near(300, 12));
        Assert.Equal(0, Near(68, 11)); // the gap between the bands (57 to 79)
        Assert.True(counts.Keys.Max() > 250);
        Assert.True(counts.Keys.Min() >= -24);
    }

    [Fact]
    public void Given_AChunk_When_ItsVeinsAreMade_Then_EveryCellIsInsideItSoNoVeinCrossesItsEdge()
    {
        var table = Table(2026);
        var veins = 0;
        for (var cz = -3; cz < 3; cz++)
        {
            for (var cx = -3; cx < 3; cx++)
            {
                for (var cy = -4; cy < 8; cy++)
                {
                    foreach (var vein in table.Veins(cx, cy, cz))
                    {
                        veins++;
                        Assert.All(vein.Cells, static c => Assert.InRange(c, 0, VoxelChunk.Volume - 1));
                        Assert.Equal(vein.Cells.Length, vein.Cells.Distinct().Count());
                        var spec = table.Rows[vein.Row];
                        Assert.InRange(vein.Cells.Length, 1, spec.SizeMax);
                    }
                }
            }
        }

        Assert.True(veins > 500);
    }

    [Fact]
    public void Given_AChunk_When_ItsVeinsAreMadeOnAnotherTableOfTheSameSeed_Then_TheyAreTheSame()
    {
        for (var cy = -4; cy < 8; cy++)
        {
            var (a, b) = (Table(7).Veins(5, cy, -9), Table(7).Veins(5, cy, -9));
            Assert.Equal(a.Count, b.Count);
            for (var i = 0; i < a.Count; i++)
            {
                Assert.Equal(a[i].Cells, b[i].Cells);
                Assert.Equal(a[i].Block, b[i].Block);
            }
        }
    }

    [Theory]
    [InlineData(42UL, Feature.Forest)]
    [InlineData(7UL, Feature.Mountain)]
    [InlineData(2026UL, Feature.Coast)]
    public void Given_APlace_When_ItsChunksAreMadeWithAndWithoutOres_Then_OnlyRockOfTheListIsReplacedByTheOresOfItsVeins(ulong seed, Feature feature)
    {
        var (cx, cz) = ProceduralTestWorld.Find(seed, feature);
        var with = ProceduralTestWorld.Source(seed);
        var without = new ProceduralChunkSource(TestGame.Data.WorldGen, TestGame.Data.Biomes, seed, null, Data with { Ores = [] });
        var table = new OreTable(Data.Ores, seed);
        var oreBlocks = Data.Ores.Select(static o => BlockDefinition.FromName(o.Block)!.Id).ToHashSet();
        var rocks = new HashSet<BlockId> { BlockId.Stone, BlockId.Slate, BlockId.Granite, BlockId.Limestone };
        var replaced = 0;
        for (var cy = with.MinChunkY; cy < (feature == Feature.Mountain ? 14 : 5); cy++)
        {
            var a = with.Generate(new VoxelChunkCoord(cx, cy, cz));
            var b = without.Generate(new VoxelChunkCoord(cx, cy, cz));
            var veinCells = table.Veins(cx, cy, cz).SelectMany(static v => v.Cells).ToHashSet();
            for (var i = 0; i < ChunkData.Volume; i++)
            {
                if (a.Blocks[i] == b.Blocks[i])
                {
                    continue;
                }

                replaced++;
                Assert.Contains(a.Blocks[i], oreBlocks);
                Assert.Contains(b.Blocks[i], rocks); // never air, water, lava, bedrock or an ore
                Assert.Contains(i, veinCells);
            }
        }

        Assert.True(replaced > 20, $"{replaced} blocks became ore");
    }

    [Fact]
    public void Given_ASeedAndAnother_When_TheirVeinsAreCompared_Then_TheyDiffer()
    {
        var (a, b) = (Table(42).Veins(3, 0, 3), Table(7).Veins(3, 0, 3));

        Assert.NotEqual(a.SelectMany(static v => v.Cells), b.SelectMany(static v => v.Cells));
    }

    [Fact]
    public void Given_ARowWithOneBandAndATallPeak_When_TheSpreadIsDrawn_Then_EveryDrawIsInTheRange()
    {
        var rng = new WorldRng(1);
        foreach (var band in new[] { new OreBand(0, 10, 0, 1), new OreBand(0, 10, 10, 1), new OreBand(-5, 5, 0, 1), new OreBand(100, 101, 100, 1) })
        {
            for (var i = 0; i < 2000; i++)
            {
                Assert.InRange(OreTable.CentreHeight(rng, band), band.MinY, band.MaxY);
            }
        }
    }
}
