using Vocaterra.Game.Core.Tests.World.Regions;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Streaming;

namespace Vocaterra.Game.Core.Tests.World.Streaming;

/// <summary>
/// The surface band (B07, biomes plan section 3.7): which chunk layers of a column meet the ground, decided from the height model with no chunk generated. A layer above it must be all air, whatever
/// generating the column says; that is what lets a distant column skip it. A layer under it is the underground (B11: strata, ores, caves), which the residency holds only near the player.
/// </summary>
public sealed class SurfaceBandsTests
{
    private static StreamedWorld World => RegionTestWorld.Composition().World;

    // Chunk columns of every kind of land: the valley (region columns), beside it, a corner, and the lowest, the highest and some middling columns of a scan of the world round it.
    private static IReadOnlyList<(int X, int Z)> Sample()
    {
        var far = World.Far;
        var scan = new List<(int X, int Z, int Y)>();
        for (var cz = -90; cz <= 90; cz += 6)
        {
            for (var cx = -90; cx <= 90; cx += 6)
            {
                scan.Add((cx, cz, far.At((cx * 16) + 8, (cz * 16) + 8).SurfaceY));
            }
        }

        scan.Sort(static (a, b) => a.Y.CompareTo(b.Y));
        var columns = new List<(int X, int Z)> { (5, 5), (12, 12), (23, 3), (0, 0), (24, 10), (25, 13), (-1, 12), (12, 24), (30, 13), (-3, -3) };
        columns.AddRange(new[] { scan[0], scan[scan.Count / 8], scan[scan.Count / 4], scan[scan.Count / 2], scan[(scan.Count * 3) / 4], scan[(scan.Count * 7) / 8], scan[^1] }.Select(static t => (t.X, t.Z)));
        return columns;
    }

    [Fact]
    public void Given_SampleColumnsOfEveryKindOfLand_When_TheirLayersAboveTheBandAreRead_Then_EachIsAllAir()
    {
        var checkedLayers = 0;
        foreach (var (cx, cz) in Sample())
        {
            var band = World.Bands.Of(cx, cz);
            var column = World.Source.GenerateColumn(cx, cz);
            for (var i = 0; i < column.Count; i++)
            {
                var layer = World.Source.MinChunkY + i;
                if (layer <= band.MaxLayer)
                {
                    continue;
                }

                var chunk = column[i];
                Assert.True(chunk.IsEmpty, $"layer {layer} of column ({cx}, {cz}) lies above the band {band} yet holds blocks");
                checkedLayers++;
            }
        }

        Assert.True(checkedLayers > 100);
    }

    [Fact]
    public void Given_SampleColumns_When_TheBandIsAsked_Then_ItHoldsAtLeastOneLayerThatMeetsTheGroundAndNeverMoreThanTheWorld()
    {
        var counts = new List<int>();
        foreach (var (cx, cz) in Sample())
        {
            var band = World.Bands.Of(cx, cz);
            Assert.False(band.IsEmpty, $"column ({cx}, {cz})");
            Assert.InRange(band.MinLayer, World.Bands.WorldMinLayer, World.Bands.WorldMaxLayer);
            Assert.InRange(band.MaxLayer, World.Bands.WorldMinLayer, World.Bands.WorldMaxLayer);
            var column = World.Source.GenerateColumn(cx, cz);
            Assert.Contains(Enumerable.Range(band.MinLayer, band.Count), layer => !column[layer - World.Source.MinChunkY].IsEmpty);
            counts.Add(band.Count);
        }

        Assert.True(counts.Average() < 8, $"a band holds {counts.Average():0.0} layers on average, against 24 for a whole column");
    }

    [Fact]
    public void Given_TheSameColumnAskedAgainAndFromManyThreads_When_ItsBandIsRead_Then_ItIsAlwaysTheSame()
    {
        var columns = Sample().Take(8).ToList();
        var expected = columns.Select(c => World.Bands.Of(c.X, c.Z)).ToList();

        var results = new ColumnBand[8][];
        Parallel.For(0, 8, new ParallelOptions { MaxDegreeOfParallelism = 4 }, t => results[t] = [.. columns.Select(c => World.Bands.Of(c.X, c.Z))]);

        Assert.All(results, r => Assert.Equal(expected, r));
    }

    [Fact]
    public void Given_AColumnInsideTheValley_When_ItsBandIsAsked_Then_ItIsTheRegionsLayersThatHoldSomethingAndAreNotAllFiller()
    {
        var band = World.Bands.Of(12, 12);
        var region = RegionTestWorld.Valley();

        bool MeetsGround(int cy)
        {
            var data = region.Copy(new VoxelChunkCoord(12, cy, 12));
            return !data.IsEmpty && !SurfaceBands.IsUniformSolid(data);
        }

        for (var cy = 0; cy < 8; cy++)
        {
            Assert.True(!MeetsGround(cy) || band.Contains(cy), $"layer {cy} meets the ground and is outside the band {band}");
        }

        Assert.True(MeetsGround(band.MinLayer));
        Assert.True(MeetsGround(band.MaxLayer));
        Assert.InRange(band.Count, 1, 8);
    }
}
