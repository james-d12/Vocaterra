namespace Vocaterra.Game.Core.Temperature;

/// <summary>
/// One term of the place's temperature (I06, I06c): tenths of a degree that add to the sum, positive warm and negative cold. A term reads what it needs from the context and holds no state.
/// A new system is a new class, a line in <see cref="TemperatureTermRegistry"/> and a row in <c>game/temperature.json</c>'s <c>terms</c>.
/// </summary>
public interface ITemperatureTerm
{
    /// <summary>The term's id, as the data names it and the debug overlay shows it.</summary>
    string Name { get; }

    /// <summary>The term's tenths of a degree here and now.</summary>
    /// <param name="context">What the term reads.</param>
    int TenthsAt(ITemperatureContext context);
}
