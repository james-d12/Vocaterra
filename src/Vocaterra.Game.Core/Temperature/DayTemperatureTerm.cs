namespace Vocaterra.Game.Core.Temperature;

/// <summary>
/// The day's swing (I06): plus the amplitude in full daylight, minus it in the dark, straight between (the daylight curve of <c>day.json</c>). I06c: the biome's share scales it (a plain swings
/// more than a forest) and the exposure fades it out underground. From <c>game/temperature.json</c>.
/// </summary>
public sealed class DayTemperatureTerm : ITemperatureTerm
{
    /// <summary>The term's id in the data and the registry.</summary>
    public const string Id = "day";

    private readonly int _amplitude;

    /// <summary>A day term.</summary>
    /// <param name="amplitudeTenths">The swing in tenths of a degree.</param>
    public DayTemperatureTerm(int amplitudeTenths) => _amplitude = amplitudeTenths;

    /// <inheritdoc />
    public string Name => Id;

    /// <summary>Reads the term's row.</summary>
    /// <param name="parameters">The row's parameters.</param>
    public static ITemperatureTerm From(TermParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        return new DayTemperatureTerm(parameters.Int("amplitudeTenths", 0, 2000));
    }

    /// <inheritdoc />
    public int TenthsAt(ITemperatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var swing = Permille.Times(_amplitude, (2 * Permille.Of(context.Atmosphere.Daylight)) - 1000);
        return Permille.Times(Permille.Times(swing, context.Biome.DaySwingPermille), context.Exposure);
    }
}
