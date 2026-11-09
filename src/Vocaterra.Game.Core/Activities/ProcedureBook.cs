using System.Globalization;
using System.Text.Json;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Core.Activities;

/// <summary>The bonus of a procedure's yield: one more of the item when enough steps were right the first time (design 10.7).</summary>
/// <param name="FirstTimeSteps">The steps that must be right first time.</param>
/// <param name="Count">How many more of the item the bonus gives.</param>
public sealed record ProcedureBonus(int FirstTimeSteps, int Count);

/// <summary>What a procedure makes: an item, how many, and the bonus when the steps go well.</summary>
/// <param name="Item">The item's name (ItemDefinition.Name; the nouns schema's item kinds).</param>
/// <param name="Count">How many one run makes.</param>
/// <param name="Bonus">The bonus, or <see langword="null"/> for none.</param>
public sealed record ProcedureMakes(string Item, int Count, ProcedureBonus? Bonus);

/// <summary>An item a procedure uses up.</summary>
/// <param name="Item">The item's name.</param>
/// <param name="Count">How many one run uses.</param>
/// <param name="Leaves">The item the container is left as when it is used up (water-bottle leaves bottle), or <see langword="null"/>.</param>
public sealed record ProcedureUse(string Item, int Count, string? Leaves);

/// <summary>
/// One step of a procedure (design 10.7). Its instruction is the does row <see cref="Row"/>; its verb <see cref="Verb"/> is the lemma the learner says.
/// The act is a pair (<see cref="ActUse"/>, <see cref="ActOn"/>) that <see cref="ActionTable"/> lists the verbs of: with <c>where</c> or <c>from</c> the
/// learner acts on <c>what</c> with what they hold (<c>use</c>, default <c>what</c>) and it goes to or comes from the fixture; with <c>with</c> the learner
/// acts on the fixture <c>what</c> with the tool <c>with</c>.
/// </summary>
/// <param name="Id">The step's id, unique in its procedure.</param>
/// <param name="Row">The does row the step's instruction is.</param>
/// <param name="Verb">The verb lemma (<c>poner</c>).</param>
/// <param name="What">The noun acted on.</param>
/// <param name="Use">The noun the learner acts with, when it is not the default (mano for sacar), or <see langword="null"/>.</param>
/// <param name="Where">The fixture the thing goes to, or <see langword="null"/>.</param>
/// <param name="From">The fixture the thing comes from, or <see langword="null"/>.</param>
/// <param name="With">The tool the act is done with, or <see langword="null"/>.</param>
/// <param name="AnyOrder">The order group: steps with the same group may come in either order (drawn per bake), or <see langword="null"/>.</param>
public sealed record ProcedureStep(string Id, string Row, string Verb, string What, string? Use, string? Where, string? From, string? With, string? AnyOrder)
{
    /// <summary>The noun the learner acts with (the pair's use).</summary>
    public string ActUse => With is null ? Use ?? What : Use ?? With;

    /// <summary>The fixture the act is done on (the pair's on).</summary>
    public string ActOn => With is null ? (Where ?? From)! : What;
}

/// <summary>A run of consecutive steps taught with one does row of its own (<c>haz-la-masa</c>).</summary>
/// <param name="Id">The chunk's id.</param>
/// <param name="Row">The does row the chunk's instruction is.</param>
/// <param name="Steps">The step ids, contiguous and in the procedure's order.</param>
public sealed record ProcedureChunk(string Id, string Row, IReadOnlyList<string> Steps);

/// <summary>The procedure's goal: the does row of the whole job (<c>haz-pan</c>).</summary>
/// <param name="Row">The does row.</param>
public sealed record ProcedureGoal(string Row);

/// <summary>
/// One procedure (W05, design 10.7; <c>game/procedures/&lt;id&gt;.json</c>): a station, the NPC who teaches it, its level, what it makes and uses,
/// its steps, its chunks and its goal. The file's words are ids and nouns; its text is the does rows' (the phrase sheet and the locale).
/// </summary>
/// <param name="Id">The procedure's id, its file's name.</param>
/// <param name="Station">The station it is done at (<see cref="StationBook"/>).</param>
/// <param name="Teacher">The NPC who teaches it.</param>
/// <param name="Level">The level its Spanish keeps to (<see cref="LessonLevels.All"/>).</param>
/// <param name="Makes">What it makes.</param>
/// <param name="Uses">What it uses up.</param>
/// <param name="Steps">The steps, in order.</param>
/// <param name="Chunks">The chunks, in order.</param>
/// <param name="Goal">The goal.</param>
public sealed record ProcedureDefinition(
    string Id,
    string Station,
    string Teacher,
    string Level,
    ProcedureMakes Makes,
    IReadOnlyList<ProcedureUse> Uses,
    IReadOnlyList<ProcedureStep> Steps,
    IReadOnlyList<ProcedureChunk> Chunks,
    ProcedureGoal Goal)
{
    /// <summary>The does rows the procedure names: its steps', its chunks' and its goal's, in that order, each once.</summary>
    public IReadOnlyList<string> Rows =>
        Steps.Select(static s => s.Row).Concat(Chunks.Select(static c => c.Row)).Append(Goal.Row).Distinct(StringComparer.Ordinal).ToList();
}

