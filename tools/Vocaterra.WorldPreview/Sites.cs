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
        var into = reaches.GroupBy(static r => (r.ToX, r.ToZ)).Where(static g => g.Count() >= 2 && g.All(static r => !r.IntoSea && !r.IntoLake)).Select(static g => (At: g.Key, Smallest: g.Min(static r => r.Area), Total: g.Sum(static r => r.Area))).OrderByDescending(static g => g.Smallest).Where(g => Open(sampler, g.At.ToX, g.At.ToZ) >= 0.7).Take(6);
        text.AppendLine("confluences in open land (x, z, the smaller river's nodes, both): " + string.Join("; ", into.Select(g => string.Create(inv, $"{g.At.ToX}, {g.At.ToZ}, {g.Smallest}, {g.Total}"))));
        var steep = reaches.Where(static r => r.Area >= 600).OrderByDescending(static r => r.FromLevel - r.ToLevel).ThenByDescending(static r => r.Area).Where(r => Open(sampler, r.FromX, r.FromZ) >= 0.7).Take(6);
        text.AppendLine("steepest reaches of rivers of 600 nodes or more in open land (from x, z, level, to level, nodes): " + string.Join("; ", steep.Select(r => string.Create(inv, $"{r.FromX}, {r.FromZ}, {r.FromLevel}, {r.ToLevel}, {r.Area}"))));
        var lakes = new List<string>();
        foreach (var reach in reaches.Where(r => r.IntoLake && r.Area >= 600 && Open(sampler, r.ToX, r.ToZ) >= 0.7).OrderByDescending(static r => r.Area).Take(10))
        {
            var path = sampler.Height.TraceRiver(reach.FromX, reach.FromZ, 4000).ToList();
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

    /// <summary>Places to stand to look at a point: dry ground a third of the range to the range (the preview command's --size is twice the range; default 50 to 150) blocks from it that is at least a few blocks over the water there, the highest first, with the yaw (0 north, 90 east) that faces the point, one for every 45 degrees round it.</summary>
    public static string Views(WorldSampler sampler, int x, int z, int far = 150)
    {
        var inv = CultureInfo.InvariantCulture;
        var at = sampler.At(x, z);
        var text = new StringBuilder();
        text.AppendLine(string.Create(inv, $"the point ({x}, {z}): ground {at.Ground}, water {(at.Water ? at.WaterLevel : -1)}"));
        for (var sector = 0; sector < 8; sector++)
        {
            (int X, int Z, int Ground, double Yaw)? best = null;
            for (var dz = -far; dz <= far; dz += 4)
            {
                for (var dx = -far; dx <= far; dx += 4)
                {
                    var d = Math.Sqrt((dx * dx) + (dz * dz));
                    var angle = (Math.Atan2(dx, -dz) * 180 / Math.PI) + 360;
                    if (d < far / 3 || d > far || (int)(((angle % 360) + 22.5) / 45) % 8 != sector)
                    {
                        continue;
                    }

                    var column = sampler.At(x + dx, z + dz);
                    if (column.Water || column.Ground < Math.Max(at.Ground, at.WaterLevel) + 3)
                    {
                        continue;
                    }

                    if (best is null || column.Ground > best.Value.Ground)
                    {
                        best = (x + dx, z + dz, column.Ground, (Math.Atan2(-dx, dz) * 180 / Math.PI + 360) % 360);
                    }
                }
            }

            if (best is { } b)
            {
                text.AppendLine(string.Create(inv, $"view {b.X} {b.Z} {b.Yaw:0} ground {b.Ground}"));
            }
        }

        return text.ToString();
    }

    // The share of the columns within 120 blocks of a point that carry no trees (water and open biomes): where a river or lake can be seen from the hills round it.
    private static double Open(WorldSampler sampler, int x, int z)
    {
        var open = 0;
        var total = 0;
        for (var dz = -120; dz <= 120; dz += 24)
        {
            for (var dx = -120; dx <= 120; dx += 24)
            {
                total++;
                var column = sampler.At(x + dx, z + dz);
                open += column.Water || sampler.Rows[column.Biome].Trees.Density == 0 ? 1 : 0;
            }
        }

        return open / (double)total;
    }
}
