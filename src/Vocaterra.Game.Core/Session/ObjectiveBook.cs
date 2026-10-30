using System.Text.Json;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Core.Session;

/// <summary>One step of an objective as the data gives it (CONTENT_PLAN 3.5, C07).</summary>
/// <param name="Id">The step's id inside its objective (<c>wood</c>); the save keys it <c>&lt;objective&gt;.&lt;step&gt;</c>.</param>
/// <param name="Noun">For a checklist step, the catalogue noun it is about (<c>madera</c>); otherwise empty.</param>
/// <param name="Label">The word the checklist shows (<c>frutas</c>): the file's <c>label</c>, or the noun's lemma for a mass noun or a target of one, else its plural.</param>
/// <param name="Target">How many matching events make the step done.</param>
/// <param name="Trigger">The world event the step counts.</param>
/// <param name="Restore">The code hook that works the step out again from a loaded world (<see cref="ObjectiveBook.RestoreHooks"/>), or <see langword="null"/> for a step the save keeps.</param>
public sealed record ObjectiveStepSpec(string Id, string Noun, string Label, int Target, Trigger Trigger, string? Restore);

/// <summary>One objective as the data gives it (<c>game/objectives/&lt;id&gt;.json</c>, CONTENT_PLAN 3.5, C07).</summary>
/// <param name="Id">The objective's id, its file's name.</param>
/// <param name="Title">The target-language title of a checklist (<c>Campamento</c>), or <see langword="null"/> for an objective the HUD shows as one line (<c>objective.&lt;id&gt;.hud</c>).</param>
/// <param name="Steps">The steps, in checklist order.</param>
/// <param name="AlsoDoneBy">The objectives whose progress also completes this one (the guide counts as met once the camp checklist has moved).</param>
/// <param name="OnComplete">The code hooks run when the objective is completed (<see cref="ObjectiveBook.CompleteHooks"/>).</param>
public sealed record ObjectiveSpec(string Id, string? Title, IReadOnlyList<ObjectiveStepSpec> Steps, IReadOnlyList<string> AlsoDoneBy, IReadOnlyList<string> OnComplete)
{
    /// <summary>Whether the HUD shows the objective as a checklist of its steps (it has a title).</summary>
    public bool IsChecklist => Title is not null;
}

/// <summary>
/// The session's objectives (CONTENT_PLAN 3.5, C07): <c>game/session.json</c> gives their order, the dialogue pool of the closing line, the items the learner starts
/// with and the place the beacon starts on; each objective is its own file, <c>game/objectives/&lt;id&gt;.json</c>. The order is the HUD's: it gates nothing, and
/// every objective counts from the start. What the data cannot say is a named hook, and the hooks are code.
/// </summary>
public sealed class ObjectiveBook
{
    /// <summary>The session file inside the bundle folder.</summary>
    public const string SessionPath = "game/session.json";

    /// <summary>The folder of the objective files inside the bundle folder.</summary>
    public const string Folder = "game/objectives/";

    /// <summary>The restore hooks a step may name: <c>lesson-begun</c> (one once a lesson was begun), <c>scan-fire-ring</c> (one when a torch stands at the player's own ring).</summary>
    public static IReadOnlyList<string> RestoreHooks { get; } = ["lesson-begun", "scan-fire-ring"];

    /// <summary>The completion hooks an objective may name: <c>guide-met</c>, <c>spare-torch</c> and <c>point-own-camp</c>.</summary>
    public static IReadOnlyList<string> CompleteHooks { get; } = ["guide-met", "spare-torch", "point-own-camp"];

    private ObjectiveBook(IReadOnlyList<ObjectiveSpec> objectives, string closingPool, IReadOnlyList<ItemId> grants, string beaconStart)
    {
        Objectives = objectives;
        ClosingPool = closingPool;
        Grants = grants;
        BeaconStart = beaconStart;
    }

    /// <summary>The objectives, in the HUD's order.</summary>
    public IReadOnlyList<ObjectiveSpec> Objectives { get; }

    /// <summary>The dialogue pool of the guide's closing line, said when the session is complete (<c>guide.lesson.end</c>).</summary>
    public string ClosingPool { get; }

    /// <summary>The items the learner starts with, one of each (the book, S20).</summary>
    public IReadOnlyList<ItemId> Grants { get; }

    /// <summary>The scene point the beacon stands on at the start of a new game (<c>camp.guide</c>, S13).</summary>
    public string BeaconStart { get; }