/// <summary>
/// The procedures of the game (W05), read from the bundle's host file <c>game/procedures.json</c> (an index of ids, like <c>lessons.json</c>) and one
/// file per procedure, <c>game/procedures/&lt;id&gt;.json</c>, of schema 1. The book holds plain values; the checks between it and the other files
/// (the stations, the actions, the nouns, the phrase sheet, the speech index, the level lists) are <see cref="Content.GameDataValidator"/>'s.
/// </summary>
public sealed class ProcedureBook
{
    /// <summary>The index's path inside the bundle folder.</summary>
    public const string RelativePath = "game/procedures.json";

    /// <summary>The folder of the procedure files inside the bundle folder: <c>game/procedures/&lt;id&gt;.json</c>.</summary>
    public const string Folder = "game/procedures/";

    /// <summary>The schema version of the index and of each procedure file.</summary>
    public const int SchemaVersion = 1;

    private readonly List<ProcedureDefinition> _procedures;

    private ProcedureBook(List<ProcedureDefinition> procedures) => _procedures = procedures;

    /// <summary>The procedures, in the index's order.</summary>
    public IReadOnlyList<ProcedureDefinition> Procedures => _procedures;

    /// <summary>The book of a bundle with no procedure.</summary>
    public static ProcedureBook Empty { get; } = new([]);

