namespace Vocaterra.Game.Core.Temperature;

/// <summary>What one term added to a temperature (<see cref="PlaceTemperature.Breakdown"/>).</summary>
/// <param name="Term">The term's <see cref="ITemperatureTerm.Name"/>.</param>
/// <param name="Tenths">Its contribution in tenths of a degree.</param>
public readonly record struct TemperatureContribution(string Term, int Tenths);
