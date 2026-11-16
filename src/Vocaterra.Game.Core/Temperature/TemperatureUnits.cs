using System.Globalization;

namespace Vocaterra.Game.Core.Temperature;

/// <summary>The temperature's unit (I06): whole tenths of a degree Celsius, and the way a person writes them (<c>12.5</c>, <c>-3</c>).</summary>
public static class TemperatureUnits
{
    /// <summary>Tenths of a degree from degrees, to the nearest tenth (half away from zero).</summary>
    /// <param name="celsius">The degrees Celsius.</param>
    public static int FromCelsius(double celsius) => (int)Math.Round(celsius * 10, MidpointRounding.AwayFromZero);

    /// <summary>Degrees as text with one decimal and a dot, whatever the culture (<c>125</c> is <c>12.5</c>, <c>-5</c> is <c>-0.5</c>).</summary>
    /// <param name="tenths">The tenths of a degree.</param>
    public static string Format(int tenths) => (tenths / 10.0).ToString("0.0", CultureInfo.InvariantCulture);
}
