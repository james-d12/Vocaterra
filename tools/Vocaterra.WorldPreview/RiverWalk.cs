using System.Globalization;
using System.Text;

namespace Vocaterra.WorldPreview;

/// <summary>
/// A river followed along the drainage network (B02c): the river through the node nearest a point is traced from its source to the sea or the lake it ends in, the line is walked a few blocks at a time and the ground, the natural ground and the water along it are
/// drawn as one section, so the water's surface can be seen stepping down the valley and never rising. A tool, not the game's source.
/// </summary>
internal static class RiverWalk
{
    /// <summary>One sample of the walk.</summary>
    /// <param name="Distance">The blocks walked.</param>
    /// <param name="X">The column's x.</param>
    /// <param name="Z">The column's z.</param>
    /// <param name="Column">What the model says about it.</param>
    internal readonly record struct Sample(double Distance, int X, int Z, Column Column);

    /// <summary>Walks the river through the node nearest (x, z), in the order of the water's flow (source first).</summary>
    /// <param name="sampler">The world.</param>
    /// <param name="x">A point on or near the river.</param>
    /// <param name="z">A point on or near the river.</param>
    /// <param name="length">The most blocks to walk.</param>
    /// <param name="step">The blocks between two samples.</param>
    /// <param name="direction"><c>down</c>: from the point to the sea; <c>up</c>: from the source to the point; <c>both</c>: the whole river.</param>
    public static List<Sample> Walk(WorldSampler sampler, int x, int z, int length, int step, string direction)
    {
        var path = sampler.Height.TraceRiver(x, z, 4000);
        if (path.Count < 2)
        {
            throw new ArgumentException($"No river within a few nodes of ({x}, {z}).");
        }

        var near = 0;
        var best = double.MaxValue;
        for (var i = 0; i < path.Count; i++)
        {
            var d = Math.Pow(path[i].X - x, 2) + Math.Pow(path[i].Z - z, 2);
            if (d < best)
            {
                best = d;
                near = i;
            }
        }

        var points = direction switch
        {
            "down" => path.Skip(near).ToList(),
            "up" => path.Take(near + 1).ToList(),
            _ => path.ToList(),
        };
        var line = new List<(double X, double Z)>();
        for (var i = 0; i < points.Count - 1; i++)
        {
            var (ax, az) = (points[i].X, points[i].Z);
            var (bx, bz) = (points[i + 1].X, points[i + 1].Z);
            var steps = Math.Max(1, (int)Math.Round(Math.Sqrt(Math.Pow(bx - ax, 2) + Math.Pow(bz - az, 2)) / step));
            for (var s = 0; s < steps; s++)
            {
                line.Add((ax + ((bx - ax) * (double)s / steps), az + ((bz - az) * (double)s / steps)));
            }
        }

        line.Add((points[^1].X, points[^1].Z));
        var total = line.Count * (double)step;
        var from = direction == "up" ? Math.Max(0, line.Count - 1 - (length / step)) : 0;
        var to = direction == "up" ? line.Count : Math.Min(line.Count, (length / step) + 1);
        var samples = new List<Sample>();
        for (var i = from; i < to; i++)
        {
            var (cx, cz) = Centre(sampler, line[i].X, line[i].Z);
            samples.Add(new Sample((i - from) * (double)step, cx, cz, Evened(sampler, cx, cz)));
        }

        _ = total;
        return samples;
    }

    // The column of the river's line: the centre of the walk's point can be a few blocks off the bowed line the network draws, so the column nearby with the most river carve is taken.
    private static (int X, int Z) Centre(WorldSampler sampler, double px, double pz)
    {
        var (bx, bz) = ((int)Math.Round(px), (int)Math.Round(pz));
        var best = -1;
        for (var dz = -10; dz <= 10; dz += 2)
        {
            for (var dx = -10; dx <= 10; dx += 2)
            {
                var weight = sampler.At(bx + dx, bz + dz).RiverWeight;
                if (weight > best)
                {
                    best = weight;
                    (px, pz) = (bx + dx, bz + dz);
                }
            }
        }

        return ((int)Math.Round(px), (int)Math.Round(pz));
    }

    // The column as the generator makes it: a river's water takes the highest level of the river water within 3 columns (the sea's level apart), so a step lies across the river (WaterEdgePass.EvenLevels in the core).
    private static Column Evened(WorldSampler sampler, int x, int z)
    {
        var column = sampler.At(x, z);
        if (!column.Water || column.WaterLevel <= sampler.SeaLevel)
        {
            return column;
        }

        var best = column.WaterLevel;
        for (var dz = -3; dz <= 3; dz++)
        {
            for (var dx = -3; dx <= 3; dx++)
            {
                var other = sampler.At(x + dx, z + dz);
                if (other.Water)
                {
                    best = Math.Max(best, other.WaterLevel);
                }
            }
        }

        return column with { WaterLevel = best };
    }

