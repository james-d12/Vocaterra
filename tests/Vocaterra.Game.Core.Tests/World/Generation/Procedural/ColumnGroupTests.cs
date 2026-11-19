using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Tests.World.Regions;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.World.Generation.Procedural;

/// <summary>
/// The shaped window shared by a group of chunk columns (B39): a chunk made through a group's window equals the chunk made alone, byte for byte, and so does the column's surface band, for columns at the corners, on the
/// edges and inside of a group of 4 x 4 chunk columns (and of a group that is not aligned, as the ring's are), on seeds 42 and 2026 and over a forest, a river, a coast, a mountain. It rests on the same invariant as the
/// margin test: a window wider than the margin changes no chunk.
/// </summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class ColumnGroupTests
{
    private const int Span = 4;

    public static TheoryData<ulong, Feature> Places => new()
    {
        { 42, Feature.Forest },
        { 2026, Feature.River },
        { 42, Feature.Coast },
        { 2026, Feature.Mountain },
        { 42, Feature.Mountain },
    };

    // The corners, the middle of each edge, two inside columns and the column the feature is in.
    private static IEnumerable<(int X, int Z)> Sample(int minX, int minZ, int cx, int cz)
    {
        var last = Span - 1;
        var offsets = new (int X, int Z)[] { (0, 0), (last, 0), (0, last), (last, last), (1, 0), (0, 2), (last, 1), (2, last), (1, 1), (2, 2) };
        return offsets.Select(o => (minX + o.X, minZ + o.Z)).Append((cx, cz)).Distinct();
    }

    private static int Floor4(int v) => (v >> 2) << 2;

    [Theory]
    [MemberData(nameof(Places))]
    public void Given_AGroupOfFourByFourColumns_When_AColumnIsMadeFromItsWindow_Then_EveryLayerAndTheBandEqualTheColumnMadeAlone(ulong seed, Feature feature)
    {
        var (cx, cz) = ProceduralTestWorld.Find(seed, feature);
        var source = ProceduralTestWorld.Source(seed);
        var (minX, minZ) = (Floor4(cx), Floor4(cz));
        var group = source.ShapeGroup(minX, minZ, Span);

        foreach (var (x, z) in Sample(minX, minZ, cx, cz))
        {
            Assert.True(group.Contains(x, z));
            var alone = source.GenerateLayers(x, z, source.MinChunkY, source.MaxChunkY - 1);
            var shared = source.GenerateLayers(x, z, source.MinChunkY, source.MaxChunkY - 1, group);
            Assert.Equal(alone.Count, shared.Count);
            for (var i = 0; i < alone.Count; i++)
            {
                Assert.Equal(alone[i].ComputeDigestHex(), shared[i].ComputeDigestHex());
            }

            Assert.Equal(source.SurfaceBand(x, z), source.SurfaceBand(x, z, group));
        }
    }

    [Theory]
    [MemberData(nameof(Places))]
    public void Given_AGroupThatIsNotAlignedToTheGrid_When_ItsColumnsAreMade_Then_TheyEqualTheColumnsMadeAlone(ulong seed, Feature feature)
    {
        // A ring footprint's group starts one column before its two columns (2 sx - 1) and spans four.
        var (cx, cz) = ProceduralTestWorld.Find(seed, feature);
        var source = ProceduralTestWorld.Source(seed);
        var (minX, minZ) = (cx - 1, cz - 2);
        var group = source.ShapeGroup(minX, minZ, Span);

        foreach (var (x, z) in new[] { (minX, minZ), (minX + 3, minZ + 3), (cx, cz), (minX + 1, minZ + 2), (minX + 3, minZ) })
        {
            var (min, max) = (source.MinChunkY, source.MaxChunkY - 1);
            var alone = source.GenerateLayers(x, z, min, max);
            var shared = source.GenerateLayers(x, z, min, max, group);
            for (var i = 0; i < alone.Count; i++)
            {
                Assert.Equal(alone[i].ComputeDigestHex(), shared[i].ComputeDigestHex());
            }

            Assert.Equal(source.SurfaceBand(x, z), source.SurfaceBand(x, z, group));
        }
    }

    [Fact]
    public void Given_AGroupOfOneColumn_When_ItsBandAndLayersAreMade_Then_TheyEqualTheColumnMadeAlone()
    {
        var (cx, cz) = ProceduralTestWorld.Find(42, Feature.River);
        var source = ProceduralTestWorld.Source(42);
        var group = source.ShapeGroup(cx, cz, 1);

        var band = source.SurfaceBand(cx, cz, group);

        Assert.Equal(source.SurfaceBand(cx, cz), band);
        var alone = source.GenerateLayers(cx, cz, band.MinLayer - 1, band.MaxLayer + 1);
        var shared = source.GenerateLayers(cx, cz, band.MinLayer - 1, band.MaxLayer + 1, group);
        Assert.Equal(alone.Select(static c => c.ComputeDigestHex()), shared.Select(static c => c.ComputeDigestHex()));
    }

    [Fact]
    public void Given_AColumnTheGroupDoesNotHold_When_ItIsMadeWithTheGroup_Then_ItIsMadeFromItsOwnWindow()
    {
        var (cx, cz) = ProceduralTestWorld.Find(42, Feature.Forest);
        var source = ProceduralTestWorld.Source(42);
        var group = source.ShapeGroup(Floor4(cx) + 8, Floor4(cz), Span);

        Assert.False(group.Contains(cx, cz));
        Assert.Equal(source.SurfaceBand(cx, cz), source.SurfaceBand(cx, cz, group));
        Assert.Equal(source.Generate(new VoxelChunkCoord(cx, 1, cz)).ComputeDigestHex(), source.GenerateLayers(cx, cz, 1, 1, group)[0].ComputeDigestHex());
    }

    [Fact]
    public void Given_ThreadsThatMakeColumnsFromOneGroup_When_TheyRun_Then_EachChunkEqualsTheOneMadeAlone()
    {
        var (cx, cz) = ProceduralTestWorld.Find(42, Feature.Forest);
        var source = ProceduralTestWorld.Source(42);
        var (minX, minZ) = (Floor4(cx), Floor4(cz));
        var group = source.ShapeGroup(minX, minZ, Span);
        var columns = Enumerable.Range(0, Span * Span).Select(i => (X: minX + (i % Span), Z: minZ + (i / Span))).ToList();
        var expected = columns.ToDictionary(static c => c, c => source.GenerateLayers(c.X, c.Z, 0, 3).Select(static d => d.ComputeDigestHex()).ToList());

        var made = columns.AsParallel().WithDegreeOfParallelism(4).Select(c => (c, source.GenerateLayers(c.X, c.Z, 0, 3, group).Select(static d => d.ComputeDigestHex()).ToList())).ToList();

        Assert.All(made, m => Assert.Equal(expected[m.c], m.Item2));
    }

    [Fact]
    public void Given_AGroup_When_ItsSizeIsAsked_Then_ItIsAboutAMegabyteForFourByFour()
    {
        var group = ProceduralTestWorld.Source(42).ShapeGroup(0, 0, Span);

        Assert.InRange(group.Bytes, 400_000, 1_200_000);
    }

    [Fact]
    public void Given_ColumnsInsideTheValleyAndBesideIt_When_TheyAreMadeFromAGroup_Then_TheyEqualTheColumnsMadeAlone()
    {
        var world = RegionTestWorld.Composition().World;
        var group = world.Bands.ShapeGroup(8, 8, Span); // chunk columns 8 to 11: the valley's (its footprint is 0 to 383 blocks, chunk columns 0 to 23)
        var beside = world.Bands.ShapeGroup(20, 8, Span);

        foreach (var (g, x, z) in new[] { (group, 8, 8), (group, 11, 11), (group, 9, 10), (beside, 20, 8), (beside, 23, 11), (beside, 22, 9) })
        {
            Assert.Equal(world.Bands.Of(x, z), world.Bands.Of(x, z, g));
            var alone = world.GenerateLayers(x, z, -4, 8);
            var shared = world.GenerateLayers(x, z, -4, 8, g);
            Assert.Equal(alone.Select(static c => c.ComputeDigestHex()), shared.Select(static c => c.ComputeDigestHex()));
        }
    }
}
