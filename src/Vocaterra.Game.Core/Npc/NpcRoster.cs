using System.Globalization;
using System.Text.Json;
using Vocaterra.Game.Core.Content;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Core.Npc;

/// <summary>One NPC as the game's data gives it (content-plan 3.6, C08): only what no other file holds, and since schema 2 (village-plan V01) what it does that was code.</summary>
/// <param name="Id">The agent id (<c>guide</c>): the core's <c>agents/&lt;id&gt;.json</c> has the same id, the scene places it, and the saves name it.</param>
/// <param name="Label">The target-language label over the NPC's head and on the dialogue panel (<c>Guía</c>, <c>Tendero</c>).</param>
/// <param name="Name">The NPC's own name (<c>Luna</c>), or <see langword="null"/> for an NPC known by its label.</param>
/// <param name="Figure">The key of the NPC's figure (its colours, its geometry and its face are drawn by the game, keyed by it).</param>
/// <param name="Focus">The phrases each of the NPC's prompts practises, by prompt (<c>stall-need</c> gives <c>pedir-agua</c>): the phrases that are at home with this NPC.</param>
public sealed record NpcEntry(string Id, string Label, string? Name, string Figure, IReadOnlyDictionary<string, IReadOnlyList<string>> Focus)
{
    /// <summary>The key of <c>prompts</c> that names the review question's prompt; it is no state of an agent.</summary>
    public const string ReviewKey = "review";

    private readonly string? _poolPrefix;

    /// <summary>What the NPC is called: their name, or their label when they have none (the beacon shows it, C08; <c>{npc:&lt;id&gt;}</c> in the support language, C04).</summary>
    public string Called => Name ?? Label;

