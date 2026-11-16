using System.Diagnostics;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World.Climate;
using Vocaterra.Game.Core.World.Generation.Procedural;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Generation.Procedural;

public sealed class TempTimingTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(42UL)]
    [InlineData(7UL)]
    [InlineData(2026UL)]
    public void Time(ulong seed)
    {
        var data = TestGame.Data;
        var climate = new ClimateSampler(data.WorldGen, seed);
        var with = new HeightModel(data.WorldGen, seed);
        var without = new HeightModel(data.WorldGen with { Height = data.WorldGen.Height with { DrainageData = null } }, seed);
        var watch = Stopwatch.StartNew();
        with.Shape(0, 0, climate.At(0, 0));
        output.WriteLine($"TIME seed {seed}: first column (builds the landmass of the origin) {watch.Elapsed.TotalMilliseconds:0} ms");
        watch.Restart();
        with.Shape(9000, 9000, climate.At(9000, 9000));
        output.WriteLine($"TIME seed {seed}: first column at 9000,9000 {watch.Elapsed.TotalMilliseconds:0} ms");
        double Run(HeightModel model, int x0, int z0)
        {
            var w = Stopwatch.StartNew();
            for (var z = 0; z < 128; z++)
            {
                for (var x = 0; x < 128; x++)
                {
                    model.Shape(x0 + x, z0 + z, climate.At(x0 + x, z0 + z));
                }
            }

            return w.Elapsed.TotalMilliseconds * 1000 / (128 * 128);
        }

        _ = Run(with, 2000, 2000);
        _ = Run(without, 2000, 2000);
        output.WriteLine($"TIME seed {seed}: warm per column with the drainage {Run(with, 2000, 2000):0.0} us, without {Run(without, 2000, 2000):0.0} us (climate included); near rivers: {Run(with, 5700, 2950):0.0} us");
    }
}
