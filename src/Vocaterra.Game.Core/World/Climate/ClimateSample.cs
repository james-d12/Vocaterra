namespace Vocaterra.Game.Core.World.Climate;

/// <summary>
/// The climate at one column (B01, D-game-34): six smooth fields in fixed point (<see cref="Mathematics.IntMath.One"/> = 4 096 is the top of a field's range). Temperature is the
/// temperature at sea level; <see cref="IClimateSampler.TemperatureAt"/> lowers it with height. Nothing here is a biome: the biome table reads these numbers.
/// </summary>
/// <param name="Temperature">-4 096 (cold) to 4 096 (hot), at sea level.</param>
/// <param name="Humidity">-4 096 (dry) to 4 096 (wet).</param>
/// <param name="Continentalness">-4 096 (open sea) to 4 096 (far inland).</param>
/// <param name="Erosion">-4 096 (rugged and high) to 4 096 (flat and low).</param>
/// <param name="Ridges">0 (the valleys) to 4 096 (the crests of the ranges).</param>
/// <param name="River">-4 096 to 4 096: a river runs where it is near zero.</param>
public readonly record struct ClimateSample(int Temperature, int Humidity, int Continentalness, int Erosion, int Ridges, int River);
