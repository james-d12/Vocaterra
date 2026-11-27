namespace Vocaterra.Game.Core.World.Options;

/// <summary>How an option's value is laid over a generator value (the <c>mode</c> of an effect row in <c>game/world-options.json</c>).</summary>
public enum TerrainMode
{
    /// <summary>The generator value is multiplied by the option's value as a percent of the default (100 leaves it as it is); rounded to the nearest whole number.</summary>
    Scale,

    /// <summary>The generator value is divided by the option's value as a percent (the option rises as the value falls: more rivers, a lower source area); 100 leaves it as it is.</summary>
    ScaleInverse,

    /// <summary>The option's value is added to the generator value (0 leaves it as it is).</summary>
    Offset,

    /// <summary>The generator value is replaced by the option's value (the option's default is then the file's value).</summary>
    Set,
}

/// <summary>
/// One generator value, or a few of one kind, that an option may change (Q02): a name the rows of <c>game/world-options.json</c> use, the way to read its current numbers out of <see cref="WorldGenData"/>
/// and the way to rebuild the data with each of them put through a function. The list is the code's (the data says which target an option moves and how, and may not name another), and every target
/// is a number the generator reads from <c>game/world.json</c> alone, so an option never touches the valley (the authored region) or the vertical range and the sea level the valley's coordinates fix.
/// </summary>
/// <param name="Name">The target's name, the path of the value in <c>game/world.json</c>.</param>
/// <param name="Read">The target's current numbers, or <see langword="null"/> when the world file has no such section.</param>
/// <param name="Map">The data with every number of the target put through the function.</param>
internal sealed record TerrainTarget(string Name, Func<WorldGenData, IReadOnlyList<int>?> Read, Func<WorldGenData, Func<int, int>, WorldGenData> Map)
{
    /// <summary>The targets an option may name, by name (ordinal).</summary>
    public static IReadOnlyDictionary<string, TerrainTarget> All { get; } = new TerrainTarget[]
    {
        // The continents' width (the cell of the jittered cells the land is cut into): bigger cells are bigger continents and wider seas between the lands' hearts (the channels keep their width).
        new("climate.continents.cell", static d => d.Climate.Continents is { } c ? [c.Cell] : null, static (d, f) => d.Climate.Continents is { } c ? d with { Climate = d.Climate with { Continents = c with { Cell = f(c.Cell) } } } : d),

        // The sea cells of every block of cells: more of them is more ocean and fewer, smaller lands.
        new("climate.continents.seaPerBlock", static d => d.Climate.Continents is { } c ? [c.SeaPerBlock] : null, static (d, f) => d.Climate.Continents is { } c ? d with { Climate = d.Climate with { Continents = c with { SeaPerBlock = f(c.SeaPerBlock) } } } : d),

        // The height the ridges add (the peaks spline's outputs, blocks): the mountain ranges' height above the foothills.
        new("height.peaks", static d => [.. d.Height.Peaks.Select(static p => p.Y)], static (d, f) => d with { Height = d.Height with { Peaks = [.. d.Height.Peaks.Select(p => p with { Y = f(p.Y) })] } }),

        // The hills' amplitude (blocks) on the eroded flats and on the rugged land.
        new("height.detail.calmAmplitude", static d => [d.Height.Detail.CalmAmplitude], static (d, f) => d with { Height = d.Height with { Detail = d.Height.Detail with { CalmAmplitude = f(d.Height.Detail.CalmAmplitude) } } }),
        new("height.detail.ruggedAmplitude", static d => [d.Height.Detail.RuggedAmplitude], static (d, f) => d with { Height = d.Height with { Detail = d.Height.Detail with { RuggedAmplitude = f(d.Height.Detail.RuggedAmplitude) } } }),

        // How many nodes must drain through a node for a river to begin: the lower, the more rivers.
        new("height.drainage.sourceArea", static d => d.Height.DrainageData is { } n ? [n.SourceArea] : null, static (d, f) => d.Height.DrainageData is { } n ? d with { Height = d.Height with { DrainageData = n with { SourceArea = f(n.SourceArea) } } } : d),
    }.ToDictionary(static t => t.Name, StringComparer.Ordinal);

    /// <summary>The function that puts a generator value through an effect of a mode at an option value.</summary>
    /// <param name="mode">The mode.</param>
    /// <param name="value">The option's value.</param>
    public static Func<int, int> Function(TerrainMode mode, int value) => mode switch
    {
        TerrainMode.Scale => current => RoundedDivide((long)current * value, 100),
        TerrainMode.ScaleInverse => current => RoundedDivide((long)current * 100, Math.Max(1, value)),
        TerrainMode.Offset => current => current + value,
        TerrainMode.Set => _ => value,
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };

    // Integer division to the nearest whole number, halves away from zero: the same answer on every machine.
    private static int RoundedDivide(long numerator, long denominator)
    {
        var half = denominator / 2;
        return (int)(numerator >= 0 ? (numerator + half) / denominator : -((-numerator + half) / denominator));
    }
}
