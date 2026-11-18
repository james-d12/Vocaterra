using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World.Climate;
using Vocaterra.Game.Core.World.Drainage;
using Vocaterra.Game.Core.World.Generation.Procedural;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Generation.Procedural;

/// <summary>One water level to a lake (B02d): B02c's lakes were staircases of levels (seed 2026 round x 5300 to 6900, z 600 to 800: 47, 45, 43 and 41 with walls of water between).</summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class LakeLevelsTests(ITestOutputHelper output) : DrainageNetworkTestsBase(output)
{
    [Theory]
    [InlineData(620)]
    [InlineData(700)]
    [InlineData(780)]
    public void Given_ARowAcrossTheSeed2026Lake_When_ItsRunsOfLakeWaterAreRead_Then_EachRunIsOneLevel(int z)
    {
        var model = new HeightModel(World, 2026);
        var climate = new ClimateSampler(World, 2026);
        var runs = 0;
        var columns = 0;
        var (start, level) = (-1, -1);
        for (var x = 5300; x <= 6900; x++)
        {
            var shape = model.Shape(x, z, climate.At(x, z));
            var lake = shape.Water && shape.LakeWeight > shape.RiverWeight;
            if (lake)
            {
                columns++;
                if (start < 0)
                {
                    (start, level) = (x, shape.WaterLevel);
                    runs++;
                }

                Assert.True(shape.WaterLevel == level, $"z {z}: the lake run from x {start} is at {level}, and at x {x} at {shape.WaterLevel}");
            }
            else
            {
                start = -1;
            }
        }

        Output.WriteLine($"z {z}: {runs} runs of lake, {columns} columns");
        Assert.True(columns >= 100, $"{columns} lake columns on the row: the lake the test is about is not there");
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Given_TheLakesOfASeed_When_TheirBodiesAreRead_Then_NoBodyHoldsTwoLevelsAndNoOutflowRisesAboveItsLake(ulong seed)
    {
        var model = new HeightModel(World, seed);
        var drainage = World.Height.DrainageData!;
        var network = new DrainageNetwork(drainage, World.Vertical.SeaLevel, seed, World.Height.Rivers.Width, model);
        const int Half = 8192;
        var spacing = network.Spacing;
        var (i0, i1) = (-Half / spacing, Half / spacing);
        var side = i1 - i0 + 1;
        var info = new DrainageNodeInfo[side * side];
        for (var j = 0; j < side; j++)
        {
            for (var i = 0; i < side; i++)
            {
                info[(j * side) + i] = network.Info(i0 + i, i0 + j);
            }
        }

        int[] dx = [1, 1, 0, -1, -1, -1, 0, 1];
        int[] dz = [0, 1, 1, 1, 0, -1, -1, -1];
        var seen = new bool[info.Length];
        var (bodies, nodes, outflows) = (0, 0, 0);
        for (var first = 0; first < info.Length; first++)
        {
            if (seen[first] || !info[first].Lake)
            {
                continue;
            }

            var body = new List<int> { first };
            seen[first] = true;
            for (var b = 0; b < body.Count; b++)
            {
                var (bi, bj) = (body[b] % side, body[b] / side);
                for (var d = 0; d < 8; d++)
                {
                    var (ni, nj) = (bi + dx[d], bj + dz[d]);
                    if ((uint)ni >= (uint)side || (uint)nj >= (uint)side)
                    {
                        continue;
                    }

                    var m = (nj * side) + ni;
                    if (!seen[m] && info[m].Lake)
                    {
                        seen[m] = true;
                        body.Add(m);
                    }
                }
            }

            bodies++;
            nodes += body.Count;
            var levels = body.Select(m => info[m].Level16).Distinct().ToList();
            var (fi, fj) = (first % side + i0, first / side + i0);
            Assert.True(levels.Count == 1, $"the lake body of {body.Count} nodes through node ({fi}, {fj}) holds {levels.Count} levels: {string.Join(", ", levels.Order())}");
            foreach (var m in body)
            {
                var next = info[m].Next;
                if (next < 0)
                {
                    continue;
                }

                var (ti, tj) = ((m % side) + dx[next], (m / side) + dz[next]);
                if ((uint)ti >= (uint)side || (uint)tj >= (uint)side || info[(tj * side) + ti].Lake)
                {
                    continue;
                }

                outflows++;
                Assert.True(info[(tj * side) + ti].Level16 <= levels[0], $"the water leaving the lake through node ({fi}, {fj}) rises from {levels[0]} to {info[(tj * side) + ti].Level16}");
            }
        }

        Output.WriteLine($"seed {seed}: {bodies} lake bodies, {nodes} nodes, {outflows} outflows");
        Assert.True(bodies >= 5, $"{bodies} lake bodies");
    }
}
