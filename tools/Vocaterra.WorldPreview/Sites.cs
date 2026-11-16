using System.Globalization;
using System.Text;

namespace Vocaterra.WorldPreview;

/// <summary>Where the drainage network of an area has something worth a picture (B02c): the confluences of two large rivers, the steepest stretches of river (the water falling in the most steps), and lakes with the river that leaves them. Text; a tool, not the game's source.</summary>
internal static class Sites
{
    /// <summary>Describes the sites of a map's area.</summary>
    public static string Describe(WorldSampler sampler, WorldMap map)
    {
        var inv = CultureInfo.InvariantCulture;
        var reaches = sampler.Height.Reaches(map.Left, map.Top, map.Left + (map.Width * map.Scale), map.Top + (map.Height * map.Scale));
        var text = new StringBuilder();
        var into = reaches.GroupBy(static r => (r.ToX, r.ToZ)).Where(static g => g.Count() >= 2 && g.All(static r => !r.IntoSea && !r.IntoLake)).Select(static g => (At: g.Key, Smallest: g.Min(static r => r.Area), Total: g.Sum(static r => r.Area))).OrderByDescending(static g => g.Smallest).Take(6);
        text.AppendLine("confluences (x, z, the smaller river's nodes, both): " + string.Join("; ", into.Select(g => string.Create(inv, $"{g.At.ToX}, {g.At.ToZ}, {g.Smallest}, {g.Total}"))));
        var steep = reaches.Where(static r => r.Area >= 600).OrderByDescending(static r => r.FromLevel - r.ToLevel).ThenByDescending(static r => r.Area).Take(6);
        text.AppendLine("steepest reaches of rivers of 600 nodes or more (from x, z, level, to level, nodes): " + string.Join("; ", steep.Select(r => string.Create(inv, $"{r.FromX}, {r.FromZ}, {r.FromLevel}, {r.ToLevel}, {r.Area}"))));
        var lakes = new List<string>();
        foreach (var reach in reaches.Where(static r => r.IntoLake && r.Area >= 600).OrderByDescending(static r => r.Area).Take(10))
        {
            var path = sampler.Height.TraceRiver(reach.FromX, reach.FromZ, 4000);
            var first = path.FindIndex(static p => p.Lake);
            if (first < 0)
            {
                continue;
            }

            var last = first;
            while (last + 1 < path.Count && path[last + 1].Lake)
            {
                last++;
            }

            if (last + 1 < path.Count && !path[last + 1].Sea)
            {
                lakes.Add(string.Create(inv, $"in at {path[first].X}, {path[first].Z}, out at {path[last + 1].X}, {path[last + 1].Z}, lake level {path[first].Level}, {path[last + 1].Area} nodes"));
            }
        }

        text.AppendLine("lakes with a river in and out: " + string.Join("; ", lakes.Take(6)));
        return text.ToString();
    }
}