    /// <summary>The procedure with this id, or <see langword="null"/>.</summary>
    /// <param name="id">The procedure's id.</param>
    public ProcedureDefinition? Find(string id) => _procedures.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.Ordinal));

    /// <summary>The procedures done at a station, in the index's order.</summary>
    /// <param name="station">The station's id.</param>
    public IReadOnlyList<ProcedureDefinition> AtStation(string station) => _procedures.Where(p => string.Equals(p.Station, station, StringComparison.Ordinal)).ToList();

    /// <summary>The procedure an NPC teaches (W08: the first whose teacher it is), or <see langword="null"/> when it teaches none.</summary>
    /// <param name="npc">The NPC's id.</param>
    public ProcedureDefinition? TaughtBy(string npc) => _procedures.FirstOrDefault(p => string.Equals(p.Teacher, npc, StringComparison.Ordinal));

    /// <summary>
    /// Reads the procedures from the bundle's verified host files: the index lists the ids, each file is read from the bytes the load hashed.
    /// </summary>
    /// <param name="bundle">The bundle, loaded with hash verification.</param>
    /// <exception cref="InvalidDataException">The index or a listed file is missing or malformed.</exception>
    public static ProcedureBook Load(ContentBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        var ids = ParseIndex(GameData.HostText(bundle, RelativePath));
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            files[id] = GameData.HostText(bundle, Folder + id + ".json");
        }

        return Parse(GameData.HostText(bundle, RelativePath), files);
    }

    /// <summary>
    /// The same book with every noun of its steps read through the noun catalogue (T03): the files name the things by neutral id (<c>flour</c>, <c>bowl</c>), and the
    /// learner is matched by, and shown, the target language's word for each (<see cref="NounCatalog.LemmaOfId"/>). An id the catalogue does not have stays as it is, for the validator.
    /// </summary>
    /// <param name="nouns">The noun catalogue.</param>
    public ProcedureBook InLanguage(NounCatalog nouns)
    {
        ArgumentNullException.ThrowIfNull(nouns);
        return new ProcedureBook(_procedures.Select(procedure => procedure with
        {
            Steps = procedure.Steps.Select(step => step with
            {
                What = nouns.LemmaOfId(step.What),
                Use = step.Use is null ? null : nouns.LemmaOfId(step.Use),
                Where = step.Where is null ? null : nouns.LemmaOfId(step.Where),
                From = step.From is null ? null : nouns.LemmaOfId(step.From),
                With = step.With is null ? null : nouns.LemmaOfId(step.With),
            }).ToList(),
        }).ToList());
    }

    /// <summary>The ids the index lists, in order.</summary>
    /// <param name="json">The index's text.</param>
    /// <exception cref="InvalidDataException">The index is malformed.</exception>
    public static IReadOnlyList<string> ParseIndex(string json)
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

            var ids = root.GetProperty("procedures").EnumerateArray().Select(static e => e.GetString() ?? throw new InvalidDataException($"{RelativePath} lists a procedure with no id.")).ToList();
            if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Count)
            {
                throw new InvalidDataException($"{RelativePath} lists a procedure twice.");
            }

            return ids;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new InvalidDataException($"{RelativePath} is malformed: {ex.Message}", ex);
        }
    }

    /// <summary>Builds the book from the index's text and the procedure files' texts, keyed by id.</summary>
    /// <param name="index">The index's text (<see cref="ParseIndex"/>).</param>
    /// <param name="files">Each listed id's procedure file text.</param>
    /// <exception cref="InvalidDataException">The index, a file is missing, or a file is malformed or names another id.</exception>
    public static ProcedureBook Parse(string index, IReadOnlyDictionary<string, string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var procedures = new List<ProcedureDefinition>();
        foreach (var id in ParseIndex(index))
        {
            if (!files.TryGetValue(id, out var text))
            {
                throw new InvalidDataException($"{Folder}{id}.json is listed in {RelativePath} but is missing from the bundle.");
            }

            var procedure = ParseProcedure(text, id);
            procedures.Add(procedure);
        }

        return new ProcedureBook(procedures);
    }

    /// <summary>Builds one procedure from its file's text.</summary>
    /// <param name="json">The file's text.</param>
    /// <param name="expectedId">The id the file must carry (its file's name), or <see langword="null"/> to take the file's own.</param>
    /// <exception cref="InvalidDataException">The text is malformed, a step names no or two targets, or the id is not the expected one.</exception>
    public static ProcedureDefinition ParseProcedure(string json, string? expectedId = null)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var id = root.GetProperty("id").GetString()!;
            var file = Folder + id + ".json";
            if (expectedId is not null && !string.Equals(id, expectedId, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"{Folder}{expectedId}.json: the procedure's id must be its file's name, '{expectedId}'.");
            }

            if (root.GetProperty("schemaVersion").GetInt32() != SchemaVersion)
            {
                throw new InvalidDataException($"{file} must have schemaVersion {SchemaVersion}.");
            }

            var level = root.GetProperty("level").GetString()!;
            if (!LessonLevels.All.Contains(level, StringComparer.Ordinal))
            {
                throw new InvalidDataException($"{file}: the level '{level}' is not one of {string.Join(", ", LessonLevels.All)}.");
            }

            var makes = ParseMakes(root.GetProperty("makes"), file);
            var uses = root.GetProperty("uses").EnumerateArray().Select(e => new ProcedureUse(
                e.GetProperty("item").GetString()!,
                Count(e.GetProperty("count"), file),
                Optional(e, "leaves"))).ToList();
            var steps = root.GetProperty("steps").EnumerateArray().Select(e => ParseStep(e, file)).ToList();
            var chunks = root.GetProperty("chunks").EnumerateArray().Select(e => new ProcedureChunk(
                e.GetProperty("id").GetString()!,
                e.GetProperty("row").GetString()!,
                e.GetProperty("steps").EnumerateArray().Select(s => s.GetString()!).ToList())).ToList();
            var goal = new ProcedureGoal(root.GetProperty("goal").GetProperty("row").GetString()!);
            return new ProcedureDefinition(
                id,
                root.GetProperty("station").GetString()!,
                root.GetProperty("teacher").GetString()!,
                level,
                makes,
                uses,
                steps,
                chunks,
                goal);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException($"A procedure file is malformed: {ex.Message}", ex);
        }
    }

    private static ProcedureMakes ParseMakes(JsonElement makes, string file)
    {
        ProcedureBonus? bonus = null;
        if (makes.TryGetProperty("bonus", out var b))
        {
            bonus = new ProcedureBonus(Count(b.GetProperty("firstTimeSteps"), file), Count(b.GetProperty("count"), file));
        }

        return new ProcedureMakes(makes.GetProperty("item").GetString()!, Count(makes.GetProperty("count"), file), bonus);
    }

    private static ProcedureStep ParseStep(JsonElement element, string file)
    {
        var id = element.GetProperty("id").GetString()!;
        var where = Optional(element, "where");
        var from = Optional(element, "from");
        var with = Optional(element, "with");
        var targets = new[] { where, from, with }.Count(static t => t is not null);
        if (targets != 1)
        {
            throw new InvalidDataException($"{file}: the step '{id}' must name exactly one of where, from and with (it names {targets}).");
        }

        return new ProcedureStep(
            id,
            element.GetProperty("row").GetString()!,
            element.GetProperty("do").GetString()!,
            element.GetProperty("what").GetString()!,
            Optional(element, "use"),
            where,
            from,
            with,
            Optional(element, "anyOrder"));
    }

    private static int Count(JsonElement value, string file)
    {
        var count = value.GetInt32();
        return count > 0 ? count : throw new InvalidDataException(string.Create(CultureInfo.InvariantCulture, $"{file}: a count ({count}) is not positive."));
    }

    private static string? Optional(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
