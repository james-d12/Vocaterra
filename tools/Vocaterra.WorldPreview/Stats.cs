using System.Globalization;
using System.Text;

namespace Vocaterra.WorldPreview;

/// <summary>
/// The numbers the owner's map review asks for (B02b), read off a sampled map as text: the biome shares, the share of the land above 150, 200 and 250, how connected the sea is (its
/// largest connected body against all the columns of the sea, and the biggest few), the water that is not sea, the snow's lowest ground, and the coast's profile (the mean ground
/// by distance from the shore, on the land and out to sea). It names two biome ids, <c>ocean</c> and <c>lake-basin</c>, because it asks whether inland water is classed as a lake: this is a
/// tool, not the game's source.
/// </summary>
internal static class Stats
{
    private const string SeaId = "ocean";
    private const string LakeId = "lake-basin";
    private static readonly int[] Bands = [16, 48, 128, 384, 1024];

    /// <summary>The report of a map.</summary>
    public static string Report(WorldSampler sampler, WorldMap map)
    {
        var inv = CultureInfo.InvariantCulture;
        var text = new StringBuilder();
        var n = map.Ground.Length;
        var seaRow = Index(sampler, SeaId);
        var lakeRow = Index(sampler, LakeId);
        var isSea = new bool[n];
        long sea = 0;
        long lake = 0;
        long water = 0;
        long land = 0;
        long snowLand = 0;
        var lowestSnow = int.MaxValue;
        var above = new long[3];
        int[] marks = [150, 200, 250];
        for (var i = 0; i < n; i++)
        {
            if (map.Water[i])
            {
                water++;
            }
            else
            {
                land++;
                for (var k = 0; k < marks.Length; k++)
                {
                    above[k] += map.Ground[i] > marks[k] ? 1 : 0;
                }
            }

            isSea[i] = map.Biome[i] == seaRow;
            sea += isSea[i] ? 1 : 0;
            lake += map.Biome[i] == lakeRow ? 1 : 0;
            if (map.Snow[i])
            {
                snowLand++;
                lowestSnow = Math.Min(lowestSnow, map.Ground[i]);
            }
        }

        text.AppendLine(string.Create(inv, $"area: {map.Width * map.Scale} x {map.Height * map.Scale} blocks, {map.Scale} blocks a column"));
        text.AppendLine(string.Create(inv, $"water: {Pct(water, n)}% of the columns; sea (biome {SeaId}) {Pct(sea, n)}%; {LakeId} {Pct(lake, n)}%"));
        text.AppendLine(string.Create(inv, $"land above 150 / 200 / 250: {Pct(above[0], land)}% / {Pct(above[1], land)}% / {Pct(above[2], land)}% of the land"));
        text.AppendLine(string.Create(inv, $"snow: {Pct(snowLand, land)}% of the land; the lowest snow-covered ground is {(snowLand == 0 ? "none" : lowestSnow.ToString(inv))}"));

        var sizes = Components(isSea, map.Width, map.Height);
        var top = string.Join(", ", sizes.Take(5).Select(s => $"{Pct(s, sea)}%"));
        text.AppendLine(string.Create(inv, $"sea bodies: {sizes.Count}; the largest is {Pct(sizes.Count == 0 ? 0 : sizes[0], sea)}% of the sea columns; the five largest: {top}"));

        text.AppendLine(Rivers(map));
        var distance = DistanceFromOtherKind(map);
        text.AppendLine("coast profile (mean ground by distance from the shore, blocks):");
        text.Append("  land: ");
        text.AppendLine(Profile(map, distance, onLand: true));
        text.Append("  sea:  ");
        text.AppendLine(Profile(map, distance, onLand: false));
        return text.ToString();
    }

    // The rivers as bodies of columns under water that a river carved (eight neighbours): how many reach other water, the sea or a lake (a column of it within two columns of the body), and how many do not (a body that runs off the map is left out: its end is not on it).
    private static string Rivers(WorldMap map)
    {
        int w = map.Width, h = map.Height;
        var seen = new bool[w * h];
        long total = 0;
        long reaching = 0;
        int bodies = 0;
        int loose = 0;
        long looseColumns = 0;
        var looseList = new List<(long Size, int X, int Z)>();
        var stack = new Stack<int>();
        for (var start = 0; start < seen.Length; start++)
        {
            if (!IsRiver(map, start) || seen[start])
            {
                continue;
            }

            long size = 0;
            var atEdge = false;
            long sumX = 0;
            long sumY = 0;
            var touches = false;
            seen[start] = true;
            stack.Push(start);
            while (stack.Count > 0)
            {
                var i = stack.Pop();
                size++;
                var x = i % w;
                var y = i / w;
                sumX += x;
                sumY += y;
                atEdge |= x == 0 || y == 0 || x == w - 1 || y == h - 1;
                for (var dy = -2; dy <= 2; dy++)
                {
                    for (var dx = -2; dx <= 2; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h)
                        {
                            continue;
                        }

                        var j = (ny * w) + nx;
                        if (map.Water[j] && !IsRiver(map, j))
                        {
                            touches = true;
                        }
                        else if (Math.Abs(dx) <= 1 && Math.Abs(dy) <= 1 && IsRiver(map, j) && !seen[j])
                        {
                            seen[j] = true;
                            stack.Push(j);
                        }
                    }
                }
            }

            if (size < 4 || atEdge)
            {
                continue; // a body that runs off the map has an end the map does not show
            }

            bodies++;
            total += size;
            if (touches)
            {
                reaching += size;
            }
            else
            {
                loose++;
                looseColumns += size;
                looseList.Add((size, map.BlockX((int)(sumX / size)), map.BlockZ((int)(sumY / size))));
            }
        }

