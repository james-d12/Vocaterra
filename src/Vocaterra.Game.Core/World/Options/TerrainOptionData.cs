using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using Vocaterra.Game.Core.Content;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Core.World.Options;

/// <summary>One thing an option does to the generator: it puts a target's numbers (a path in <c>game/world.json</c>) through a mode.</summary>
/// <param name="Target">The target's name (one of the code's <see cref="TerrainTarget"/>s).</param>
/// <param name="Mode">How the option's value is laid over the target.</param>
public sealed record TerrainEffect(string Target, TerrainMode Mode);

/// <summary>
/// One terrain option (a row of <c>game/world-options.json</c>, Q02): a slider the New game page offers. The values are whole numbers on a grid (<see cref="Min"/> to <see cref="Max"/> in steps of
/// <see cref="Step"/>), <see cref="Default"/> is today's world, and <see cref="Effects"/> says which generator values the option scales, offsets or sets.
/// </summary>
/// <param name="Id">The option's id (lower-case letters and digits, <c>mountains</c>), as the command line and the save name it.</param>
/// <param name="Label">The English label of the slider.</param>
/// <param name="Hint">A line of English that says what the option does, or empty.</param>
/// <param name="Unit">The unit shown after the value (<c>%</c>), or empty; ignored when <paramref name="Labels"/> names every stop.</param>
/// <param name="Min">The lowest value.</param>
/// <param name="Max">The highest value.</param>
/// <param name="Step">The distance between two stops.</param>
/// <param name="Default">The value that leaves the world as the shipped files describe it.</param>
/// <param name="Labels">An English name for each stop from the lowest (so <c>(Max - Min) / Step + 1</c> of them), or empty to show the value.</param>
/// <param name="Effects">The generator values the option changes, in the order they are applied.</param>
public sealed record TerrainOption(string Id, string Label, string Hint, string Unit, int Min, int Max, int Step, int Default, IReadOnlyList<string> Labels, IReadOnlyList<TerrainEffect> Effects)
{
    /// <summary>How many stops the slider has.</summary>
    public int Stops => ((Max - Min) / Step) + 1;

    /// <summary>Whether a value is a stop of the slider.</summary>
    /// <param name="value">The value.</param>
    public bool OnGrid(int value) => value >= Min && value <= Max && (value - Min) % Step == 0;

    /// <summary>The text of a stop for the slider: its label when there are labels, else the value and the unit.</summary>
    /// <param name="value">A value on the grid.</param>
    public string Show(int value) =>
        Labels.Count > 0 && OnGrid(value) ? Labels[(value - Min) / Step] : string.Create(CultureInfo.InvariantCulture, $"{value}{Unit}");
}

/// <summary>
/// The terrain options of the New game page (<c>game/world-options.json</c>, Q02, D-game-37): the rows, each a slider over a few of <c>game/world.json</c>'s generator values with today's values as its default.
/// <see cref="Create"/> turns the player's choices into a <see cref="WorldOptions"/> and <see cref="Apply"/> lays one over the world's numbers, so the generator, the far store's cache and the save all carry it;
/// <see cref="TerrainOptionValidator"/> checks every row, and that every value on every grid still gives a world the generator accepts (the valley, a region the generator never changes, still fits: no option
/// names the vertical range, the sea level or the regions' blend, and the land round the valley is biased towards land by numbers no option moves).
/// </summary>
/// <param name="Options">The rows, in the order the page shows them.</param>
public sealed record TerrainOptionData(IReadOnlyList<TerrainOption> Options)
{
    /// <summary>The file's path inside the bundle folder.</summary>
    public const string RelativePath = "game/world-options.json";

    /// <summary>The file's format.</summary>
    public const int SchemaVersion = 1;

    /// <summary>The row of an option, or <see langword="null"/> when there is none of that id.</summary>
    /// <param name="id">The option's id.</param>
    public TerrainOption? Find(string id)
    {
        foreach (var option in Options)
        {
            if (string.Equals(option.Id, id, StringComparison.Ordinal))
            {
                return option;
            }
        }

        return null;
    }

    /// <summary>Reads the rows from the bundle's verified host file.</summary>
    /// <param name="bundle">The bundle, loaded with hash verification.</param>
    /// <exception cref="InvalidDataException">The bundle has no such host file, or the file is malformed.</exception>
    public static TerrainOptionData Load(ContentBundle bundle) => Parse(GameData.HostText(bundle, RelativePath));