    /// <summary>
    /// The prompt the NPC waits on in each state of its agent (<c>idle</c>: <c>stall-need</c>, <c>offering</c>: <c>stall-offer</c>); a state not listed waits on the
    /// initial state's (<see cref="PromptIn"/>). Empty for an NPC whose file names none, which the validator reports.
    /// </summary>
    public IReadOnlyDictionary<string, string> Prompts { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>The prompt of the guide's review question (<c>guide-review</c>; <c>prompts.review</c> in the file), or <see langword="null"/> for an NPC that asks none.</summary>
    public string? ReviewPrompt { get; init; }

    /// <summary>How the NPC opens a talk (<see cref="NpcOpeningRule.Never"/> when the file says nothing).</summary>
    public NpcOpeningRule Opening { get; init; } = NpcOpeningRule.Never;

    /// <summary>The voice its lines are recorded with, or <see langword="null"/> when the file gives none (a validation error).</summary>
    public NpcVoiceSpec? Voice { get; init; }

    /// <summary>
    /// The pools an NPC says only while a lesson step is answered, by the pool it says in free talk instead (V21b: <c>farmer_total</c>, fifteen apples for fifteen coins, is
    /// the table the market lesson teaches; outside it León says <c>farmer_unit</c>, the price of one). The agent's transition says both, and the game keeps the one that fits
    /// (<see cref="NpcTurnRules.Compose"/>). Empty for most.
    /// </summary>
    public IReadOnlyDictionary<string, string> FreeTalkPools { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>The key words (catalogue nouns) whose cards the NPC shows alone before its first question (the shopkeeper's <c>agua</c>, <c>pan</c>); empty for most.</summary>
    public IReadOnlyList<string> KeyWords { get; init; } = [];

    /// <summary>The name the NPC's dialogue pools carry (<c>shop</c> for the shopkeeper's <c>shop_*</c> and <c>guide.lesson.shop.*</c>); its id unless the file says otherwise.</summary>
    public string PoolPrefix
    {
        get => _poolPrefix ?? Id;
        init => _poolPrefix = value;
    }

    /// <summary>
    /// The prompt the NPC waits on in a state: the review prompt while the review question is open (for an NPC that has one), else the state's own prompt, else the
    /// prompt of the agent's initial state; <see langword="null"/> when the file names neither.
    /// </summary>
    /// <param name="stateId">The agent's state.</param>
    /// <param name="initialState">The agent's initial state (<c>AgentStateMachine.InitialState</c>).</param>
    /// <param name="review">Whether the review question is open.</param>
    public string? PromptIn(string stateId, string initialState, bool review = false)
    {
        ArgumentNullException.ThrowIfNull(stateId);
        ArgumentNullException.ThrowIfNull(initialState);
        if (review && ReviewPrompt is { } reviewPrompt)
        {
            return reviewPrompt;
        }

        return Prompts.TryGetValue(stateId, out var prompt) ? prompt : Prompts.GetValueOrDefault(initialState);
    }

    /// <summary>
    /// Whether a dialogue pool is this NPC's, so its lines speak with its voice: <c>&lt;poolPrefix&gt;_*</c> (<c>villager_hello</c>) and the lesson lines
    /// <c>guide.lesson.&lt;poolPrefix&gt;.*</c> (named so for the content lint, S26).
    /// </summary>
    /// <param name="poolId">The pool id.</param>
    public bool OwnsPool(string poolId)
    {
        ArgumentNullException.ThrowIfNull(poolId);
        return poolId.StartsWith(PoolPrefix + "_", StringComparison.Ordinal) || poolId.StartsWith(NpcRoster.LessonPoolPrefix + PoolPrefix + ".", StringComparison.Ordinal);
    }
}

/// <summary>
/// The game's NPCs (content-plan 3.6, C08): <c>game/npcs.json</c> lists their ids in order (the saves keep the NPCs in this order), and each NPC is its own file,
/// <c>game/npcs/&lt;id&gt;.json</c>. What the NPC is in the conversation (its states and prompts) stays the core's <c>agents/</c> and <c>prompts/</c>, where it is placed
/// stays the scene's, and its role in the support language is the locale's. Since schema 2 (village-plan V01) the file also says which prompt it waits on in each
/// state, the host event it opens a talk with, its voice, its key words and the name of its pools, so a new NPC needs no code; the behaviour the data cannot say
/// (the shopkeeper's fail-forward, the guide's review and transfer offer) stays code, by id.
/// </summary>
public sealed class NpcRoster
{
    /// <summary>The index of the NPC files inside the bundle folder.</summary>
    public const string IndexPath = "game/npcs.json";

    /// <summary>The folder of the NPC files inside the bundle folder.</summary>
    public const string Folder = "game/npcs/";

    /// <summary>The schema version of an NPC file.</summary>
    public const int SchemaVersion = 2;

    /// <summary>The pool-id prefix of the NPCs' lesson lines (S26): <c>guide.lesson.&lt;poolPrefix&gt;.*</c> is the NPC's, named so the content lint counts it as the game's own pool.</summary>
    public const string LessonPoolPrefix = "guide.lesson.";

    /// <summary>The largest gain a voice may take (<see cref="NpcVoiceSpec.Gain"/>, village-plan V11): a voice is brought down or up to the others' level, not made to clip.</summary>
    public const float MaxGain = 4.0f;

    private readonly Dictionary<string, NpcEntry> _byId;

    private NpcRoster(IReadOnlyList<NpcEntry> npcs)
    {
        All = npcs;
        Ids = npcs.Select(static n => n.Id).ToList();
        _byId = npcs.ToDictionary(static n => n.Id, StringComparer.Ordinal);
        var homes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var npc in npcs)
        {
            foreach (var phrase in npc.Focus.Values.SelectMany(static phrases => phrases))
            {
                homes.TryAdd(phrase, npc.Id);
            }
        }

        Homes = homes;
        ScaffoldPhrases = homes.Keys.ToList();
    }

    /// <summary>The NPCs, in the index's order.</summary>
    public IReadOnlyList<NpcEntry> All { get; }

    /// <summary>The NPCs' agent ids, in the index's order.</summary>
    public IReadOnlyList<string> Ids { get; }

    /// <summary>Each phrase some NPC's prompt practises, with that NPC (its home: the guide sends the learner back there to practise it).</summary>
    public IReadOnlyDictionary<string, string> Homes { get; }

    /// <summary>The phrases with a scaffold to fade: the ones an NPC's prompt practises (the four of the stall and the river).</summary>
    public IReadOnlyList<string> ScaffoldPhrases { get; }

    /// <summary>The NPC with this id, or <see langword="null"/>.</summary>
    /// <param name="id">The agent id.</param>
    public NpcEntry? Find(string id) => _byId.TryGetValue(id, out var npc) ? npc : null;

    /// <summary>The NPC whose dialogue pool this is (<see cref="NpcEntry.OwnsPool"/>), the first in the index's order, or <see langword="null"/> for a pool of no NPC.</summary>
    /// <param name="poolId">The pool id.</param>
    public NpcEntry? OwnerOfPool(string poolId)
    {
        ArgumentNullException.ThrowIfNull(poolId);
        return All.FirstOrDefault(npc => npc.OwnsPool(poolId));
    }

    /// <summary>The label of an NPC (<c>Guía</c>), or the id itself for an NPC the data does not have.</summary>
    /// <param name="id">The agent id.</param>
    public string LabelOf(string id) => Find(id)?.Label ?? id;

    /// <summary>The phrases a prompt practises (its NPC's focus at it), or none.</summary>
    /// <param name="prompt">The prompt's id.</param>
    public IReadOnlyList<string> FocusAt(string prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        foreach (var npc in All)
        {
            if (npc.Focus.TryGetValue(prompt, out var phrases))
            {
                return phrases;
            }
        }

        return [];
    }

    /// <summary>The NPC a phrase is at home with, or <see langword="null"/>.</summary>
    /// <param name="phraseId">The phrase sheet's id.</param>
    public string? HomeOf(string phraseId) => Homes.TryGetValue(phraseId, out var home) ? home : null;

    /// <summary>
    /// Whether a phrase is at home with an NPC: one of its prompts practises it. A phrase can be at home with two NPCs (the smith's and the farmer's
    /// <c>como-te-llamas</c>, village-plan V12b); <see cref="HomeOf"/> names the first of them in the index's order.
    /// </summary>
    /// <param name="npc">The agent id.</param>
    /// <param name="phraseId">The phrase sheet's id.</param>
    public bool IsHomeOf(string npc, string phraseId) =>
        Find(npc) is { } entry && entry.Focus.Values.Any(phrases => phrases.Contains(phraseId, StringComparer.Ordinal));

    /// <summary>Whether a phrase has a scaffold to fade (an NPC's prompt practises it).</summary>
    /// <param name="phraseId">The phrase sheet's id.</param>
    public bool HasScaffold(string phraseId) => Homes.ContainsKey(phraseId);

    /// <summary>Reads the NPCs from the bundle's verified host files.</summary>
    /// <param name="bundle">The bundle, loaded with hash verification.</param>
    /// <exception cref="InvalidDataException">A file is missing, malformed, not listed, or names another id.</exception>
    public static NpcRoster Load(ContentBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        List<string> ids;
        try
        {
            using var index = JsonDocument.Parse(GameData.HostText(bundle, IndexPath));
            if (index.RootElement.GetProperty("schemaVersion").GetInt32() != 1)
            {
                throw new InvalidDataException($"{IndexPath} must have schemaVersion 1.");
            }

            ids = index.RootElement.GetProperty("npcs").EnumerateArray().Select(static e => e.GetString() ?? string.Empty).ToList();
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new InvalidDataException($"{IndexPath} is malformed: {ex.Message}", ex);
        }

        if (ids.Count == 0 || ids.Distinct(StringComparer.Ordinal).Count() != ids.Count)
        {
            throw new InvalidDataException($"{IndexPath} must list each NPC once, and at least one.");
        }

        var unlisted = bundle.HostFilePaths.Where(path => path.StartsWith(Folder, StringComparison.Ordinal) && !ids.Contains(path[Folder.Length..^".json".Length], StringComparer.Ordinal)).ToList();
        if (unlisted.Count > 0)
        {
            throw new InvalidDataException($"{IndexPath} does not list the NPC file {string.Join(", ", unlisted)}.");
        }

        return new NpcRoster(ids.Select(id => Parse(Folder + id + ".json", GameData.HostText(bundle, Folder + id + ".json"), id)).ToList());
    }

    /// <summary>
    /// Builds one NPC from its file's text: <c>{ "schemaVersion": 2, "id", "label", "name"?, "figure", "focus"?: { "&lt;prompt&gt;": [ "&lt;phrase&gt;" ] },
    /// "prompts": { "&lt;state&gt;" | "review": "&lt;prompt&gt;" }, "opening": { "event"?, "when": "always" | "first" | "never" }, "voice": { "speaker", "name", "speed"? },
    /// "keyWords"?: [ "&lt;noun&gt;" ], "poolPrefix"? }</c>. What needs another file (a prompt that is no bundle prompt, an event no state takes) or is missing
    /// (no prompts, no voice, a voice with no name) is left to the validator, which reports every problem at once.
    /// </summary>
    /// <param name="path">The file's path, for the messages.</param>
    /// <param name="json">The file's text.</param>
    /// <param name="id">The id the file must have (its name).</param>
    /// <exception cref="InvalidDataException">The text is malformed, the id is not the file's name, or the opening or the voice is not of their shape.</exception>
    public static NpcEntry Parse(string path, string json, string id)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.GetProperty("schemaVersion").GetInt32() != SchemaVersion)
            {
                throw new InvalidDataException($"{path} must have schemaVersion {SchemaVersion}.");
            }

            if (!string.Equals(Text(root, "id", path), id, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"{path}: the NPC's id must be its file's name, '{id}'.");
            }

            var focus = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            if (root.TryGetProperty("focus", out var focusElement))
            {
                foreach (var prompt in focusElement.EnumerateObject())
                {
                    focus[prompt.Name] = prompt.Value.EnumerateArray().Select(static p => p.GetString() ?? string.Empty).ToList();
                }
            }

            var prompts = new Dictionary<string, string>(StringComparer.Ordinal);
            string? review = null;
            if (root.TryGetProperty("prompts", out var promptsElement))
            {
                foreach (var state in promptsElement.EnumerateObject())
                {
                    var prompt = state.Value.GetString() ?? string.Empty;
                    if (string.Equals(state.Name, NpcEntry.ReviewKey, StringComparison.Ordinal))
                    {
                        review = prompt;
                    }
                    else
                    {
                        prompts[state.Name] = prompt;
                    }
                }
            }

            var name = root.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
            var pools = root.TryGetProperty("poolPrefix", out var poolsElement) ? poolsElement.GetString() : null;
            var entry = new NpcEntry(id, Text(root, "label", path), string.IsNullOrWhiteSpace(name) ? null : name, Text(root, "figure", path), focus)
            {
                Prompts = prompts,
                ReviewPrompt = review,
                Opening = root.TryGetProperty("opening", out var opening) ? OpeningOf(opening, path) : NpcOpeningRule.Never,
                Voice = root.TryGetProperty("voice", out var voice) ? VoiceOf(voice, path) : null,
                FreeTalkPools = root.TryGetProperty("freeTalkPools", out var freeTalk) ? freeTalk.EnumerateObject().ToDictionary(static p => p.Name, static p => p.Value.GetString() ?? string.Empty, StringComparer.Ordinal) : new Dictionary<string, string>(StringComparer.Ordinal),
                KeyWords = root.TryGetProperty("keyWords", out var keyWords) ? keyWords.EnumerateArray().Select(static w => w.GetString() ?? string.Empty).ToList() : [],
            };
            return string.IsNullOrWhiteSpace(pools) ? entry : entry with { PoolPrefix = pools };
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new InvalidDataException($"{path} is malformed: {ex.Message}", ex);
        }
    }

    /// <summary>A roster of these NPCs (the tests' own).</summary>
    /// <param name="npcs">The NPCs, in order.</param>
    internal static NpcRoster Of(IReadOnlyList<NpcEntry> npcs) => new(npcs);

    // { "event"?, "when": "always" | "first" | "never" }: a rule that raises an event must name it.
    private static NpcOpeningRule OpeningOf(JsonElement opening, string path)
    {
        var when = Text(opening, "when", path) switch
        {
            "always" => OpeningWhen.Always,
            "first" => OpeningWhen.First,
            "never" => OpeningWhen.Never,
            var other => throw new InvalidDataException($"{path}: the opening's 'when' is '{other}', not always, first or never."),
        };
        if (when == OpeningWhen.Never)
        {
            return NpcOpeningRule.Never;
        }

        return new NpcOpeningRule(Text(opening, "event", path), when);
    }

    // { "speaker": <Kokoro id>, "name": "<voice>", "speed"?: 1.0, "gain"?: 1.0 }: the name may be missing here (the validator says so); the speaker, the speed
    // and the gain (V11) are numbers.
    private static NpcVoiceSpec VoiceOf(JsonElement voice, string path)
    {
        var speaker = voice.GetProperty("speaker").GetInt32();
        var speed = voice.TryGetProperty("speed", out var speedElement) ? speedElement.GetSingle() : 1.0f;
        var gain = voice.TryGetProperty("gain", out var gainElement) ? gainElement.GetSingle() : 1.0f;
        if (speaker < 0 || !(speed > 0) || !(gain > 0) || gain > MaxGain)
        {
            throw new InvalidDataException(string.Create(CultureInfo.InvariantCulture, $"{path}: the voice's speaker ({speaker}) must be 0 or more, its speed ({speed}) more than 0 and its gain ({gain}) more than 0 and at most {MaxGain}."));
        }

        var name = voice.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String ? nameElement.GetString()!.Trim() : string.Empty;
        return new NpcVoiceSpec(speaker, name, speed, gain);
    }

    private static string Text(JsonElement element, string name, string path) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new InvalidDataException($"{path}: the text '{name}' is missing or empty.");
}
