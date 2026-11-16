using System.Globalization;
using System.Text;

namespace Vocaterra.WorldPreview;

/// <summary>
/// A river followed from a point on it (B02b round 3): the river field's zero line walked a few blocks at a time (the centre line is found again at each step by Newton's method on the field), downstream or upstream, and the ground, the natural
/// ground and the water along it drawn as one section, so the water's surface can be seen stepping down the valley from the source to the sea. A tool, not the game's source: it uses floating point.
/// </summary>
internal static class RiverWalk
{
    /// <summary>One sample of the walk.</summary>
    /// <param name="Distance">The blocks walked.</param>
    /// <param name="X">The column's x.</param>
    /// <param name="Z">The column's z.</param>
    /// <param name="Column">What the model says about it.</param>
    internal readonly record struct Sample(double Distance, int X, int Z, Column Column);

    /// <summary>Walks a river from the line nearest (x, z).</summary>
    /// <param name="sampler">The world.</param>
    /// <param name="x">Where to start.</param>
    /// <param name="z">Where to start.</param>
    /// <param name="length">How far to walk, in blocks.</param>
    /// <param name="step">The blocks between two samples.</param>
    /// <param name="downstream">Whether to walk to where the ground falls (to the sea), or the other way (to the source).</param>
    public static List<Sample> Walk(WorldSampler sampler, int x, int z, int length, int step, bool downstream)
    {
        var (px, pz) = Snap(sampler, x, z);
        var (tx, tz) = Tangent(sampler, px, pz);
        // The way down: the way along the line the natural ground ends lower after 1 500 blocks (a short look would be fooled by the hills).
        var ahead = Run(sampler, px, pz, tx, tz, 1500, step);
        var behind = Run(sampler, px, pz, -tx, -tz, 1500, step);
        var aheadFalls = ahead[^1].Column.Natural < behind[^1].Column.Natural;
        if (aheadFalls != downstream)
        {
            (tx, tz) = (-tx, -tz);
        }

        return Run(sampler, px, pz, tx, tz, length, step);
    }

    private static List<Sample> Run(WorldSampler sampler, double px, double pz, double tx, double tz, int length, int step)
    {
        var samples = new List<Sample>();
        for (double walked = 0; walked <= length; walked += step)
        {
            var (cx, cz) = ((int)Math.Round(px), (int)Math.Round(pz));
            samples.Add(new Sample(walked, cx, cz, Evened(sampler, cx, cz)));
            (px, pz) = (px + (tx * step), pz + (tz * step));
            (px, pz) = SnapFrom(sampler, px, pz, 3);
            var (nx, nz) = Tangent(sampler, px, pz);
            if ((nx * tx) + (nz * tz) < 0)
            {
                (nx, nz) = (-nx, -nz);
            }

            (tx, tz) = (nx, nz);
        }

        return samples;
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

    /// <summary>A few lines about the walk: how far it went, the water's levels, the length of the reaches between steps and the height of the steps.</summary>
    public static string Summary(IReadOnlyList<Sample> samples)
    {
        var inv = CultureInfo.InvariantCulture;
        var text = new StringBuilder();
        var wet = samples.Count(s => s.Column.Water);
        text.AppendLine(string.Create(inv, $"walked {samples[^1].Distance:0} blocks from ({samples[0].X}, {samples[0].Z}) to ({samples[^1].X}, {samples[^1].Z}); the ground falls from {samples[0].Column.Natural} to {samples[^1].Column.Natural}; {Pct(wet, samples.Count)}% of the samples under water"));
        var reaches = new List<(int Level, double Length)>();
        var steps = new List<int>();
        int? level = null;
        var from = 0.0;
        foreach (var s in samples.Where(s => s.Column.Water))
        {
            if (level is { } l && l != s.Column.WaterLevel)
            {
                reaches.Add((l, s.Distance - from));
                steps.Add(Math.Abs(l - s.Column.WaterLevel));
                from = s.Distance;
            }

            level ??= s.Column.WaterLevel;
            level = s.Column.WaterLevel;
        }

        if (reaches.Count > 0)
        {
            text.AppendLine(string.Create(inv, $"{reaches.Count} steps; water from y {samples.Where(s => s.Column.Water).Max(s => s.Column.WaterLevel)} down to {samples.Where(s => s.Column.Water).Min(s => s.Column.WaterLevel)}; reaches {reaches.Min(r => r.Length):0} to {reaches.Max(r => r.Length):0} blocks long (median {reaches.OrderBy(r => r.Length).ElementAt(reaches.Count / 2).Length:0}); steps of {steps.Min()} to {steps.Max()} blocks"));
        }

        return text.ToString();
    }

    private static (double X, double Z) Snap(WorldSampler sampler, int x, int z) => SnapFrom(sampler, x, z, 12);

    // Newton's method on the river field: towards the nearest zero line.
    private static (double X, double Z) SnapFrom(WorldSampler sampler, double x, double z, int rounds)
    {
        for (var i = 0; i < rounds; i++)
        {
            var r = sampler.RiverField((int)Math.Round(x), (int)Math.Round(z));
            if (r == 0)
            {
                break;
            }

            var (gx, gz) = Gradient(sampler, x, z);
            var g2 = (gx * gx) + (gz * gz);
            if (g2 < 1e-9)
            {
                break;
            }

            (x, z) = (x - (r * gx / g2), z - (r * gz / g2));
        }

        return (x, z);
    }

    private static (double X, double Z) Gradient(WorldSampler sampler, double x, double z)
    {
        const int D = 4;
        int ix = (int)Math.Round(x), iz = (int)Math.Round(z);
        return ((sampler.RiverField(ix + D, iz) - sampler.RiverField(ix - D, iz)) / (2.0 * D), (sampler.RiverField(ix, iz + D) - sampler.RiverField(ix, iz - D)) / (2.0 * D));
    }

    private static (double X, double Z) Tangent(WorldSampler sampler, double x, double z)
    {
        var (gx, gz) = Gradient(sampler, x, z);
        var n = Math.Sqrt((gx * gx) + (gz * gz));
        return n < 1e-9 ? (1, 0) : (-gz / n, gx / n);
    }

    private static string Pct(long part, long whole) => (whole == 0 ? 0 : 100.0 * part / whole).ToString("0", CultureInfo.InvariantCulture);
}