    /// <summary>Builds the rows from the file's text. The shape is checked here; the values are <see cref="TerrainOptionValidator"/>'s.</summary>
    /// <param name="json">The contents of <c>game/world-options.json</c>.</param>
    /// <exception cref="InvalidDataException">The text is malformed or a part is missing.</exception>
    public static TerrainOptionData Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.GetProperty("schemaVersion").GetInt32() != SchemaVersion)
            {
                throw new InvalidDataException($"{RelativePath} must have schemaVersion {SchemaVersion}.");
            }

            var rows = new List<TerrainOption>();
            foreach (var row in root.GetProperty("options").EnumerateArray())
            {
                rows.Add(new TerrainOption(
                    row.GetProperty("id").GetString() ?? string.Empty,
                    row.GetProperty("label").GetString() ?? string.Empty,
                    row.TryGetProperty("hint", out var hint) ? hint.GetString() ?? string.Empty : string.Empty,
                    row.TryGetProperty("unit", out var unit) ? unit.GetString() ?? string.Empty : string.Empty,
                    row.GetProperty("min").GetInt32(),
                    row.GetProperty("max").GetInt32(),
                    row.GetProperty("step").GetInt32(),
                    row.GetProperty("default").GetInt32(),
                    row.TryGetProperty("labels", out var labels) ? [.. labels.EnumerateArray().Select(static l => l.GetString() ?? string.Empty)] : [],
                    [.. row.GetProperty("effects").EnumerateArray().Select(static e => new TerrainEffect(e.GetProperty("target").GetString() ?? string.Empty, ParseMode(e.GetProperty("mode").GetString() ?? string.Empty)))]));
            }

            return new TerrainOptionData(rows);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException($"{RelativePath} is malformed: {ex.Message}", ex);
        }
    }

    private static TerrainMode ParseMode(string name) => name switch
    {
        "scale" => TerrainMode.Scale,
        "scaleInverse" => TerrainMode.ScaleInverse,
        "offset" => TerrainMode.Offset,
        "set" => TerrainMode.Set,
        _ => throw new FormatException($"the mode '{name}' is not scale, scaleInverse, offset or set"),
    };

    /// <summary>The name of a mode as the file writes it.</summary>
    /// <param name="mode">The mode.</param>
    public static string ModeName(TerrainMode mode) => mode switch
    {
        TerrainMode.Scale => "scale",
        TerrainMode.ScaleInverse => "scaleInverse",
        TerrainMode.Offset => "offset",
        _ => "set",
    };

    /// <summary>The options for some chosen values: an option at its default is left out, so the same world is the same <see cref="WorldOptions"/>.</summary>
    /// <param name="values">The option ids and their values.</param>
    /// <param name="options">The options.</param>
    /// <param name="error">Why the values cannot be options: an id the content lacks, an id given twice or a value off the option's grid; empty when they can.</param>
    /// <returns>Whether the values are valid options.</returns>
    public bool TryCreate(IEnumerable<KeyValuePair<string, int>> values, [NotNullWhen(true)] out WorldOptions? options, out string error)
    {
        ArgumentNullException.ThrowIfNull(values);
        var chosen = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (id, value) in values)
        {
            if (Find(id) is not { } row)
            {
                options = null;
                error = $"there is no terrain option '{id}' (the options are {string.Join(", ", Options.Select(static o => o.Id))}).";
                return false;
            }

            if (!row.OnGrid(value))
            {
                options = null;
                error = string.Create(CultureInfo.InvariantCulture, $"the terrain option '{id}' takes a value from {row.Min} to {row.Max} in steps of {row.Step} ({row.Default} is the default); {value} is not one.");
                return false;
            }

            if (!chosen.TryAdd(id, value))
            {
                options = null;
                error = $"the terrain option '{id}' is given twice.";
                return false;
            }
        }

        options = WorldOptions.FromNormalised(chosen.Where(pair => pair.Value != Find(pair.Key)!.Default));
        error = string.Empty;
        return true;
    }

    /// <summary>The options for some chosen values (see <see cref="TryCreate"/>).</summary>
    /// <param name="values">The option ids and their values.</param>
    /// <exception cref="ArgumentException">An id the content lacks, an id twice or a value off the grid.</exception>
    public WorldOptions Create(IEnumerable<KeyValuePair<string, int>> values) =>
        TryCreate(values, out var options, out var error) ? options : throw new ArgumentException(error, nameof(values));

    /// <summary>Parses <c>id=value,id=value</c> (the <c>--terrain</c> option's text) into options.</summary>
    /// <param name="text">The text.</param>
    /// <param name="options">The options.</param>
    /// <param name="error">Why the text is not options; empty when it is.</param>
    /// <returns>Whether the text is valid options.</returns>
    public bool TryParse(string text, [NotNullWhen(true)] out WorldOptions? options, out string error)
    {
        ArgumentNullException.ThrowIfNull(text);
        var pairs = new List<KeyValuePair<string, int>>();
        foreach (var part in text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var at = part.IndexOf('=', StringComparison.Ordinal);
            if (at <= 0 || !int.TryParse(part.AsSpan(at + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
            {
                options = null;
                error = $"'{part}' is not <id>=<whole number>.";
                return false;
            }

            pairs.Add(new KeyValuePair<string, int>(part[..at].Trim(), value));
        }

        return TryCreate(pairs, out options, out error);
    }

    /// <summary>The world's numbers with the options laid over them: each option's effects, in the order of the rows. The defaults give back the very same data.</summary>
    /// <param name="world">The shipped numbers (<c>game/world.json</c>).</param>
    /// <param name="options">The options (made by <see cref="Create"/>).</param>
    /// <exception cref="ArgumentException">An option names an id this table lacks.</exception>
    public WorldGenData Apply(WorldGenData world, WorldOptions options)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(options);
        var result = world;
        foreach (var (id, value) in options.Entries)
        {
            result = ApplyOne(result, Find(id) ?? throw new ArgumentException($"there is no terrain option '{id}'.", nameof(options)), value);
        }

        return result;
    }

    /// <summary>The numbers with one option at a value (also the default's, which changes nothing: the validator and the tests check it).</summary>
    /// <param name="world">The numbers.</param>
    /// <param name="option">The row.</param>
    /// <param name="value">A value on its grid.</param>
    internal static WorldGenData ApplyOne(WorldGenData world, TerrainOption option, int value)
    {
        var result = world;
        foreach (var effect in option.Effects)
        {
            result = TerrainTarget.All[effect.Target].Map(result, TerrainTarget.Function(effect.Mode, value));
        }

        return result;
    }
}