        return string.Create(CultureInfo.InvariantCulture, $"rivers: {bodies} bodies; {Pct(reaching, total)}% of the river columns lie in bodies that reach the sea or a lake; {loose} bodies ({Pct(looseColumns, total)}% of the columns) reach neither")
            + (looseList.Count == 0 ? string.Empty : "; the largest of them (columns at x, z): " + string.Join(", ", looseList.OrderByDescending(b => b.Size).Take(4).Select(b => $"{b.Size} at {b.X}, {b.Z}")));
    }

    // A river column: under water and carved by the river (a lake or the sea is water that is not).
    private static bool IsRiver(WorldMap map, int i) => map.Water[i] && map.River[i] > 1000;

    private static string Profile(WorldMap map, int[] distance, bool onLand)
    {
        var inv = CultureInfo.InvariantCulture;
        var sums = new long[Bands.Length + 1];
        var counts = new long[Bands.Length + 1];
        var deep = 0L;
        var total = 0L;
        for (var i = 0; i < distance.Length; i++)
        {
            if (map.Water[i] == onLand)
            {
                continue;
            }

            var d = distance[i] * map.Scale;
            var band = 0;
            while (band < Bands.Length && d >= Bands[band])
            {
                band++;
            }

            sums[band] += map.Ground[i];
            counts[band]++;
            total++;
            deep += map.Ground[i] <= 0 ? 1 : 0;
        }

        var parts = new List<string>();
        for (var b = 0; b <= Bands.Length; b++)
        {
            var label = b == 0 ? $"<{Bands[0]}" : b == Bands.Length ? $">={Bands[^1]}" : $"{Bands[b - 1]}-{Bands[b]}";
            parts.Add(counts[b] == 0 ? $"{label}: -" : string.Create(inv, $"{label}: {(double)sums[b] / counts[b]:0.0}"));
        }

        var tail = onLand ? string.Empty : string.Create(inv, $"  (ground at or below y 0: {Pct(deep, total)}% of the sea)");
        return string.Join("  ", parts) + tail;
    }

    // Each column's distance, in columns, to the nearest column of the other kind (land to water, water to land), by a breadth-first fill over the four neighbours.
    private static int[] DistanceFromOtherKind(WorldMap map)
    {
        int w = map.Width, h = map.Height;
        var distance = new int[w * h];
        Array.Fill(distance, int.MaxValue);
        var queue = new Queue<int>();
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var i = (y * w) + x;
                var edge = (x > 0 && map.Water[i - 1] != map.Water[i]) || (x < w - 1 && map.Water[i + 1] != map.Water[i])
                    || (y > 0 && map.Water[i - w] != map.Water[i]) || (y < h - 1 && map.Water[i + w] != map.Water[i]);
                if (edge)
                {
                    distance[i] = 0;
                    queue.Enqueue(i);
                }
            }
        }

        while (queue.Count > 0)
        {
            var i = queue.Dequeue();
            var x = i % w;
            var y = i / w;
            foreach (var j in new[] { x > 0 ? i - 1 : -1, x < w - 1 ? i + 1 : -1, y > 0 ? i - w : -1, y < h - 1 ? i + w : -1 })
            {
                if (j >= 0 && distance[j] == int.MaxValue)
                {
                    distance[j] = distance[i] + 1;
                    queue.Enqueue(j);
                }
            }
        }

        for (var i = 0; i < distance.Length; i++)
        {
            distance[i] = distance[i] == int.MaxValue ? int.MaxValue / 2 : distance[i];
        }

        return distance;
    }

    // The sizes of the four-connected bodies of true columns, largest first.
    private static List<long> Components(bool[] on, int w, int h)
    {
        var seen = new bool[on.Length];
        var sizes = new List<long>();
        var stack = new Stack<int>();
        for (var start = 0; start < on.Length; start++)
        {
            if (!on[start] || seen[start])
            {
                continue;
            }

            long size = 0;
            seen[start] = true;
            stack.Push(start);
            while (stack.Count > 0)
            {
                var i = stack.Pop();
                size++;
                var x = i % w;
                var y = i / w;
                foreach (var j in new[] { x > 0 ? i - 1 : -1, x < w - 1 ? i + 1 : -1, y > 0 ? i - w : -1, y < h - 1 ? i + w : -1 })
                {
                    if (j >= 0 && on[j] && !seen[j])
                    {
                        seen[j] = true;
                        stack.Push(j);
                    }
                }
            }

            sizes.Add(size);
        }

        sizes.Sort((a, b) => b.CompareTo(a));
        return sizes;
    }

    private static int Index(WorldSampler sampler, string id)
    {
        for (var i = 0; i < sampler.Rows.Count; i++)
        {
            if (sampler.Rows[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }

    private static string Pct(long part, long whole) => (whole == 0 ? 0 : 100.0 * part / whole).ToString("0.00", CultureInfo.InvariantCulture);
}
