using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World.Climate;
using Vocaterra.Game.Core.World.Generation.Procedural;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Generation.Procedural;

/// <summary>Landmasses read ahead and at once (B40): the network is the same whichever order, and however many threads, the landmasses are read in.</summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class DrainageNetworkWarmTests(ITestOutputHelper output) : DrainageNetworkTestsBase(output)
{
    private const int Reach = 2048;

    private static List<(int X, int Z)> Columns()
    {
        var list = new List<(int, int)>();
        for (var z = -Reach; z <= Reach; z += 61)
        {
            for (var x = -Reach; x <= Reach; x += 67)
            {
                list.Add((x, z));
            }
        }

        return list;
    }

    private static ColumnShape[] Read(HeightModel model, ClimateSampler climate, List<(int X, int Z)> columns) => [.. columns.Select(c => model.Shape(c.X, c.Z, climate.At(c.X, c.Z)))];

    [Theory]
    [InlineData(42UL)]
    [InlineData(2026UL)]
    public void Given_LandmassesReadByRowsInTwoOrdersOnOneAndOnManyThreads_When_ColumnsAreShaped_Then_TheyAreTheSame(ulong seed)
    {
        var columns = Columns();
        var climate = new ClimateSampler(World, seed);
        var step = new HeightModel(World, seed).WarmStep;
        Assert.True(step > 0);
        var rows = Enumerable.Range(0, (2 * Reach / step) + 2).Select(r => -Reach + (r * step)).ToList();

        var forward = new HeightModel(World, seed);
        foreach (var z in rows)
        {
            forward.WarmRow(z, -Reach, Reach);
        }

        var reverse = new HeightModel(World, seed);
        foreach (var z in Enumerable.Reverse(rows))
        {
            reverse.WarmRow(z, Reach, -Reach); // an empty row: nothing is read by it
        }

        foreach (var z in Enumerable.Reverse(rows))
        {
            reverse.WarmRow(z, -Reach, Reach);
        }

        var threaded = new HeightModel(World, seed);
        var threads = Enumerable.Range(0, 6).Select(t => new Thread(() =>
        {
            foreach (var z in t % 2 == 0 ? rows : Enumerable.Reverse(rows).ToList())
            {
                threaded.WarmRow(z, -Reach, Reach);
            }
        })).ToList();
        threads.ForEach(t => t.Start());
        threads.ForEach(t => t.Join());

        var cold = new HeightModel(World, seed); // read on demand, in the order of the columns
        var expected = Read(cold, climate, columns);
        Assert.Equal(expected, Read(forward, climate, columns));
        Assert.Equal(expected, Read(reverse, climate, columns));
        Assert.Equal(expected, Read(threaded, climate, columns));
        Output.WriteLine($"seed {seed}: {columns.Count} columns, {rows.Count} rows, step {step}: equal in all four orders");
    }
}
