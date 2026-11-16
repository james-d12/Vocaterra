namespace Vocaterra.WorldPreview;

/// <summary>
/// The drainage network drawn over the height map (B02c): every reach of river as a line from node to node, thicker for more ground draining into it; with a point (<c>--x --z</c> on a river) the whole drainage basin of that river, the river and all its tributaries to
/// the sea or lake it ends in, is drawn in orange and the rest in blue. A tool, not the game's source.
/// </summary>
internal static class DrainageMap
{
    private static readonly Rgb Basin = new(255, 150, 30);
    private static readonly Rgb Other = new(40, 110, 255);
    private static readonly Rgb Source = new(255, 255, 255);

    /// <summary>Draws the reaches of a map's area over the picture, and returns how many reaches the highlighted basin holds (0 when no basin was asked for).</summary>
    public static int Draw(Image image, WorldMap map, WorldSampler sampler, int? basinX, int? basinZ)
    {
        var reaches = sampler.Height.Reaches(map.Left, map.Top, map.Left + (map.Width * map.Scale), map.Top + (map.Height * map.Scale));
        var inBasin = new HashSet<(int, int)>();
        if (basinX is { } bx && basinZ is { } bz)
        {
            var path = sampler.Height.TraceRiver(bx, bz, 4000);
            if (path.Count > 0)
            {
                var sink = path[^1];
                var next = new Dictionary<(int, int), (int, int)>();
                foreach (var r in reaches)
                {
                    next[(r.FromX, r.FromZ)] = (r.ToX, r.ToZ);
                }

                foreach (var r in reaches)
                {
                    var at = (r.FromX, r.FromZ);
                    for (var step = 0; step < 4000; step++)
                    {
                        if (!next.TryGetValue(at, out var to))
                        {
                            break;
                        }

                        if (to == (sink.X, sink.Z))
                        {
                            inBasin.Add((r.FromX, r.FromZ));
                            break;
                        }

                        at = to;
                    }
                }
            }
        }

        foreach (var r in reaches.OrderBy(r => r.Area))
        {
            var width = r.Area < 3000 ? 1 : r.Area < 7000 ? 2 : r.Area < 15000 ? 3 : 4;
            var colour = inBasin.Contains((r.FromX, r.FromZ)) ? Basin : Other;
            Line(image, map, r.FromX, r.FromZ, r.ToX, r.ToZ, width, colour);
        }

        return inBasin.Count;
    }

    private static void Line(Image image, WorldMap map, int x0, int z0, int x1, int z1, int width, Rgb colour)
    {
        var length = Math.Max(Math.Abs(x1 - x0), Math.Abs(z1 - z0));
        var steps = Math.Max(1, length / Math.Max(1, map.Scale / 2));
        for (var s = 0; s <= steps; s++)
        {
            var px = ((x0 + ((x1 - x0) * (long)s / steps)) - map.Left) / (double)map.Scale;
            var pz = ((z0 + ((z1 - z0) * (long)s / steps)) - map.Top) / (double)map.Scale;
            for (var dz = 0; dz < width; dz++)
            {
                for (var dx = 0; dx < width; dx++)
                {
                    image.Set((int)px + dx - ((width - 1) / 2), (int)pz + dz - ((width - 1) / 2), colour);
                }
            }
        }
    }
}
