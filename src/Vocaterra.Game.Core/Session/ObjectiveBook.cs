using System.Text.Json;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Core.Session;

/// <summary>One step of an objective as the data gives it (content-plan 3.5, C07).</summary>
/// <param name="Id">The step's id inside its objective (<c>wood</c>); the save keys it <c>&lt;objective&gt;.&lt;step&gt;</c>.</param>
/// <param name="Noun">For a checklist step, the catalogue noun it is about (<c>madera</c>); otherwise empty.</param>
/// <param name="Label">The word the checklist shows (<c>frutas</c>): the file's <c>label</c>, or the noun's lemma for a mass noun or a target of one, else its plural.</param>
/// <param name="Target">How many matching events make the step done.</param>
/// <param name="Trigger">The world event the step counts.</param>
/// <param name="Restore">The code hook that works the step out again from a loaded world (<see cref="ObjectiveBook.RestoreHooks"/>), or <see langword="null"/> for a step the save keeps.</param>
public sealed record ObjectiveStepSpec(string Id, string Noun, string Label, int Target, Trigger Trigger, string? Restore);

/// <summary>Goods an order asks for (village-plan V03): an item and how many.</summary>
/// <param name="Item">The item.</param>
/// <param name="Count">How many, from 1.</param>
public sealed record OrderGoods(ItemId Item, int Count);

/// <summary>One objective as the data gives it (<c>game/objectives/&lt;id&gt;.json</c>, content-plan 3.5, C07).</summary>
/// <param name="Id">The objective's id, its file's name.</param>
/// <param name="Title">The target-language title of a checklist (<c>Campamento</c>), or <see langword="null"/> for an objective the HUD shows as one line (<c>objective.&lt;id&gt;.hud</c>).</param>
/// <param name="Steps">The steps, in checklist order.</param>
/// <param name="AlsoDoneBy">The objectives whose progress also completes this one (the guide counts as met once the camp checklist has moved).</param>
/// <param name="OnComplete">The code hooks run when the objective is completed (<see cref="ObjectiveBook.CompleteHooks"/>).</param>
/// <param name="Session">The play session the objective is shown and counted from (village-plan V03), or <see langword="null"/> for every session.</param>
/// <param name="DeliverTo">For an order (village-plan V03): the NPC the goods are brought to; its one step is the delivery (<see cref="ObjectiveBook.DeliveredStep"/>).</param>
/// <param name="Deliver">For an order: the goods, each item once.</param>
/// <param name="Lesson">
/// For an order (village-plan V13): the lesson that tells it in the three times, whose <c>do</c> step waits for the delivery, or <see langword="null"/>. While that lesson is
/// under way before the step, or is about to start at the talk, walking up with the goods does not deliver them (<see cref="Language.Lessons.LessonRunner"/>), so the plan
/// is said first even by a learner who carries them already.
/// </param>
public sealed record ObjectiveSpec(
    string Id,
    string? Title,
    IReadOnlyList<ObjectiveStepSpec> Steps,
    IReadOnlyList<string> AlsoDoneBy,
    IReadOnlyList<string> OnComplete,
    int? Session = null,
    string? DeliverTo = null,
    IReadOnlyList<OrderGoods>? Deliver = null,
    string? Lesson = null)
{
    /// <summary>Whether the HUD shows the objective as a checklist of its steps (it has a title).</summary>
    public bool IsChecklist => Title is not null;

    /// <summary>Whether the objective is an order: goods brought to an NPC (<see cref="DeliverTo"/>).</summary>
    public bool IsOrder => DeliverTo is not null;

    /// <summary>The goods of an order; empty for any other objective.</summary>
    public IReadOnlyList<OrderGoods> Goods => Deliver ?? [];

    /// <summary>
    /// Whether the session's ending (S17: the camp and the player's own fire) waits for this objective: every objective of the first session does, an order or an
    /// objective of a later session does not.
    /// </summary>
    public bool EndsSession => !IsOrder && Session is null;

    /// <summary>Whether the objective is shown and counted in a play session (from its <see cref="Session"/> on).</summary>
    /// <param name="playSession">The play session now.</param>
    public bool ShownIn(int playSession) => Session is not { } from || from <= playSession;
}

