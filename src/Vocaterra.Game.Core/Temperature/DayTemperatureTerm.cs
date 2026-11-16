namespace Vocaterra.Game.Core.Temperature;

/// <summary>
/// The day's swing (I06): warmer by the amplitude in full daylight, colder by it in full dark, and straight between by the daylight (<see cref="Atmosphere.AtmosphereSnapshot.Daylight"/>,
/// the day clock's own curve), so the night is the cold half and the dawn and dusk ease across. From <c>game/temperature.json</c>.
/// </summary>
public sealed class DayTemperatureTerm : ITemperatureTerm
{
    private readonly int _amplitude;

    /// <summary>A day term.</summary>
    /// <param name="amplitudeTenths">How far above or below the base the day and the night go, in tenths of a degree.</param>
    public DayTemperatureTerm(int amplitudeTenths) => _amplitude = amplitudeTenths;

    /// <inheritdoc />
    public string Name => "day";

    /// <inheritdoc />
    public int TenthsAt(in TemperatureSituation situation) => Permille.Times(_amplitude, (2 * Permille.Of(situation.Atmosphere.Daylight)) - 1000);
}
