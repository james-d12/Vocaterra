namespace Vocaterra.Game.Core.Temperature;

/// <summary>
/// The terms the game knows (I06c): a term id and the factory that builds it from its row's parameters. <see cref="Standard"/> is the game's; a future system adds one
/// <see cref="ITemperatureTerm"/> class and one line here (<see cref="With"/> makes a registry with one more, as the tests do). The data lists which terms run:
/// an id with no factory is a validation error, not a silent skip.
/// </summary>
public sealed class TemperatureTermRegistry
{
    private readonly IReadOnlyDictionary<string, Func<TermParameters, ITemperatureTerm>> _factories;

    private TemperatureTermRegistry(IReadOnlyDictionary<string, Func<TermParameters, ITemperatureTerm>> factories) => _factories = factories;

    /// <summary>The game's terms: the climate's baseline, the day's swing, the weather, a roof's shelter and a fire's warmth.</summary>
    public static TemperatureTermRegistry Standard { get; } = new(new Dictionary<string, Func<TermParameters, ITemperatureTerm>>(StringComparer.Ordinal)
    {
        [ClimateTemperatureTerm.Id] = ClimateTemperatureTerm.From,
        [DayTemperatureTerm.Id] = DayTemperatureTerm.From,
        [WeatherTemperatureTerm.Id] = WeatherTemperatureTerm.From,
        [ShelterTemperatureTerm.Id] = ShelterTemperatureTerm.From,
        [FireTemperatureTerm.Id] = FireTemperatureTerm.From,
    });

    /// <summary>The ids this registry builds.</summary>
    public IEnumerable<string> Ids => _factories.Keys;

    /// <summary>A registry with one more term (or this id's factory replaced).</summary>
    /// <param name="id">The term's id.</param>
    /// <param name="factory">What builds it from its row.</param>
    public TemperatureTermRegistry With(string id, Func<TermParameters, ITemperatureTerm> factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(factory);
        return new TemperatureTermRegistry(new Dictionary<string, Func<TermParameters, ITemperatureTerm>>(_factories, StringComparer.Ordinal) { [id] = factory });
    }

    /// <summary>Builds the term of a row.</summary>
    /// <param name="spec">The row.</param>
    /// <exception cref="InvalidDataException">The id is unknown or a parameter is missing, out of range or not known.</exception>
    public ITemperatureTerm Create(TemperatureTermSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        if (!_factories.TryGetValue(spec.Id, out var factory))
        {
            throw new InvalidDataException($"the term '{spec.Id}' is not one the game knows ({string.Join(", ", _factories.Keys.Order(StringComparer.Ordinal))}).");
        }

        var parameters = new TermParameters(spec);
        var term = factory(parameters);
        parameters.Done();
        return term;
    }

    /// <summary>Builds the terms of the rows, in order.</summary>
    /// <param name="specs">The rows.</param>
    public IReadOnlyList<ITemperatureTerm> Build(IEnumerable<TemperatureTermSpec> specs)
    {
        ArgumentNullException.ThrowIfNull(specs);
        return [.. specs.Select(Create)];
    }

    /// <summary>What is wrong with the rows: an unknown id, a bad parameter, an id used twice.</summary>
    /// <param name="specs">The rows.</param>
    public IReadOnlyList<string> Problems(IEnumerable<TemperatureTermSpec> specs)
    {
        ArgumentNullException.ThrowIfNull(specs);
        var problems = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var spec in specs)
        {
            if (!seen.Add(spec.Id))
            {
                problems.Add($"{TemperatureData.RelativePath}: the term '{spec.Id}' is listed twice.");
                continue;
            }

            try
            {
                _ = Create(spec);
            }
            catch (InvalidDataException ex)
            {
                problems.Add($"{TemperatureData.RelativePath}: {ex.Message}");
            }
        }

        return problems;
    }
}