/// <summary>
/// The session's objectives (content-plan 3.5, C07): <c>game/session.json</c> gives their order, the dialogue pool of the closing line, the items the learner starts
/// with and the place the beacon starts on; each objective is its own file, <c>game/objectives/&lt;id&gt;.json</c>. The order is the HUD's: it gates nothing, and
/// every objective counts from the start, unless it names the play <c>session</c> it is shown and counted from (village-plan V03). What the data cannot say is a
/// named hook, and the hooks are code. An order (<c>deliverTo</c> and <c>deliver</c>, V03) has one step the loader makes: the delivery, which waits for
/// <see cref="WorldEvents.OrderDelivered"/> at its NPC with its id.
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

    /// <summary>The id of an order's one step, the delivery (the save keys it <c>&lt;order&gt;.delivered</c>).</summary>
    public const string DeliveredStep = "delivered";

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
    /// "alsoDoneBy"?, "onComplete"?, "session"? }</c>, or an order: <c>{ "schemaVersion": 1, "id", "session"?, "deliverTo", "deliver": [ { "item", "count" } ], "lesson"?,
    /// "onComplete"? }</c> (no title, no steps: its one step is the delivery; <c>lesson</c>, V13, the lesson that tells it).
    /// </summary>
    /// <param name="path">The file's path, for the messages.</param>
    /// <param name="json">The file's text.</param>
    /// <param name="id">The id the file must have (its name).</param>
    /// <param name="nouns">The noun catalogue.</param>
    /// <exception cref="InvalidDataException">The text is malformed, the id is not the file's name, a checklist step names no catalogue noun, a step id or target is
    /// wrong, the session is below 1, an order lacks its NPC or its goods, names an item twice or has a title or steps of its own, or another objective names a lesson.</exception>
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
            int? session = root.TryGetProperty("session", out var sessionElement) ? sessionElement.GetInt32() : null;
            if (session < 1)
            {
                throw new InvalidDataException($"{path}: the session must be a play session from 1.");
            }

            var deliverTo = root.TryGetProperty("deliverTo", out _) ? Text(root, "deliverTo", path) : null;
            var goods = Goods(root, path);
            if (deliverTo is null != (goods.Count == 0))
            {
                throw new InvalidDataException($"{path}: an order names the NPC it is brought to (deliverTo) and its goods (deliver), both or neither.");
            }

            var lesson = root.TryGetProperty("lesson", out _) ? Text(root, "lesson", path) : null;
            if (deliverTo is not null)
            {
                if (title is not null || root.TryGetProperty("steps", out _))
                {
                    throw new InvalidDataException($"{path}: an order has no title and no steps (its line is objective.{id}.hud, its one step the delivery).");
                }

                var delivery = new ObjectiveStepSpec(DeliveredStep, string.Empty, DeliveredStep, 1, new Trigger(WorldEvents.OrderDelivered, deliverTo, id), null);
                return new ObjectiveSpec(id, null, [delivery], Strings(root, "alsoDoneBy"), Strings(root, "onComplete"), session, deliverTo, goods, lesson);
            }

            if (lesson is not null)
            {
                throw new InvalidDataException($"{path}: only an order names the lesson that tells it (lesson).");
            }

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
                    label ??= entry.Mass || target == 1 ? entry.Lemma : entry.Plural; // content-plan 3.5: the label rule reproduces the old checklist's words
                }

                steps.Add(new ObjectiveStepSpec(stepId, noun, label ?? stepId, target, trigger, restore));
            }

            if (steps.Count == 0)
            {
                throw new InvalidDataException($"{path}: the objective has no steps.");
            }

            return new ObjectiveSpec(id, title, steps, Strings(root, "alsoDoneBy"), Strings(root, "onComplete"), session);
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

    private static ItemId ItemNamed(string name, string where = SessionPath) =>
        ItemDefinition.All.FirstOrDefault(d => string.Equals(d.Name, name, StringComparison.Ordinal))?.Id
        ?? throw new InvalidDataException($"{where} names an unknown item '{name}'.");

    // An order's goods (V03): each { "item", "count" }, an item by the name the save gives it, once, and a count from 1. Empty when the file has none.
    private static List<OrderGoods> Goods(JsonElement root, string path)
    {
        var goods = new List<OrderGoods>();
        if (!root.TryGetProperty("deliver", out var deliver))
        {
            return goods;
        }

        foreach (var element in deliver.EnumerateArray())
        {
            var item = ItemNamed(Text(element, "item", path), path);
            var count = element.GetProperty("count").GetInt32();
            if (count < 1 || goods.Any(g => g.Item == item))
            {
                throw new InvalidDataException($"{path}: the order asks for '{Text(element, "item", path)}' twice, or for fewer than one.");
            }

            goods.Add(new OrderGoods(item, count));
        }

        return goods;
    }

    private static List<string> Strings(JsonElement element, string name) =>
        element.TryGetProperty(name, out var array) ? array.EnumerateArray().Select(static e => e.GetString() ?? string.Empty).ToList() : [];

    private static string Text(JsonElement element, string name, string path) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new InvalidDataException($"{path}: the text '{name}' is missing or empty.");
}
