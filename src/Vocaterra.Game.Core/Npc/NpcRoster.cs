using System.Text.Json;
using Vocaterra.Game.Core.Content;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Core.Npc;

/// <summary>One NPC as the game's data gives it (CONTENT_PLAN 3.6, C08): only what no other file holds.</summary>
/// <param name="Id">The agent id (<c>guide</c>): the core's <c>agents/&lt;id&gt;.json</c> has the same id, the scene places it, and the saves name it.</param>
/// <param name="Label">The target-language label over the NPC's head and on the dialogue panel (<c>Guía</c>, <c>Tendero</c>).</param>
/// <param name="Name">The NPC's own name (<c>Luna</c>), or <see langword="null"/> for an NPC known by its label.</param>
/// <param name="Figure">The key of the NPC's figure (its colours, its geometry and its face are drawn by the game, keyed by it).</param>
/// <param name="Focus">The phrases each of the NPC's prompts practises, by prompt (<c>stall-need</c> gives <c>pedir-agua</c>): the phrases that are at home with this NPC.</param>
public sealed record NpcEntry(string Id, string Label, string? Name, string Figure, IReadOnlyDictionary<string, IReadOnlyList<string>> Focus)
{
    /// <summary>The word the beacon shows for the NPC: their name, or their label when they have none (C08: before it the raw id for any NPC but the guide).</summary>
    public string BeaconLabel => Name ?? Label;
}

/// <summary>
/// The game's NPCs (CONTENT_PLAN 3.6, C08): <c>game/npcs.json</c> lists their ids in order (the saves keep the NPCs in this order), and each NPC is its own file,
/// <c>game/npcs/&lt;id&gt;.json</c>. What the NPC is in the conversation (its states and prompts) stays the core's <c>agents/</c> and <c>prompts/</c>, where it is placed
/// stays the scene's, its role in the support language is the locale's, and its behaviour (when it opens with what) stays code.
/// </summary>
public sealed class NpcRoster
{
    /// <summary>The index of the NPC files inside the bundle folder.</summary>
    public const string IndexPath = "game/npcs.json";

    /// <summary>The folder of the NPC files inside the bundle folder.</summary>
    public const string Folder = "game/npcs/";

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

    /// <summary>Builds one NPC from its file's text: <c>{ "schemaVersion": 1, "id", "label", "name"?, "figure", "focus"?: { "&lt;prompt&gt;": [ "&lt;phrase&gt;" ] } }</c>.</summary>
    /// <param name="path">The file's path, for the messages.</param>
    /// <param name="json">The file's text.</param>
    /// <param name="id">The id the file must have (its name).</param>
    /// <exception cref="InvalidDataException">The text is malformed, or the id is not the file's name.</exception>
    public static NpcEntry Parse(string path, string json, string id)
    {
        ArgumentNullException.ThrowIfNull(json);
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

            var name = root.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
            return new NpcEntry(id, Text(root, "label", path), string.IsNullOrWhiteSpace(name) ? null : name, Text(root, "figure", path), focus);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new InvalidDataException($"{path} is malformed: {ex.Message}", ex);
        }
    }

    /// <summary>A roster of these NPCs (the tests' own).</summary>
    /// <param name="npcs">The NPCs, in order.</param>
    internal static NpcRoster Of(IReadOnlyList<NpcEntry> npcs) => new(npcs);

    private static string Text(JsonElement element, string name, string path) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new InvalidDataException($"{path}: the text '{name}' is missing or empty.");
}
