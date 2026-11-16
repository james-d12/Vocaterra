namespace Vocaterra.Game.Core.Temperature;

/// <summary>The valley's base temperature (I06): a fixed number, the mean the day's swing and the weather move round. From <c>game/temperature.json</c>.</summary>
public sealed class BaseTemperatureTerm : ITemperatureTerm
{
    private readonly int _tenths;

    /// <summary>A base term.</summary>
    /// <param name="tenths">The base temperature in tenths of a degree.</param>
    public BaseTemperatureTerm(int tenths) => _tenths = tenths;

    /// <inheritdoc />
    public string Name => "base";

    /// <inheritdoc />
    public int TenthsAt(in TemperatureSituation situation) => _tenths;
}
