using System.Text;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Tests.Temperature;
using Vocaterra.Game.Core.World;

namespace Vocaterra.Game.Core.Tests.Scratch;

public sealed class ZSurvey
{
    [Fact]
    public void Survey()
    {
        var sb = new StringBuilder();
        var scene = WorldFactory.Create(TestGame.Data, 42, WorldMode.Bounded);
        var p = scene.Places!;
        int Feel(int x, int y, int z, string kind, int hour) => TemperatureRig.Place.TenthsAt(TemperatureRig.At(TemperatureRig.Sky(hour, kind), p, x, y, z));
        void Row(string name, int x, int y, int z)
        {
            var cells = new List<string>();
            foreach (var (kind, hour) in new[] { ("sunny", 13), ("sunny", 0), ("rain", 13), ("rain", 0) })
            {
                var f = Feel(x, y, z, kind, hour);
                cells.Add($"{f / 10.0,6:0.0} {TemperatureRig.Data.BandFor(f).Id,-9}/{TemperatureRig.Data.BandFor(f + 100).Id,-9}");
            }
            sb.AppendLine($"{name,-28} {p.BiomeAt(x, z).Id,-14} y{y,4} | {string.Join(" | ", cells)}");
        }
        sb.AppendLine("place C (band at +0 / band at +10); columns: noon clear | night clear | noon rain | night rain");
        foreach (var (n, x, z) in new[] { ("camp", 176, 186), ("village", 292, 218), ("mid slope", -1784, 2952), ("highest peak", -632, -1144) })
        {
            Row(n, x, p.NaturalSurfaceAt(x, z) + 1, z);
        }
        Row("cave deep (-47,190)", -47, 23, 190);
        Row("cave shallow (274,-62) d62", 279, 33, -62);
        (int x, int z, int f)? warm = null, cold = null;
        var byBiome = new SortedDictionary<string, (int min, int max)>();
        for (var x = -8192; x <= 8192; x += 128)
        {
            for (var z = -8192; z <= 8192; z += 128)
            {
                var id = p.BiomeAt(x, z).Id;
                var y = p.NaturalSurfaceAt(x, z);
                if (y <= 36 && (id is "ocean" or "lake-basin" or "river-channel"))
                {
                    continue;
                }
                var fn = Feel(x, y + 1, z, "sunny", 13);
                var fm = Feel(x, y + 1, z, "sunny", 0);
                if (warm is null || fn > warm.Value.f) warm = (x, z, fn);
                if (cold is null || fm < cold.Value.f) cold = (x, z, fm);
                var (mn, mx) = byBiome.GetValueOrDefault(id, (int.MaxValue, int.MinValue));
                byBiome[id] = (Math.Min(mn, fm), Math.Max(mx, fn));
            }
        }
        Row("warmest noon column", warm!.Value.x, p.NaturalSurfaceAt(warm.Value.x, warm.Value.z) + 1, warm.Value.z);
        Row("coldest night column", cold!.Value.x, p.NaturalSurfaceAt(cold.Value.x, cold.Value.z) + 1, cold.Value.z);
        foreach (var (id, v) in byBiome)
        {
            sb.AppendLine($"{id,-16} clear-night min {v.min / 10.0,6:0.0}  clear-noon max {v.max / 10.0,6:0.0}");
        }
        File.WriteAllText("/tmp/claude-1000/-home-james-Dev-Vocaterra/bc0e312e-d400-4d18-b834-c5dbe00e0e36/scratchpad/i06c-feel.txt", sb.ToString());
    }
}
