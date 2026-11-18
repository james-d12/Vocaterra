using System.Globalization;
using System.Text;
using Vocaterra.Game.Core.World.Generation.Procedural;

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
        text.AppendLine(RiverWater(sampler, map));
        text.AppendLine(Network(sampler, map));
        text.AppendLine(Chains(map, 150, isSea));
        text.AppendLine(Chains(map, 180, isSea));
        text.AppendLine(Coast(map, isSea));
        text.AppendLine(Pools(map, Index(sampler, "river-channel")));
        text.AppendLine(Humps(map, Index(sampler, "river-channel")));
        var distance = DistanceFromOtherKind(map);
        text.AppendLine("coast profile (mean ground by distance from the shore, blocks):");
        text.Append("  land: ");
        text.AppendLine(Profile(map, distance, onLand: true));
        text.Append("  sea:  ");
        text.AppendLine(Profile(map, distance, onLand: false));
        return text.ToString();
    }

    // The drainage network (B02c) read straight from the model: the reaches of river in the area (each from a node to the node the water runs to), how many begin in it (sources) and end in it, whether the water ever rises along a reach
    // or a chain of reaches (it must not), the share of the river's length whose chain ends in the sea or a lake (a chain that runs off the area is left out of both), the rivers per square of 16 384 blocks, and the share of the land within 500
    // blocks of a river.
    private static string Network(WorldSampler sampler, WorldMap map)
    {
        var inv = CultureInfo.InvariantCulture;
        var x0 = map.Left;
        var z0 = map.Top;
        var x1 = map.Left + (map.Width * map.Scale);
        var z1 = map.Top + (map.Height * map.Scale);
        var reaches = sampler.Height.Reaches(x0, z0, x1, z1);
        if (reaches.Count == 0)
        {
            return "network: no reaches";
        }

        var byFrom = new Dictionary<(int, int), RiverReach>();
        foreach (var r in reaches)
        {
            byFrom[(r.FromX, r.FromZ)] = r;
        }

        var hasUpstream = new HashSet<(int, int)>(reaches.Select(r => (r.ToX, r.ToZ)));
        var sources = reaches.Count(r => !hasUpstream.Contains((r.FromX, r.FromZ)));
        long lengthTotal = 0;
        long lengthReaching = 0;
        long lengthLeaving = 0;
        long lengthStranded = 0;
        var rises = 0;
        var chainRises = 0;
        var largestRise = 0;
        foreach (var r in reaches)
        {
            var length = (long)Math.Sqrt(Math.Pow(r.ToX - r.FromX, 2) + Math.Pow(r.ToZ - r.FromZ, 2));
            if (r.ToLevel > r.FromLevel)
            {
                rises++;
                largestRise = Math.Max(largestRise, r.ToLevel - r.FromLevel);
            }

            var end = r;
            var steps = 0;
            var last = r.FromLevel;
            var outcome = 0; // 1 sea or lake, 2 leaves the area, 3 stranded
            while (steps++ < 4000)
            {
                if (end.ToLevel > end.FromLevel || end.FromLevel > last)
                {
                    chainRises++;
                    break;
                }

                last = end.FromLevel;
                if (end.IntoSea || end.IntoLake)
                {
                    outcome = 1;
                    break;
                }

                if (!byFrom.TryGetValue((end.ToX, end.ToZ), out var next))
                {
                    outcome = end.ToX <= x0 + 128 || end.ToZ <= z0 + 128 || end.ToX >= x1 - 128 || end.ToZ >= z1 - 128 ? 2 : 3;
                    break;
                }

                end = next;
            }

            lengthTotal += length;
            switch (outcome)
            {
                case 1:
                    lengthReaching += length;
                    break;
                case 2:
                    lengthLeaving += length;
                    break;
                case 3:
                    lengthStranded += length;
                    break;
            }
        }

        // The land within 500 blocks of a river: every pixel along every reach marks a disc.
        var w = map.Width;
        var h = map.Height;
        var near = new bool[w * h];
        var radius = 500 / map.Scale;
        foreach (var r in reaches)
        {
            var n = Math.Max(1, (int)(Math.Max(Math.Abs(r.ToX - r.FromX), Math.Abs(r.ToZ - r.FromZ)) / map.Scale));
            for (var s = 0; s <= n; s++)
            {
                var px = ((r.FromX + ((r.ToX - r.FromX) * (long)s / n)) - map.Left) / map.Scale;
                var pz = ((r.FromZ + ((r.ToZ - r.FromZ) * (long)s / n)) - map.Top) / map.Scale;
                for (var dz = -radius; dz <= radius; dz++)
                {
                    for (var dx = -radius; dx <= radius; dx++)
                    {
                        if ((dx * dx) + (dz * dz) > radius * radius)
                        {
                            continue;
                        }

                        long qx = px + dx;
                        long qz = pz + dz;
                        if (qx >= 0 && qz >= 0 && qx < w && qz < h)
                        {
                            near[(qz * w) + qx] = true;
                        }
                    }
                }
            }
        }

        long landColumns = 0;
        long landNear = 0;
        for (var i = 0; i < near.Length; i++)
        {
            if (!map.Water[i])
            {
                landColumns++;
                landNear += near[i] ? 1 : 0;
            }
        }

        var squares = (double)map.Width * map.Scale * map.Height * map.Scale / (16384.0 * 16384.0);
        var mouths = reaches.Count(r => r.IntoSea || r.IntoLake);
        return string.Create(inv, $"network: {reaches.Count} reaches ({lengthTotal} blocks of river); {sources} sources ({sources / squares:0} per 16 384-block square), {mouths} reaches end in the sea or a lake; rises along a reach: {rises}{(rises == 0 ? string.Empty : $" (largest {largestRise})")}, along a chain: {chainRises}; river length whose chain ends in the sea or a lake {Pct(lengthReaching, lengthReaching + lengthStranded)}% (leaving the area {Pct(lengthLeaving, lengthTotal)}%, stranded {Pct(lengthStranded, lengthTotal)}% of the river); land within 500 blocks of a river {Pct(landNear, landColumns)}%");
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

    // The water on the rivers (round 3): the river's line is where the carve is nearly full (0.9 or more of the weight) on land the sea does not reach; below the height the rivers fade out from, the share of those columns that hold water.
    // The columns above it are the river's sources, where it begins.
    private static string RiverWater(WorldSampler sampler, WorldMap map)
    {
        long line = 0;
        long wet = 0;
        long lineHigh = 0;
        long wetHigh = 0;
        var levels = new HashSet<int>();
        for (var i = 0; i < map.Ground.Length; i++)
        {
            if (map.River[i] < 3700 || map.Natural[i] <= sampler.SeaLevel + 1)
            {
                continue;
            }

            if (map.Natural[i] <= sampler.RiverFadeFrom)
            {
                line++;
                if (map.Water[i])
                {
                    wet++;
                    levels.Add(map.WaterLevel[i]);
                }
            }
            else
            {
                lineHigh++;
                wetHigh += map.Water[i] ? 1 : 0;
            }
        }

        return string.Create(CultureInfo.InvariantCulture, $"river line (carve 0.9 or more) below y {sampler.RiverFadeFrom}: {line} columns, {Pct(wet, line)}% under water ({levels.Count} different water levels); above it (the sources): {lineHigh} columns, {Pct(wetHigh, lineHigh)}% under water");
    }

    // The ranges (round 3): the eight-connected bodies of land above a height, each with its width and length from the spread of its columns (the length of a rectangle of that spread, sqrt(12 x variance) along and across its main axis), so a chain is long and thin and a massif is wide.
    // A body at the edge of the map is left out. The area-weighted median length, width and aspect, the share of the area in chains (aspect 3 or more, under 1 000 blocks wide) and in massifs (1 000 or wider).
    private static string Chains(WorldMap map, int height, bool[] isSea)
    {
        int w = map.Width, h = map.Height;
        var seen = new bool[w * h];
        var bodies = new List<(int Count, double Length, double Width)>();
        var thin = new List<(int Count, double Length, double Width)>();
        var stack = new Stack<int>();
        for (var start = 0; start < seen.Length; start++)
        {
            if (seen[start] || isSea[start] || map.Water[start] || map.Ground[start] <= height)
            {
                continue;
            }

            seen[start] = true;
            stack.Push(start);
            long n = 0;
            long edges = 0;
            double sx = 0, sz = 0, sxx = 0, szz = 0, sxz = 0;
            var atEdge = false;
            while (stack.Count > 0)
            {
                var i = stack.Pop();
                int x = i % w, y = i / w;
                n++;
                sx += x;
                sz += y;
                sxx += (double)x * x;
                szz += (double)y * y;
                sxz += (double)x * y;
                atEdge |= x == 0 || y == 0 || x == w - 1 || y == h - 1;
                foreach (var j in new[] { x > 0 ? i - 1 : -1, x < w - 1 ? i + 1 : -1, y > 0 ? i - w : -1, y < h - 1 ? i + w : -1 })
                {
                    if (j < 0 || isSea[j] || map.Water[j] || map.Ground[j] <= height)
                    {
                        edges++;
                    }
                }

                for (var dy = -1; dy <= 1; dy++)
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h)
                        {
                            continue;
                        }

                        var j = (ny * w) + nx;
                        if (!seen[j] && !isSea[j] && !map.Water[j] && map.Ground[j] > height)
                        {
                            seen[j] = true;
                            stack.Push(j);
                        }
                    }
                }
            }

            if (atEdge || n < 12)
            {
                continue;
            }

            var mx = sx / n;
            var mz = sz / n;
            var vxx = (sxx / n) - (mx * mx);
            var vzz = (szz / n) - (mz * mz);
            var vxz = (sxz / n) - (mx * mz);
            var mean = (vxx + vzz) / 2;
            var spread = Math.Sqrt((((vxx - vzz) / 2) * ((vxx - vzz) / 2)) + (vxz * vxz));
            var major = mean + spread;
            var minor = Math.Max(mean - spread, 0.01);
            bodies.Add(((int)n, Math.Sqrt(12 * major) * map.Scale, Math.Sqrt(12 * minor) * map.Scale));
            var thickness = 2.0 * n / Math.Max(1, edges) * map.Scale; // 2 x area / perimeter: the width of a strip, however the strip is bent or forked
            thin.Add(((int)n, n * map.Scale * map.Scale / Math.Max(1, thickness) / map.Scale, thickness));
        }

        if (bodies.Count == 0)
        {
            return $"land above {height}: no body that stays inside the map";
        }

        var total = bodies.Sum(b => (long)b.Count);
        double Median(Func<(int Count, double Length, double Width), double> key)
        {
            var sorted = bodies.OrderBy(key).ToList();
            long acc = 0;
            foreach (var b in sorted)
            {
                acc += b.Count;
                if (acc * 2 >= total)
                {
                    return key(b);
                }
            }

            return 0;
        }

        double MedianThin(Func<(int Count, double Length, double Width), double> key)
        {
            var sorted = thin.OrderBy(key).ToList();
            long acc = 0;
            foreach (var b in sorted)
            {
                acc += b.Count;
                if (acc * 2 >= total)
                {
                    return key(b);
                }
            }

            return 0;
        }

        // A chain is a body of land whose thickness (2 x area / perimeter) is under 500 blocks and whose extent (the length from its spread) is 4 times that or more; a massif has a thickness of 500 or more.
        var chains = 0L;
        var massifs = 0L;
        for (var i = 0; i < bodies.Count; i++)
        {
            if (thin[i].Width >= 500)
            {
                massifs += bodies[i].Count;
            }
            else if (bodies[i].Length >= 4 * thin[i].Width)
            {
                chains += bodies[i].Count;
            }
        }
        return string.Create(CultureInfo.InvariantCulture, $"land above {height}: {bodies.Count} bodies; area-weighted median length {Median(b => b.Length):0}, width {Median(b => b.Width):0}, aspect {Median(b => b.Length / b.Width):0.0}; {Pct(chains, total)}% of the area in chains (thickness under 500 blocks, extent 4 times that or more), {Pct(massifs, total)}% in massifs (thickness 500 or more); the longest {bodies.Max(b => b.Length):0}; thickness (2 x area / perimeter, area-weighted median) {MedianThin(b => b.Width):0}, length (area / thickness) {MedianThin(b => b.Length):0}");
    }

    // The straightness of the coast (round 3): the land columns next to the sea are the coast; from each, in 32 directions, a line is followed while a coast column lies within one column of it; the longest such run (both ways through the column) is how far the coast
    // runs straight there, to within about a column's width either side. The longest run anywhere, and the share of the coast that lies on a straight run of 300 blocks or more.
    private static string Coast(WorldMap map, bool[] isSea)
    {
        int w = map.Width, h = map.Height;
        var coast = new bool[w * h];
        var points = new List<int>();
        for (var y = 1; y < h - 1; y++)
        {
            for (var x = 1; x < w - 1; x++)
            {
                var i = (y * w) + x;
                if (!isSea[i] && !map.Water[i] && (isSea[i - 1] || isSea[i + 1] || isSea[i - w] || isSea[i + w]))
                {
                    coast[i] = true;
                    points.Add(i);
                }
            }
        }

        if (points.Count == 0)
        {
            return "coast: none";
        }

        const int Directions = 32;
        var maxSteps = (int)(1200 / map.Scale);
        var dxs = new double[Directions];
        var dys = new double[Directions];
        for (var d = 0; d < Directions; d++)
        {
            dxs[d] = Math.Cos(2 * Math.PI * d / Directions);
            dys[d] = Math.Sin(2 * Math.PI * d / Directions);
        }

        bool Near(double fx, double fy)
        {
            int cx = (int)Math.Round(fx), cy = (int)Math.Round(fy);
            for (var oy = -1; oy <= 1; oy++)
            {
                for (var ox = -1; ox <= 1; ox++)
                {
                    int nx = cx + ox, ny = cy + oy;
                    if (nx >= 0 && ny >= 0 && nx < w && ny < h && coast[(ny * w) + nx])
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        var run = new double[Directions];
        double longest = 0;
        long straight = 0;
        var cutoff = 300.0 / map.Scale;
        foreach (var i in points)
        {
            int x = i % w, y = i / w;
            for (var d = 0; d < Directions; d++)
            {
                var steps = 0;
                while (steps < maxSteps && Near(x + (dxs[d] * (steps + 1)), y + (dys[d] * (steps + 1))))
                {
                    steps++;
                }

                run[d] = steps;
            }

            var best = 0.0;
            for (var d = 0; d < Directions / 2; d++)
            {
                best = Math.Max(best, run[d] + run[d + (Directions / 2)]);
            }

            longest = Math.Max(longest, best);
            straight += best >= cutoff ? 1 : 0;
        }

        return string.Create(CultureInfo.InvariantCulture, $"coast: {points.Count} columns; the longest straight run (within a column of a line) {longest * map.Scale:0} blocks; {Pct(straight, points.Count)}% of the coast lies on a straight run of 300 blocks or more");
    }

    // River humps (round 3): the water along a river line should only fall towards the sea. The water columns are grouped into reaches (eight-connected columns at one level; the sea is one reach, a lake another); a hump is a reach of river
    // that stands higher than every reach it touches and touches two on different sides (neither falls into the other through lower reaches, so the water falls away on both sides of it: the line rises and falls again, over a hill or a divide between two seas; B02c: a reach that touches a river and the sea that river runs into is a step, not a hump). Reaches of fewer than 6 columns are the contour's
    // flicker and are not counted. A spring (one neighbour) is not a hump. The rise of a hump is its level less the highest of the floors its sides fall to (the lowest level reachable from each neighbour through lower reaches, not through the hump itself).
    private static string Humps(WorldMap map, int riverRow)
    {
        int w = map.Width, h = map.Height;
        var reach = new int[w * h];
        Array.Fill(reach, -1);
        var levels = new List<int>();
        var sizes = new List<int>();
        var riverColumns = new List<int>();
        var firsts = new List<int>();
        var stack = new Stack<int>();
        for (var start = 0; start < reach.Length; start++)
        {
            if (reach[start] >= 0 || !map.Water[start])
            {
                continue;
            }

            var id = levels.Count;
            levels.Add(map.WaterLevel[start]);
            sizes.Add(0);
            riverColumns.Add(0);
            firsts.Add(start);
            reach[start] = id;
            stack.Push(start);
            while (stack.Count > 0)
            {
                var i = stack.Pop();
                sizes[id]++;
                riverColumns[id] += map.Biome[i] == riverRow ? 1 : 0;
                int x = i % w, y = i / w;
                for (var dy = -1; dy <= 1; dy++)
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h)
                        {
                            continue;
                        }

                        var j = (ny * w) + nx;
                        if (reach[j] < 0 && map.Water[j] && map.WaterLevel[j] == levels[id])
                        {
                            reach[j] = id;
                            stack.Push(j);
                        }
                    }
                }
            }
        }

        var neighbours = new HashSet<int>[levels.Count];
        for (var i = 0; i < reach.Length; i++)
        {
            if (reach[i] < 0)
            {
                continue;
            }

            int x = i % w, y = i / w;
            for (var dy = -2; dy <= 2; dy++)
            {
                for (var dx = -2; dx <= 2; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h || reach[(ny * w) + nx] < 0 || reach[(ny * w) + nx] == reach[i])
                    {
                        continue;
                    }

                    (neighbours[reach[i]] ??= []).Add(reach[(ny * w) + nx]);
                }
            }
        }

        long river = riverColumns.Sum(c => (long)c);
        long inHumps = 0;
        var rises = new List<int>();
        var where = new List<string>();
        for (var r = 0; r < levels.Count; r++)
        {
            if (riverColumns[r] < 6 || neighbours[r] is not { Count: >= 2 } near || near.Any(n => levels[n] >= levels[r]))
            {
                continue;
            }

            // The water on each side: from each neighbour, every reach the water can fall to through lower reaches. The reach is a hump only if two of its neighbours lie on different sides: neither one falls into the other (a reach that touches a
            // river and the sea that river runs into is a step in a river that reaches the sea, not a hump).
            var floors = new List<int>();
            var falls = new Dictionary<int, HashSet<int>>();
            foreach (var n in near)
            {
                var seen = new HashSet<int> { n };
                var queue = new Queue<int>();
                queue.Enqueue(n);
                var floor = levels[n];
                while (queue.Count > 0)
                {
                    var m = queue.Dequeue();
                    floor = Math.Min(floor, levels[m]);
                    foreach (var k in neighbours[m] ?? [])
                    {
                        if (k != r && levels[k] < levels[m] && seen.Add(k))
                        {
                            queue.Enqueue(k);
                        }
                    }
                }

                falls[n] = seen;
                floors.Add(floor);
            }

            var sides = near.Any(a => near.Any(b => a < b && !falls[a].Contains(b) && !falls[b].Contains(a)));
            if (!sides)
            {
                continue;
            }

            inHumps += riverColumns[r];
            rises.Add(levels[r] - floors.Max());
            if (where.Count < 6)
            {
                where.Add($"({map.BlockX(firsts[r] % w)}, {map.BlockZ(firsts[r] / w)}: level {levels[r]}, {riverColumns[r]} columns, touching levels {string.Join("/", near.Select(n => levels[n]).Order())})");
            }
        }

        return string.Create(CultureInfo.InvariantCulture, $"river humps (reaches of river water higher than every reach they touch, on two sides, of 6 columns or more): {rises.Count} humps holding {Pct(inHumps, river)}% of the {river} river-water columns; the largest rise {(rises.Count == 0 ? 0 : rises.Max())} blocks, the median {(rises.Count == 0 ? 0 : rises.Order().ElementAt(rises.Count / 2))}{(where.Count == 0 ? string.Empty : "; the first: " + string.Join(" ", where))}");
    }

    // Water that is not a river's: the sea and the lakes (the biome row of the river's channel is the river's).
    private static bool Pool(WorldMap map, int i, int riverRow) => map.Water[i] && map.Biome[i] != riverRow;

    // The pools (round 2's test): the four-connected bodies of water that no river carved (not touching the edge of the map), how many are of 3 columns or fewer and of 8 or fewer: a puddle lies on the land or the beach.
    private static string Pools(WorldMap map, int riverRow)
    {
        int w = map.Width, h = map.Height;
        var seen = new bool[w * h];
        var stack = new Stack<int>();
        int bodies = 0, puddles = 0, ponds = 0;
        var where = new List<string>();
        var byBiome = new Dictionary<int, int>();
        for (var start = 0; start < seen.Length; start++)
        {
            if (seen[start] || !Pool(map, start, riverRow))
            {
                continue;
            }

            seen[start] = true;
            stack.Push(start);
            var size = 0;
            var atEdge = false;
            var first = start;
            while (stack.Count > 0)
            {
                var i = stack.Pop();
                size++;
                int x = i % w, y = i / w;
                atEdge |= x == 0 || y == 0 || x == w - 1 || y == h - 1;
                foreach (var j in new[] { x > 0 ? i - 1 : -1, x < w - 1 ? i + 1 : -1, y > 0 ? i - w : -1, y < h - 1 ? i + w : -1 })
                {
                    if (j >= 0 && !seen[j] && Pool(map, j, riverRow))
                    {
                        seen[j] = true;
                        stack.Push(j);
                    }
                }
            }

            if (!atEdge)
            {
                bodies++;
                puddles += size <= 3 ? 1 : 0;
                if (size <= 3)
                {
                    byBiome[map.Biome[first]] = byBiome.GetValueOrDefault(map.Biome[first]) + 1;
                }
                if (size <= 3 && where.Count < 6)
                {
                    where.Add($"({map.BlockX(first % w)}, {map.BlockZ(first / w)}: ground {map.Ground[first]}, biome {map.Biome[first]}, carve {map.River[first]})");
                }

                ponds += size <= 8 ? 1 : 0;
            }
        }

        return $"pools (sea and lake water, inside the map): {bodies} bodies, {puddles} of 3 columns or fewer, {ponds} of 8 or fewer, at {map.Scale} blocks a column; puddles by biome row " + string.Join(", ", byBiome.Select(kv => $"{kv.Key}: {kv.Value}")) + "; the first: " + string.Join(" ", where);
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
