namespace Vocaterra.Game.Core.Temperature;

/// <summary>
/// Where the terms of the temperature are registered (I06): the one list the session builds its <see cref="PlaceTemperature"/> from. A new cause of warmth or cold (the biomes'
/// climate and its lapse over altitude, a cave's pull, a biome's base) is a new <see cref="ITemperatureTerm"/> class and one more line in <see cref="Standard"/> (or a
/// <see cref="PlaceTemperature.With"/> where the world is wired); <see cref="PlaceTemperature"/> and the terms already here stay as they are.
/// </summary>
public static class TemperatureTerms
{
    /// <summary>The terms the game starts with, made from the data's numbers: base, day, weather, shelter, fire.</summary>
    /// <param name="data">The temperature's data.</param>
    public static IReadOnlyList<ITemperatureTerm> Standard(TemperatureData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return
        [
            new BaseTemperatureTerm(data.BaseTenths),
            new DayTemperatureTerm(data.DayAmplitudeTenths),
            new WeatherTemperatureTerm(data.CloudCoolingTenths, data.PrecipitationCoolingTenths, data.WindCoolingTenths),
            new ShelterTemperatureTerm(data.ShelterBonusTenths, data.ShelterOnlyInPrecipitation),
            new FireTemperatureTerm(data.FireRadiusBlocks, data.FireBonusTenths),
        ];
    }
}
