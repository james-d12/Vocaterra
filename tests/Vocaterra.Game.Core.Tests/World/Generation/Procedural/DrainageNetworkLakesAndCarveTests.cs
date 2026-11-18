using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World.Climate;
using Vocaterra.Game.Core.World.Generation.Procedural;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Generation.Procedural;

/// <summary>Tests split out of DrainageNetworkTests (B28), so they run beside the others: LakesAreCounted, WholeWorldOfASeed, CarveOfARiver, RiversOfASeed.</summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class DrainageNetworkLakesAndCarveTests(ITestOutputHelper output) : DrainageNetworkTestsBase(output)
{
    [Theory]
    [MemberData(nameof(Seeds))]
    public void Given_TheWholeWorldOfASeed_When_TheRiverWaterIsFollowedToItsEnd_Then_AtLeast98PercentOfItReachesTheSeaOrALake(ulong seed)
    {
        var model = new HeightModel(World, seed);
        var reaches = model.Reaches(-8192, -8192, 8192, 8192);
        var from = reaches.ToDictionary(static r => (r.FromX, r.FromZ));
        double total = 0, reaching = 0, stranded = 0;
        foreach (var reach in reaches)
        {
            var end = reach;
            var steps = 0;
            var outcome = 0; // 1 reaches water, 2 leaves the area, 3 stranded
            while (steps++ < 4000)
            {
                if (end.IntoSea || end.IntoLake)
                {
                    outcome = 1;
                    break;
                }

                if (!from.TryGetValue((end.ToX, end.ToZ), out end))
                {
                    break;
                }
            }

            if (outcome == 0)
            {
                // The chain left the area (the box's edge) or ended on nothing: it counts as stranded only when it ended away from the edge.
                var last = reach;
                var count = 0;
                while (from.TryGetValue((last.ToX, last.ToZ), out var next) && count++ < 4000)
                {
                    last = next;
                }

                var nearEdge = Math.Abs(last.ToX) > 8192 - 160 || Math.Abs(last.ToZ) > 8192 - 160;
                if (nearEdge)
                {
                    continue;
                }

                outcome = 3;
            }

            var length = Math.Sqrt(Math.Pow(reach.ToX - reach.FromX, 2) + Math.Pow(reach.ToZ - reach.FromZ, 2));
            total += length;
            reaching += outcome == 1 ? length : 0;
            stranded += outcome == 3 ? length : 0;
        }

        Output.WriteLine($"seed {seed}: {reaches.Count} reaches, {100 * reaching / total:0.00}% of the river reaches the sea or a lake, {100 * stranded / total:0.00}% is stranded");
        Assert.True(reaching >= total * 0.98, $"{100 * reaching / total:0.00}% reaches water");
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Given_ThePlaneOfASeed_When_ItsLakesAreCounted_Then_TheyAreFlatAndTakeAShareOfTheColumns(ulong seed)
    {
        var model = new HeightModel(World, seed);
        var climate = new ClimateSampler(World, seed);
        const int Side = 16_384;
        const int Step = 32;
        const int N = Side / Step;
        var lake = new bool[N * N];
        var level = new int[N * N];
        long water = 0;
        long lakeColumns = 0;
        for (var j = 0; j < N; j++)
        {
            for (var i = 0; i < N; i++)
            {
                var (x, z) = (-8192 + (i * Step), -8192 + (j * Step));
                var shape = model.Shape(x, z, climate.At(x, z));
                if (shape.Water)
                {
                    water++;
                }

                if (shape.Water && shape.LakeWeight > shape.RiverWeight && shape.LakeWeight >= 2048)
                {
                    lake[(j * N) + i] = true;
                    level[(j * N) + i] = shape.WaterLevel;
                    lakeColumns++;
                }
            }
        }

        // A lake is flat: of every two neighbouring lake columns (north-south or east-west, 32 blocks apart) all but the few that belong to two lakes a sill narrower than 32 blocks apart (one spills into the next, a step lower) are at the same level.
        long pairs = 0;
        long equal = 0;
        for (var j = 0; j < N; j++)
        {
            for (var i = 0; i < N; i++)
            {
                var k = (j * N) + i;
                if (!lake[k])
                {
                    continue;
                }

                foreach (var other in new[] { i + 1 < N ? k + 1 : -1, j + 1 < N ? k + N : -1 })
                {
                    if (other >= 0 && lake[other])
                    {
                        pairs++;
                        equal += level[k] == level[other] ? 1 : 0;
                    }
                }
            }
        }

        var bodies = lake.Count(static l => l);
        Assert.True(pairs > 100, $"{pairs} neighbouring lake columns");
        Assert.True(equal >= pairs * 94 / 100, $"{equal} of {pairs} neighbouring lake columns are at one level");
        var share = 100.0 * lakeColumns / (N * N);
        Output.WriteLine($"seed {seed}: {share:0.00}% of the columns are lake, {equal} of {pairs} neighbouring pairs level");
        Assert.InRange(share, 1.0, 4.0);
        Assert.True(bodies >= 200, $"{bodies} lake columns");
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Given_TheRiversOfASeed_When_TheirNumberAndReachAreCounted_Then_TheyAreCommonAndNoneBeginsOnACrest(ulong seed)
    {
        var model = new HeightModel(World, seed);
        var climate = new ClimateSampler(World, seed);
        var reaches = model.Reaches(-8192, -8192, 8192, 8192);
        var hasUpstream = new HashSet<(int, int)>(reaches.Select(static r => (r.ToX, r.ToZ)));
        var sources = reaches.Where(r => !hasUpstream.Contains((r.FromX, r.FromZ))).ToList();

        Output.WriteLine($"seed {seed}: {reaches.Count} reaches, {sources.Count} sources");
        Assert.InRange(sources.Count, 100, 900);
        // A source is never on a crest: the ranges' peaks add less than the height at which the river is gone, where it begins.
        foreach (var source in sources)
        {
            var c = climate.At(source.FromX, source.FromZ);
            Assert.True(c.Ridges < 3900 || source.FromLevel < World.Height.Rivers.FadeToHeight, $"a river begins on a crest at ({source.FromX}, {source.FromZ})");
            Assert.True(source.FromLevel < World.Height.Rivers.FadeToHeight, $"a river begins at {source.FromLevel}, above the height they fade out");
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Given_RowsOfColumns_When_TheCarveOfARiverOrALakeIsReadAlongThem_Then_NeighbouringColumnsDifferByLittleSoNoRiverEndsInACliff(ulong seed)
    {
        // A river's carve is looked for among the nodes round the column: if the look were too short a wide river would be cut off at the edge of it, with a wall in the ground. The weight (0 to 4096) of a carve changes by at most a few hundred from column to column.
        var model = new HeightModel(World, seed);
        var climate = new ClimateSampler(World, seed);
        var biggest = 0;
        var where = string.Empty;
        for (var z = -7000; z <= 7000; z += 2000)
        {
            var previous = default(ColumnShape);
            for (var i = 0; i <= 16384; i++)
            {
                var x = -8192 + i;
                var shape = model.Shape(x, z, climate.At(x, z));
                if (i > 0)
                {
                    var change = Math.Max(Math.Abs(shape.RiverWeight - previous.RiverWeight), Math.Abs(shape.LakeWeight - previous.LakeWeight));
                    if (change > biggest)
                    {
                        (biggest, where) = (change, $"({x}, {z})");
                    }
                }

                previous = shape;
            }
        }

        Output.WriteLine($"seed {seed}: the carve changes by at most {biggest} between two columns, at {where}");
        Assert.True(biggest <= 1300, $"the carve jumps by {biggest} at {where}");
    }
}
