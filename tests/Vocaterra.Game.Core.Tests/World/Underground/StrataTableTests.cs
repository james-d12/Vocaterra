using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Underground;

namespace Vocaterra.Game.Core.Tests.World.Underground;

/// <summary>
/// The rock layers (B11, B11b): stone above the dither, slate below it, a ragged change between, pockets of gravel, sand, dirt, granite and limestone in the stone and of granite and basalt in the slate, and a
/// chunk's rocks equal the block-by-block ones.
/// </summary>
public sealed class StrataTableTests
{
    private static StrataTable Table(ulong seed = 42) => new(TestGame.Data.Underground!.Strata, seed);

    private static readonly StrataSpec Spec = TestGame.Data.Underground!.Strata;

    [Fact]
    public void Given_TheStrata_When_RocksAreReadAboveTheDither_Then_OnlyStoneAndItsPocketsAppear()
    {
        var table = Table();
        var seen = new HashSet<BlockId>();
        for (var y = Spec.DitherToY; y < 320; y += 3)
        {
            for (var z = -40; z < 40; z += 3)
            {
                for (var x = -40; x < 40; x += 3)
                {
                    seen.Add(table.RockAt(x, y, z));
                }
            }
        }

        Assert.Equal(new[] { BlockId.Stone, BlockId.Sand, BlockId.Dirt, BlockId.Granite, BlockId.Limestone, BlockId.Gravel }.Order(), seen.Order());
    }

    [Fact]
    public void Given_TheStrata_When_RocksAreReadBelowTheDither_Then_ItIsSlateWithPocketsOfBasaltAndGranite()
    {
        var table = Table();
        var counts = new Dictionary<BlockId, int>();
        for (var y = -64; y < Spec.DitherFromY; y += 2)
        {
            for (var z = -120; z < 120; z += 3)
            {
                for (var x = -120; x < 120; x += 3)
                {
                    var rock = table.RockAt(x, y, z);
                    counts[rock] = counts.GetValueOrDefault(rock) + 1;
                }
            }
        }

        Assert.Equal(new[] { BlockId.Slate, BlockId.Granite, BlockId.Basalt }.Order(), counts.Keys.Order());
        var total = counts.Values.Sum();
        Assert.InRange(counts[BlockId.Basalt] * 100.0 / total, 3, 30);
        Assert.InRange(counts[BlockId.Granite] * 100.0 / total, 1, 20);
        Assert.True(counts[BlockId.Slate] * 100.0 / total > 45, "slate is still the deep rock");
    }

    [Fact]
    public void Given_TheDitherBand_When_ItsRocksAreCounted_Then_TheShareOfSlateFallsInAStraightLineAndTheBoundaryIsRagged()
    {
        var table = Table();
        var span = Spec.DitherToY - Spec.DitherFromY;
        var slate = new int[span];
        const int N = 100;
        for (var y = Spec.DitherFromY; y < Spec.DitherToY; y++)
        {
            for (var z = 0; z < N; z++)
            {
                for (var x = 0; x < N; x++)
                {
                    var rock = table.RockAt(x, y, z);
                    Assert.Contains(rock, new[] { BlockId.Slate, BlockId.Stone, BlockId.Granite, BlockId.Limestone, BlockId.Basalt, BlockId.Gravel, BlockId.Sand, BlockId.Dirt }); // the pockets lie in the rock of the band as well
                    slate[y - Spec.DitherFromY] += rock == BlockId.Slate ? 1 : 0;
                }
            }
        }

        for (var i = 0; i < span; i++)
        {
            var expected = (span - i) * N * N / span; // the whole of the lowest row, none of the row at ditherToY
            Assert.InRange(slate[i], expected - 400 - (N * N / 3), expected + 400); // basalt and granite pockets take a good part of the slate
        }

        // Ragged: within one column slate and stone alternate in the band, so the boundary is not a plane.
        var mixed = 0;
        for (var z = 0; z < 30; z++)
        {
            for (var x = 0; x < 30; x++)
            {
                var flips = 0;
                for (var y = Spec.DitherFromY; y < Spec.DitherToY - 1; y++)
                {
                    flips += (table.RockAt(x, y, z) == BlockId.Slate) != (table.RockAt(x, y + 1, z) == BlockId.Slate) ? 1 : 0;
                }

                mixed += flips > 0 ? 1 : 0;
            }
        }

        Assert.True(mixed > 800, $"{mixed} of 900 columns change rock in the band");
    }

    [Fact]
    public void Given_TheBlobs_When_TheStoneLayerIsSampled_Then_EveryPocketFormsLargePatchesOfASensibleShare()
    {
        var table = Table();
        var counts = new Dictionary<BlockId, int>();
        long total = 0;
        for (var y = 16; y < 200; y += 4)
        {
            for (var z = -200; z < 200; z += 4)
            {
                for (var x = -200; x < 200; x += 4)
                {
                    var rock = table.RockAt(x, y, z);
                    counts[rock] = counts.GetValueOrDefault(rock) + 1;
                    total++;
                }
            }
        }

        foreach (var blob in new[] { BlockId.Granite, BlockId.Limestone, BlockId.Gravel, BlockId.Sand, BlockId.Dirt })
        {
            Assert.InRange(counts[blob] * 100.0 / total, 2, 25);
        }

        Assert.InRange(counts[BlockId.Stone] * 100.0 / total, 35, 75); // the stone no longer fills the layer

        // Blobs are big: the rock at a block and at the next one across agrees nearly always.
        var agree = 0;
        var tested = 0;
        for (var z = -100; z < 100; z += 5)
        {
            for (var x = -100; x < 100; x += 5)
            {
                tested++;
                agree += table.RockAt(x, 60, z) == table.RockAt(x + 1, 60, z) ? 1 : 0;
            }
        }

        Assert.True(agree * 100 / tested >= 88, $"{agree} of {tested}");
    }

    [Theory]
    [InlineData(42UL, 0, 0, 0)]
    [InlineData(42UL, -3, -1, 5)]
    [InlineData(7UL, 4, 0, -2)]
    [InlineData(2026UL, 2, 1, 2)]
    [InlineData(2026UL, 9, 6, -11)]
    public void Given_AChunk_When_ItsRocksAreFilledAtOnce_Then_TheyEqualTheBlockByBlockRocks(ulong seed, int cx, int cy, int cz)
    {
        var table = Table(seed);
        var rocks = new BlockId[VoxelChunk.Volume];

        table.Fill(cx << 4, cy << 4, cz << 4, rocks);

        var index = 0;
        for (var ly = 0; ly < 16; ly++)
        {
            for (var lz = 0; lz < 16; lz++)
            {
                for (var lx = 0; lx < 16; lx++, index++)
                {
                    Assert.Equal(table.RockAt((cx << 4) + lx, (cy << 4) + ly, (cz << 4) + lz), rocks[index]);
                }
            }
        }
    }

    [Fact]
    public void Given_ASeedAndAnother_When_TheBlobsAreCompared_Then_TheySitElsewhere()
    {
        var (a, b) = (Table(42), Table(7));
        var differ = 0;
        for (var x = 0; x < 200; x += 2)
        {
            differ += a.RockAt(x, 60, 10) != b.RockAt(x, 60, 10) ? 1 : 0;
        }

        Assert.True(differ > 5);
    }
}
