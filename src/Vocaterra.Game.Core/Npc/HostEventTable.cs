using System.Text.Json;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Items;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Core.Npc;

/// <summary>
/// What each NPC host event does in the world (CONTENT_PLAN 3.6, C08), read from <c>game/host-events.json</c>: the shopkeeper's <c>stall.give_water</c> gives the
/// bottle, the villager's <c>villager.point</c> points the beacon at the river. The core's agents raise the host events; the game gives each one its action. An
/// event the file does not map changes nothing. The field is <c>action</c>, because the core's <c>effects/effects.json</c> already says "effect" for another thing.
/// </summary>
public sealed class HostEventTable
{
    /// <summary>The file's path inside the bundle folder.</summary>
    public const string RelativePath = "game/host-events.json";

    private readonly Dictionary<(string Npc, string HostEvent), NpcEffect> _table;

    private HostEventTable(Dictionary<(string Npc, string HostEvent), NpcEffect> table) => _table = table;

    /// <summary>Reads the table from the bundle's verified host file.</summary>
    /// <param name="bundle">The bundle, loaded with hash verification.</param>
    /// <exception cref="InvalidDataException">The bundle has no such host file, or the file is malformed.</exception>
    public static HostEventTable Load(ContentBundle bundle) => Parse(GameData.HostText(bundle, RelativePath));

    /// <summary>
    /// Builds the table from the file's text: <c>{ "schemaVersion": 1, "events": [ { "npc", "event", "action": { "kind": "give_item", "item" } | { "kind":
    /// "point_at_noun", "noun" } } ] }</c>. An item is named as the save names it (<c>water-bottle</c>).
    /// </summary>
    /// <param name="json">The contents of <c>game/host-events.json</c>.</param>
    /// <exception cref="InvalidDataException">The text is malformed, an action's kind or item is unknown, or one NPC's event is given twice.</exception>
    public static HostEventTable Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.GetProperty("schemaVersion").GetInt32() != 1)
            {
                throw new InvalidDataException($"{RelativePath} must have schemaVersion 1.");
            }

            var table = new Dictionary<(string, string), NpcEffect>();
            foreach (var entry in root.GetProperty("events").EnumerateArray())
            {
                var npc = Text(entry, "npc");
                var name = Text(entry, "event");
                var action = entry.GetProperty("action");
                var effect = Text(action, "kind") switch
                {
                    "give_item" => NpcEffect.Give(ItemNamed(Text(action, "item"))),
                    "point_at_noun" => NpcEffect.PointAt(Text(action, "noun")),
                    var kind => throw new InvalidDataException($"{RelativePath}: the action '{kind}' of {npc}'s '{name}' is neither give_item nor point_at_noun."),
                };
                if (!table.TryAdd((npc, name), effect))
                {
                    throw new InvalidDataException($"{RelativePath}: {npc}'s '{name}' is given twice.");
                }
            }

            return new HostEventTable(table);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new InvalidDataException($"{RelativePath} is malformed: {ex.Message}", ex);
        }
    }

    /// <summary>What a host event does, or <see cref="NpcEffectKind.None"/> for an event the file does not map.</summary>
    /// <param name="npc">The agent that raised it.</param>
    /// <param name="hostEvent">The host event.</param>
    public NpcEffect Of(string npc, string hostEvent)
    {
        ArgumentNullException.ThrowIfNull(npc);
        ArgumentNullException.ThrowIfNull(hostEvent);
        return _table.TryGetValue((npc, hostEvent), out var effect) ? effect : new NpcEffect(NpcEffectKind.None);
    }

    /// <summary>Every mapped (NPC, host event), ordinal.</summary>
    public IReadOnlyList<(string Npc, string HostEvent)> Mapped() =>
        _table.Keys.OrderBy(static k => k.Npc, StringComparer.Ordinal).ThenBy(static k => k.HostEvent, StringComparer.Ordinal).ToList();

    private static ItemId ItemNamed(string name) =>
        ItemDefinition.All.FirstOrDefault(d => string.Equals(d.Name, name, StringComparison.Ordinal))?.Id
        ?? throw new InvalidDataException($"{RelativePath} names an unknown item '{name}'.");

    private static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new InvalidDataException($"{RelativePath}: the text '{name}' is missing or empty.");
}
