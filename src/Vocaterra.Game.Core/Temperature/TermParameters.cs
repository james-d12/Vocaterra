using System.Text.Json;

namespace Vocaterra.Game.Core.Temperature;

/// <summary>
/// A term row's parameters, read by a term's factory (I06c): each read names the parameter, checks its range and says which row and name are wrong; <see cref="Done"/> refuses a parameter
/// nobody read (a misspelling). A fault is an <see cref="InvalidDataException"/> the validator reports.
/// </summary>
public sealed class TermParameters
{
    private readonly JsonElement _row;
    private readonly HashSet<string> _read = new(StringComparer.Ordinal) { "id" };

    /// <summary>The parameters of a row.</summary>
    /// <param name="spec">The row.</param>
    public TermParameters(TemperatureTermSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        Id = spec.Id;
        _row = spec.Row;
    }

    /// <summary>The term's id.</summary>
    public string Id { get; }

    /// <summary>A whole number within limits.</summary>
    /// <param name="name">The parameter's name.</param>
    /// <param name="minimum">The least allowed.</param>
    /// <param name="maximum">The most allowed.</param>
    public int Int(string name, int minimum, int maximum)
    {
        var element = Take(name);
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out var value))
        {
            throw Fault($"{name} must be a whole number");
        }

        return value < minimum || value > maximum ? throw Fault($"{name} is {value}; it must be from {minimum} to {maximum}") : value;
    }

    /// <summary>A whole number within limits, or a default when the row has none.</summary>
    /// <param name="name">The parameter's name.</param>
    /// <param name="minimum">The least allowed.</param>
    /// <param name="maximum">The most allowed.</param>
    /// <param name="fallback">The value when the row does not name it.</param>
    public int Int(string name, int minimum, int maximum, int fallback) => _row.TryGetProperty(name, out _) ? Int(name, minimum, maximum) : fallback;

    /// <summary>A true or false.</summary>
    /// <param name="name">The parameter's name.</param>
    public bool Bool(string name)
    {
        var element = Take(name);
        return element.ValueKind is JsonValueKind.True or JsonValueKind.False ? element.GetBoolean() : throw Fault($"{name} must be true or false");
    }

    /// <summary>An array of objects of whole numbers, each with the named fields: the rows of a curve.</summary>
    /// <param name="name">The parameter's name.</param>
    /// <param name="first">The first field of a row.</param>
    /// <param name="second">The second field of a row.</param>
    /// <param name="minimum">The least a field may be.</param>
    /// <param name="maximum">The most a field may be.</param>
    public IReadOnlyList<(int First, int Second)> Pairs(string name, string first, string second, int minimum, int maximum)
    {
        var element = Take(name);
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw Fault($"{name} must be an array");
        }

        var list = new List<(int, int)>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty(first, out var a) || !item.TryGetProperty(second, out var b)
                || !a.TryGetInt32(out var x) || !b.TryGetInt32(out var y) || x < minimum || x > maximum || y < minimum || y > maximum)
            {
                throw Fault($"every row of {name} needs whole numbers {first} and {second} from {minimum} to {maximum}");
            }

            list.Add((x, y));
        }

        return list;
    }

    /// <summary>Refuses a parameter that no read named.</summary>
    public void Done()
    {
        foreach (var property in _row.EnumerateObject())
        {
            if (!_read.Contains(property.Name))
            {
                throw Fault($"it has a parameter '{property.Name}' the term does not know");
            }
        }
    }

    /// <summary>A fault of this row.</summary>
    /// <param name="what">What is wrong.</param>
    public InvalidDataException Fault(string what) => new($"the term '{Id}': {what}.");

    private JsonElement Take(string name)
    {
        _ = _read.Add(name);
        return _row.TryGetProperty(name, out var element) ? element : throw Fault($"{name} is missing");
    }
}
