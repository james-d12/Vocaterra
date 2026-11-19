namespace Vocaterra.Game.Core.Temperature;

/// <summary>
/// The checks of <c>game/temperature.json</c> that need more than its shape (I06): the terms are known ones with their parameters in range (the registry builds each), the ground, the lag and the
/// biomes' adjustments are inside limits and name real biomes, and the bands are ordered by their bounds with walk factors above 0 and at most 1, ids that are unique, chills that never fall as the
/// bands get colder, heats that never rise as the bands get colder, a chill or a heat but not both, thirst factors above 0, and a line only where there is a chill or a heat. <see cref="Content.GameDataValidator"/> runs it with the other files' checks.
/// </summary>
internal static class TemperatureValidator
{
    private const string File = TemperatureData.RelativePath;
    private const int Limit = 2000; // tenths: 200 degrees, far outside any real number and small enough that no sum overflows

    /// <summary>Every problem of the numbers and the bands, in a fixed order; empty when they hold together.</summary>
    /// <param name="data">The temperature's data.</param>
    /// <param name="biomeIds">The ids of biomes.json's rows, to check the biome adjustments against (null skips the check).</param>
    /// <param name="registry">The registry the terms are checked against (null for the game's).</param>
    public static IReadOnlyList<string> Validate(TemperatureData data, IEnumerable<string>? biomeIds = null, TemperatureTermRegistry? registry = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        var problems = new List<string>();
        problems.AddRange((registry ?? TemperatureTermRegistry.Standard).Problems(data.Terms));
        var environment = data.Environment;
        Within(problems, "ground.tenths", environment.GroundTenths, -Limit, Limit);
        Within(problems, "ground.shallowBlocks", environment.ShallowBlocks, 0, 64);
        Within(problems, "ground.fadeBlocks", environment.FadeBlocks, 1, 256);
        Within(problems, "lag.secondsToSettle", data.Lag.SecondsToSettle, 0, 600);
        var known = biomeIds is null ? null : new HashSet<string>(biomeIds, StringComparer.Ordinal);
        foreach (var (id, effect) in environment.Biomes)
        {
            if (known is not null && !known.Contains(id))
            {
                problems.Add($"{File}: biomes names '{id}', which is not a row of biomes.json.");
            }

            Within(problems, $"biomes.{id}.offsetTenths", effect.OffsetTenths, -Limit, Limit);
            Within(problems, $"biomes.{id}.daySwingPermille", effect.DaySwingPermille, 0, 4000);
            Within(problems, $"biomes.{id}.weatherPermille", effect.WeatherPermille, 0, 4000);
        }

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

            if (band.Heat < 0)
            {
                problems.Add($"{File}: {name} has a heat of {band.Heat}; it must be 0 or more.");
            }

            if (band.Chill > 0 && band.Heat > 0)
            {
                problems.Add($"{File}: {name} has both a chill and a heat; a band is one or the other.");
            }

            if (!double.IsFinite(band.ThirstFactor) || band.ThirstFactor <= 0 || band.ThirstFactor > 8)
            {
                problems.Add($"{File}: {name} has a thirstFactor of {band.ThirstFactor}; it must be above 0 and at most 8.");
            }

            if (band.Line is not null && band.Chill <= 0 && band.Heat <= 0)
            {
                problems.Add($"{File}: {name} has a line but no chill or heat; a line is said only on entering a chilled or heated band.");
            }

            if (i > 0 && band.FromTenths >= bands[i - 1].FromTenths)
            {
                problems.Add($"{File}: {name} begins at {band.FromTenths}, which is not below the band before it ({bands[i - 1].FromTenths}); the bands must run from the warmest to the coldest.");
            }

            if (i > 0 && band.Heat > bands[i - 1].Heat)
            {
                problems.Add($"{File}: {name} has a heat of {band.Heat}, more than the warmer band before it ({bands[i - 1].Heat}); a colder band cannot be hotter.");
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
