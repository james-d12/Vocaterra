using System.Text.Json;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Items;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Core.Npc;

/// <summary>
/// What each NPC host event does in the world (CONTENT_PLAN 3.6, C08), read from <c>game/host-events.json</c>: the shopkeeper's <c>stall.give_water</c> gives the
/// bottle, the villager's <c>villager.point</c> points the beacon at the place the learner asked for. The core's agents raise the host events; the game gives each
/// one its action. An event the file does not map changes nothing. The field is <c>action</c>, because the core's <c>effects/effects.json</c> already says "effect"
/// for another thing. Schema 2 (VILLAGE_PLAN V02) gave <c>give_item</c> a count and added the pointing at a place and at the place a slot names.
/// </summary>
public sealed class HostEventTable
{
    /// <summary>The file's path inside the bundle folder.</summary>
    public const string RelativePath = "game/host-events.json";

    /// <summary>The file's format: 2 since VILLAGE_PLAN V02 (a count on <c>give_item</c>, <c>point_at_place</c>, <c>point_at_slot</c>).</summary>
    public const int SchemaVersion = 2;

    /// <summary>The kinds of action the file may name.</summary>
    public static IReadOnlyList<string> Kinds { get; } = ["give_item", "point_at_noun", "point_at_place", "point_at_slot"];

    private readonly Dictionary<(string Npc, string HostEvent), NpcEffect> _table;

    private HostEventTable(Dictionary<(string Npc, string HostEvent), NpcEffect> table) => _table = table;

    /// <summary>Reads the table from the bundle's verified host file.</summary>
    /// <param name="bundle">The bundle, loaded with hash verification.</param>
    /// <exception cref="InvalidDataException">The bundle has no such host file, or the file is malformed.</exception>
    public static HostEventTable Load(ContentBundle bundle) => Parse(GameData.HostText(bundle, RelativePath));

    /// <summary>
    /// Builds the table from the file's text: <c>{ "schemaVersion": 2, "events": [ { "npc", "event", "action": { "kind": "give_item", "item", "count"? } | { "kind":
    /// "point_at_noun", "noun" } | { "kind": "point_at_place", "place" } | { "kind": "point_at_slot", "slot" } } ] }</c>. An item is named as the save names it
    /// (<c>water-bottle</c>); a count is a number from 1 or <c>"qty"</c>, the number the learner said (<see cref="NpcEffect.Quantity"/>), and one when it is left out.
    /// </summary>
    /// <param name="json">The contents of <c>game/host-events.json</c>.</param>
    /// <exception cref="InvalidDataException">The text is malformed, an action's kind, item or count is unknown, or one NPC's event is given twice.</exception>
    public static HostEventTable Parse(string json)
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

            var table = new Dictionary<(string, string), NpcEffect>();
            foreach (var entry in root.GetProperty("events").EnumerateArray())
            {
                var npc = Text(entry, "npc");
                var name = Text(entry, "event");
                var action = entry.GetProperty("action");
                var effect = Text(action, "kind") switch
                {
                    "give_item" => Counted(NpcEffect.Give(ItemNamed(Text(action, "item"))), action, npc, name),
                    "point_at_noun" => NpcEffect.PointAt(Text(action, "noun")),
                    "point_at_place" => NpcEffect.PointAtPlaceNamed(Text(action, "place")),
                    "point_at_slot" => NpcEffect.PointAtSlotNamed(Text(action, "slot")),
                    var kind => throw new InvalidDataException($"{RelativePath}: the action '{kind}' of {npc}'s '{name}' is not one of {string.Join(", ", Kinds)}."),
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

    // The count of an action that gives or takes items: a number from 1 (one when it is left out), or "qty", the number the learner said.
    private static NpcEffect Counted(NpcEffect effect, JsonElement action, string npc, string name)
    {
        if (!action.TryGetProperty("count", out var count))
        {
            return effect;
        }

        if (count.ValueKind == JsonValueKind.String && string.Equals(count.GetString(), NpcEffect.QuantitySlot, StringComparison.Ordinal))
        {
            return effect with { CountSlot = NpcEffect.QuantitySlot };
        }

        return count.ValueKind == JsonValueKind.Number && count.TryGetInt32(out var number) && number >= 1 && number <= ItemDefinition.Get(effect.Item).MaxStack
            ? effect with { Count = number }
            : throw new InvalidDataException($"{RelativePath}: the count of {npc}'s '{name}' must be \"{NpcEffect.QuantitySlot}\" or a number from 1 to the item's stack.");
    }

    private static ItemId ItemNamed(string name) =>
        ItemDefinition.All.FirstOrDefault(d => string.Equals(d.Name, name, StringComparison.Ordinal))?.Id
        ?? throw new InvalidDataException($"{RelativePath} names an unknown item '{name}'.");

    private static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new InvalidDataException($"{RelativePath}: the text '{name}' is missing or empty.");
}
