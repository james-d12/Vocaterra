namespace Vocaterra.Game.Core.Temperature;

/// <summary>
/// The checks of <c>game/temperature.json</c> that need more than its shape (I06): the numbers are inside the limits (so a sum cannot overflow), the cooling and warming numbers
/// are not negative, the fire has a radius, and the bands are ordered by their bounds with walk factors above 0 and at most 1, ids that are unique, chills that never fall as the
/// bands get colder, and a line only where there is a chill. <see cref="Content.GameDataValidator"/> runs it with the other files' checks.
/// </summary>
internal static class TemperatureValidator
{
    private const string File = TemperatureData.RelativePath;
    private const int Limit = 2000; // tenths: 200 degrees, far outside any real number and small enough that no sum overflows

    /// <summary>Every problem of the numbers and the bands, in a fixed order; empty when they hold together.</summary>
    /// <param name="data">The temperature's data.</param>
    public static IReadOnlyList<string> Validate(TemperatureData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var problems = new List<string>();
        Within(problems, "base.tenths", data.BaseTenths, -Limit, Limit);
        Within(problems, "day.amplitudeTenths", data.DayAmplitudeTenths, 0, Limit);
        Within(problems, "weather.cloudTenths", data.CloudCoolingTenths, 0, Limit);
        Within(problems, "weather.precipitationTenths", data.PrecipitationCoolingTenths, 0, Limit);
        Within(problems, "weather.windTenths", data.WindCoolingTenths, 0, Limit);
        Within(problems, "climate.referenceThousandths", data.Climate.ReferenceThousandths, -2000, 2000);
        Within(problems, "climate.coolingTenthsPerRange", data.Climate.CoolingTenthsPerRange, 0, Limit);
        Within(problems, "climate.maxCoolingTenths", data.Climate.MaxCoolingTenths, 0, Limit);
        Within(problems, "shelter.bonusTenths", data.ShelterBonusTenths, 0, Limit);
        Within(problems, "fire.bonusTenths", data.FireBonusTenths, 0, Limit);
        Within(problems, "fire.radiusBlocks", data.FireRadiusBlocks, 1, 64);
        CheckBands(problems, data.Bands);
        return problems;
    }

    private static void CheckBands(List<string> problems, IReadOnlyList<TemperatureBand> bands)
    {
        if (bands.Count == 0)
        {
            problems.Add($"{File}: bands needs at least one band.");
            return;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < bands.Count; i++)
        {
            var band = bands[i];
            var name = $"the band '{band.Id}'";
            if (string.IsNullOrWhiteSpace(band.Id))
            {
                problems.Add($"{File}: band {i + 1} has no id.");
            }
            else if (!ids.Add(band.Id))
            {
                problems.Add($"{File}: the band id '{band.Id}' is used twice.");
            }

            Within(problems, $"{name}: from", band.FromTenths, -Limit, Limit);
            if (!double.IsFinite(band.WalkFactor) || band.WalkFactor <= 0 || band.WalkFactor > 1)
            {
                problems.Add($"{File}: {name} has a walkFactor of {band.WalkFactor}; it must be above 0 and at most 1.");
            }

            if (band.Chill < 0)
            {
                problems.Add($"{File}: {name} has a chill of {band.Chill}; it must be 0 or more.");
            }

            if (band.Line is not null && band.Chill <= 0)
            {
                problems.Add($"{File}: {name} has a line but no chill; a line is said only on entering a chilled band.");
            }

            if (i > 0 && band.FromTenths >= bands[i - 1].FromTenths)
            {
                problems.Add($"{File}: {name} begins at {band.FromTenths}, which is not below the band before it ({bands[i - 1].FromTenths}); the bands must run from the warmest to the coldest.");
            }

            if (i > 0 && band.Chill < bands[i - 1].Chill)
            {
                problems.Add($"{File}: {name} has a chill of {band.Chill}, less than the warmer band before it ({bands[i - 1].Chill}); a colder band cannot be less chilled.");
            }
        }
    }

    private static void Within(List<string> problems, string name, int value, int minimum, int maximum)
    {
        if (value < minimum || value > maximum)
        {
            problems.Add($"{File}: {name} is {value}; it must be from {minimum} to {maximum}.");
        }
    }
}
