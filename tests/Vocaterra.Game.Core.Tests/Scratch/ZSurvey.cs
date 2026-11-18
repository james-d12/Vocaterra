using System.Text;
using Vocaterra.Game.Core.Tests.TestSupport;
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
        foreach (var (x, z) in new[] { (176, 186), (292, 218), (200, 200), (-1784, 2952), (-632, -1144) })
        {
            var y = p.NaturalSurfaceAt(x, z);
            sb.AppendLine($"at {x},{z} surf {y} biome {p.BiomeAt(x, z).Id} T {p.TemperatureAt(x, y, z) * 1000 / 4096} Tsea {p.TemperatureAt(x, 32, z) * 1000 / 4096}");
        }
        var stats = new SortedDictionary<string, (int n, int min, int max, long sum, int minY, int maxY)>();
        for (var x = -8192; x <= 8192; x += 128)
        {
            for (var z = -8192; z <= 8192; z += 128)
            {
                var y = p.NaturalSurfaceAt(x, z);
                var id = p.BiomeAt(x, z).Id;
                var t = p.TemperatureAt(x, y, z) * 1000 / 4096;
                var (n, mn, mx, s, miy, may) = stats.GetValueOrDefault(id, (0, int.MaxValue, int.MinValue, 0L, int.MaxValue, int.MinValue));
                stats[id] = (n + 1, Math.Min(mn, t), Math.Max(mx, t), s + t, Math.Min(miy, y), Math.Max(may, y));
            }
        }
        foreach (var (id, v) in stats)
        {
            sb.AppendLine($"{id,-16} n {v.n,5} T min {v.min,6} max {v.max,6} mean {v.sum / v.n,6}  y {v.minY}..{v.maxY}");
        }
        var src = ProceduralTestWorld.Source(42);
        var sites = src.World.Entrances!;
        for (var sz = -4; sz < 4; sz++)
        {
            for (var sx = -4; sx < 4; sx++)
            {
                if (sites.Site(sx, sz) is { } r)
                {
                    var n = r.Nodes[r.Last];
                    var surf = p.NaturalSurfaceAt(n.X, n.Z);
                    sb.AppendLine($"cave anchor {r.AnchorX},{r.AnchorZ} mouthY {r.MouthY} last {n.X},{n.Z} floor {n.Floor} surf {surf} depth {surf - n.Floor} biome {p.BiomeAt(n.X, n.Z).Id} T {p.TemperatureAt(n.X, n.Floor + 1, n.Z) * 1000 / 4096}/{p.TemperatureAt(n.X, surf, n.Z) * 1000 / 4096}");
                }
            }
        }
        File.WriteAllText("/tmp/claude-1000/-home-james-Dev-Vocaterra/bc0e312e-d400-4d18-b834-c5dbe00e0e36/scratchpad/i06c-survey.txt", sb.ToString());
    }
}