    /// <summary>Reads the session and its objectives from the bundle's verified host files.</summary>
    /// <param name="bundle">The bundle, loaded with hash verification.</param>
    /// <param name="nouns">The noun catalogue (a checklist step's label comes from its noun).</param>
    /// <exception cref="InvalidDataException">A file is missing, malformed, not listed or names another id, or the session has no checklist.</exception>
    public static ObjectiveBook Load(ContentBundle bundle, NounCatalog nouns)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(nouns);
        try
        {
            using var document = JsonDocument.Parse(GameData.HostText(bundle, SessionPath));
            var root = document.RootElement;
            if (root.GetProperty("schemaVersion").GetInt32() != 1)
            {
                throw new InvalidDataException($"{SessionPath} must have schemaVersion 1.");
            }

            var ids = root.GetProperty("objectives").EnumerateArray().Select(static e => e.GetString() ?? string.Empty).ToList();
            if (ids.Count == 0 || ids.Distinct(StringComparer.Ordinal).Count() != ids.Count)
            {
                throw new InvalidDataException($"{SessionPath} must list each objective once, and at least one.");
            }

            var unlisted = bundle.HostFilePaths.Where(path => path.StartsWith(Folder, StringComparison.Ordinal) && !ids.Contains(path[Folder.Length..^".json".Length], StringComparer.Ordinal)).ToList();
            if (unlisted.Count > 0)
            {
                throw new InvalidDataException($"{SessionPath} does not list the objective file {string.Join(", ", unlisted)}.");
            }

            var objectives = ids.Select(id => Parse(Folder + id + ".json", GameData.HostText(bundle, Folder + id + ".json"), id, nouns)).ToList();
            if (!objectives.Any(static o => o.IsChecklist))
            {
                throw new InvalidDataException($"{SessionPath}: the session needs a checklist (an objective with a title).");
            }

            var grants = root.TryGetProperty("grants", out var grantsElement)
                ? grantsElement.EnumerateArray().Select(static e => ItemNamed(e.GetString() ?? string.Empty)).ToList()
                : [];
            return new ObjectiveBook(objectives, Text(root, "closingPool", SessionPath), grants, Text(root, "beaconStart", SessionPath));
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new InvalidDataException($"{SessionPath} is malformed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Builds one objective from its file's text: <c>{ "schemaVersion": 1, "id", "title"?, "steps": [ { "id", "noun"?, "label"?, "target", "trigger", "restore"? } ],
    /// "alsoDoneBy"?, "onComplete"? }</c>.
    /// </summary>
    /// <param name="path">The file's path, for the messages.</param>
    /// <param name="json">The file's text.</param>
    /// <param name="id">The id the file must have (its name).</param>
    /// <param name="nouns">The noun catalogue.</param>
    /// <exception cref="InvalidDataException">The text is malformed, the id is not the file's name, a checklist step names no catalogue noun, or a step id or target is wrong.</exception>
    public static ObjectiveSpec Parse(string path, string json, string id, NounCatalog nouns)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(nouns);
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.GetProperty("schemaVersion").GetInt32() != 1)
            {
                throw new InvalidDataException($"{path} must have schemaVersion 1.");
            }

            if (!string.Equals(Text(root, "id", path), id, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"{path}: the objective's id must be its file's name, '{id}'.");
            }

            var title = root.TryGetProperty("title", out _) ? Text(root, "title", path) : null;
            var steps = new List<ObjectiveStepSpec>();
            foreach (var element in root.GetProperty("steps").EnumerateArray())
            {
                var stepId = Text(element, "id", path);
                if (stepId.Contains('.', StringComparison.Ordinal) || steps.Any(s => string.Equals(s.Id, stepId, StringComparison.Ordinal)))
                {
                    throw new InvalidDataException($"{path}: the step id '{stepId}' is given twice or has a dot (the save keys a step '<objective>.<step>').");
                }

                var target = element.GetProperty("target").GetInt32();
                if (target < 1)
                {
                    throw new InvalidDataException($"{path}: the step '{stepId}' needs a target of at least 1.");
                }

                var trigger = Trigger.Parse(element.GetProperty("trigger"), WorldEvents.All, $"{path}: the step '{stepId}'");
                var restore = element.TryGetProperty("restore", out _) ? Text(element, "restore", path) : null;
                var noun = element.TryGetProperty("noun", out _) ? Text(element, "noun", path) : string.Empty;
                var label = element.TryGetProperty("label", out _) ? Text(element, "label", path) : null;
                if (title is not null)
                {
                    var entry = nouns.ForLemma(noun) ?? throw new InvalidDataException($"{path}: the checklist step '{stepId}' names '{noun}', which is not a catalogue noun.");
                    label ??= entry.Mass || target == 1 ? entry.Lemma : entry.Plural; // CONTENT_PLAN 3.5: the label rule reproduces the old checklist's words
                }

                steps.Add(new ObjectiveStepSpec(stepId, noun, label ?? stepId, target, trigger, restore));
            }

            if (steps.Count == 0)
            {
                throw new InvalidDataException($"{path}: the objective has no steps.");
            }

            return new ObjectiveSpec(id, title, steps, Strings(root, "alsoDoneBy"), Strings(root, "onComplete"));
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new InvalidDataException($"{path} is malformed: {ex.Message}", ex);
        }
    }

    /// <summary>A book of these objectives (the tests' own).</summary>
    /// <param name="objectives">The objectives, in order.</param>
    /// <param name="closingPool">The closing line's pool.</param>
    /// <param name="grants">The starting items.</param>
    /// <param name="beaconStart">The beacon's start.</param>
    internal static ObjectiveBook Of(IReadOnlyList<ObjectiveSpec> objectives, string closingPool, IReadOnlyList<ItemId> grants, string beaconStart) =>
        new(objectives, closingPool, grants, beaconStart);

    /// <summary>The objective with this id, or <see langword="null"/>.</summary>
    /// <param name="id">The objective's id.</param>
    public ObjectiveSpec? Find(string id) => Objectives.FirstOrDefault(o => string.Equals(o.Id, id, StringComparison.Ordinal));

    private static ItemId ItemNamed(string name) =>
        ItemDefinition.All.FirstOrDefault(d => string.Equals(d.Name, name, StringComparison.Ordinal))?.Id
        ?? throw new InvalidDataException($"{SessionPath} grants an unknown item '{name}'.");

    private static List<string> Strings(JsonElement element, string name) =>
        element.TryGetProperty(name, out var array) ? array.EnumerateArray().Select(static e => e.GetString() ?? string.Empty).ToList() : [];

    private static string Text(JsonElement element, string name, string path) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new InvalidDataException($"{path}: the text '{name}' is missing or empty.");
}
