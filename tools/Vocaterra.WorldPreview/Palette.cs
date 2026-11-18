using Vocaterra.Game.Core.World.Biomes;

namespace Vocaterra.WorldPreview;

/// <summary>
/// The colour of each biome row, from the row's position in the file and whether it is a water row (a <c>when</c> that asks for water), never from its id: the hue steps round the
/// colour wheel by the golden ratio, so rows next to each other in the file are far apart in colour, and the water rows are blues of different lightness.
/// </summary>
internal sealed class Palette
{
    private const double GoldenRatio = 0.6180339887498949;

    private readonly Rgb[] _colours;

    /// <summary>Creates the palette of a table's rows.</summary>
    public Palette(IReadOnlyList<BiomeRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        _colours = new Rgb[rows.Count];
        var water = 0;
        foreach (var row in rows)
        {
            if (row.When.Water == true)
            {
                _colours[row.Index] = FromHsv(0.58 + (0.015 * (water % 3)), 0.75, Math.Max(0.35, 0.9 - (0.16 * water)));
                water++;
            }
            else
            {
                var hue = (row.Index * GoldenRatio) % 1.0;
                _colours[row.Index] = FromHsv(hue, row.Index % 2 == 0 ? 0.7 : 0.45, row.Index % 3 == 0 ? 0.95 : 0.78);
            }
        }
    }

    /// <summary>The colour of the row.</summary>
    public Rgb Of(BiomeRow row) => _colours[row.Index];

    /// <summary>The colour of the row at a position in the file.</summary>
    public Rgb Of(int index) => _colours[index];

    private static Rgb FromHsv(double h, double s, double v)
    {
        h = (h % 1.0) * 6.0;
        var sector = (int)Math.Floor(h);
        var f = h - sector;
        var p = v * (1 - s);
        var q = v * (1 - (s * f));
        var t = v * (1 - (s * (1 - f)));
        var (r, g, b) = sector switch
        {
            0 => (v, t, p),
            1 => (q, v, p),
            2 => (p, v, t),
            3 => (p, q, v),
            4 => (t, p, v),
            _ => (v, p, q),
        };
        return new Rgb((byte)Math.Round(r * 255), (byte)Math.Round(g * 255), (byte)Math.Round(b * 255));
    }
}