    /// <summary>The section: the natural ground (a thin line), the ground the river carved (brown), the water (blue) and the sea level (dashed yellow), the vertical scale the data's own.</summary>
    public static Image Draw(IReadOnlyList<Sample> samples, int seaLevel, int zoom, int pixelsPerSample)
    {
        var low = samples.Min(s => s.Column.Ground) - 4;
        var high = samples.Max(s => Math.Max(s.Column.Natural, s.Column.WaterLevel)) + 4;
        var image = new Image(samples.Count * pixelsPerSample, (high - low + 1) * zoom, new Rgb(28, 32, 44));
        int Row(int y) => (high - y) * zoom;
        Rgb ground = new(150, 100, 80);
        Rgb water = new(54, 110, 200);
        Rgb natural = new(200, 200, 200);
        for (var i = 0; i < samples.Count; i++)
        {
            var c = samples[i].Column;
            for (var k = 0; k < pixelsPerSample; k++)
            {
                var px = (i * pixelsPerSample) + k;
                image.VerticalRun(px, Row(c.Ground), image.Height - 1, ground);
                if (c.Water && c.WaterLevel > c.Ground)
                {
                    image.VerticalRun(px, Row(c.WaterLevel), Row(c.Ground) - 1, water);
                }

                image.Set(px, Row(c.Natural), natural);
                if (seaLevel >= low && seaLevel <= high && (px / 6) % 2 == 0)
                {
                    image.Set(px, Row(seaLevel), new Rgb(255, 214, 64));
                }
            }
        }

        var white = new Rgb(255, 255, 255);
        for (var y = (low + 15) / 16 * 16; y <= high; y += 16)
        {
            for (var k = 0; k < (y % 64 == 0 ? 12 : 5); k++)
            {
                image.Set(k, Row(y), white);
            }
        }

        return image;
    }

    /// <summary>A few lines about the walk: how far it went, the water's levels, the length of the reaches between steps, the height of the steps and how often the water rose (it must not).</summary>
    public static string Summary(IReadOnlyList<Sample> samples)
    {
        var inv = CultureInfo.InvariantCulture;
        var text = new StringBuilder();
        var wet = samples.Count(s => s.Column.Water);
        text.AppendLine(string.Create(inv, $"walked {samples[^1].Distance:0} blocks from ({samples[0].X}, {samples[0].Z}) to ({samples[^1].X}, {samples[^1].Z}); the natural ground falls from {samples[0].Column.Natural} to {samples[^1].Column.Natural}; {Pct(wet, samples.Count)}% of the samples under water"));
        var reaches = new List<(int Level, double Length)>();
        var steps = new List<int>();
        var rises = new List<int>();
        int? level = null;
        var from = 0.0;
        foreach (var s in samples.Where(s => s.Column.Water))
        {
            if (level is { } l && l != s.Column.WaterLevel)
            {
                reaches.Add((l, s.Distance - from));
                steps.Add(Math.Abs(l - s.Column.WaterLevel));
                if (s.Column.WaterLevel > l)
                {
                    rises.Add(s.Column.WaterLevel - l);
                }

                from = s.Distance;
            }

            level = s.Column.WaterLevel;
        }

        if (reaches.Count > 0)
        {
            text.AppendLine(string.Create(inv, $"{reaches.Count} steps; water from y {samples.Where(s => s.Column.Water).Max(s => s.Column.WaterLevel)} down to {samples.Where(s => s.Column.Water).Min(s => s.Column.WaterLevel)}; reaches {reaches.Min(r => r.Length):0} to {reaches.Max(r => r.Length):0} blocks long (median {reaches.OrderBy(r => r.Length).ElementAt(reaches.Count / 2).Length:0}); steps of {steps.Min()} to {steps.Max()} blocks"));
        }

        text.AppendLine(string.Create(inv, $"rises along the flow: {rises.Count}{(rises.Count == 0 ? string.Empty : $" (the largest {rises.Max()} blocks)")}"));
        return text.ToString();
    }

    private static string Pct(long part, long whole) => (whole == 0 ? 0 : 100.0 * part / whole).ToString("0", CultureInfo.InvariantCulture);
}
