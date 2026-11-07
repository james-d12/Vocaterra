using System.Text.Json;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Items;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Core.Npc;

/// <summary>
/// What each NPC host event does in the world (content-plan 3.6, C08), read from <c>game/host-events.json</c>: the shopkeeper's <c>stall.give_water</c> gives the
/// bottle, the villager's <c>villager.point</c> points the beacon at the place the learner asked for. The core's agents raise the host events; the game gives each
/// one its action. An event the file does not map changes nothing. The field is <c>action</c>, because the core's <c>effects/effects.json</c> already says "effect"
/// for another thing. Schema 2 (village-plan V02) gave <c>give_item</c> a count and added the pointing at a place and at the place a slot names; V03 added
/// <c>take_item</c> and <c>sell</c>.
/// </summary>
public sealed class HostEventTable
{
    /// <summary>The file's path inside the bundle folder.</summary>
    public const string RelativePath = "game/host-events.json";

    /// <summary>The file's format: 2 since village-plan V02 (a count on <c>give_item</c>, <c>point_at_place</c>, <c>point_at_slot</c>).</summary>
    public const int SchemaVersion = 2;

    /// <summary>The kinds of action the file may name.</summary>
    public static IReadOnlyList<string> Kinds { get; } = ["give_item", "point_at_noun", "point_at_place", "point_at_slot", "take_item", "sell", "buy", "procedure_repeat", "procedure_slower", "procedure_help", "give_missing", "teach_recipe", "start_procedure"];

    private readonly Dictionary<(string Npc, string HostEvent), NpcEffect> _table;

    private HostEventTable(Dictionary<(string Npc, string HostEvent), NpcEffect> table) => _table = table;

    /// <summary>Reads the table from the bundle's verified host file.</summary>
    /// <param name="bundle">The bundle, loaded with hash verification.</param>
    /// <exception cref="InvalidDataException">The bundle has no such host file, or the file is malformed.</exception>
    public static HostEventTable Load(ContentBundle bundle) => Parse(GameData.HostText(bundle, RelativePath));

    /// <summary>
    /// Builds the table from the file's text: <c>{ "schemaVersion": 2, "events": [ { "npc", "event", "action": { "kind": "give_item", "item", "count"? } | { "kind":
    /// "point_at_noun", "noun" } | { "kind": "point_at_place", "place" } | { "kind": "point_at_slot", "slot" } | { "kind": "take_item", "item", "count"? } | { "kind":
    /// "sell", "item", "price" } | { "kind": "buy", "item", "price" } | { "kind": "procedure_repeat" | "procedure_slower" | "procedure_help" | "give_missing" } | { "kind": "teach_recipe" | "start_procedure", "procedure"? } } ] }</c> (<c>buy</c> since village-plan V15: the NPC pays for what the learner sells; the six procedure kinds since W08). An item is named as the save names it (<c>water-bottle</c>); a count is a number from 1 or <c>"qty"</c>, the number the
    /// learner said (<see cref="NpcEffect.Quantity"/>), and one when it is left out; a price is a number of coins from 1.
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
                    "give_item" => Counted(NpcEffect.Give(ItemNamed(Text(action, "item"))), action, npc, name) with { Asked = AskedNouns(action, npc, name), Instead = InsteadNouns(action, npc, name) },
                    "point_at_noun" => NpcEffect.PointAt(Text(action, "noun")),
                    "point_at_place" => NpcEffect.PointAtPlaceNamed(Text(action, "place")),
                    "point_at_slot" => NpcEffect.PointAtSlotNamed(Text(action, "slot")),
                    "take_item" => Counted(NpcEffect.Take(ItemNamed(Text(action, "item"))), action, npc, name),
                    "sell" => NpcEffect.SellAt(ItemNamed(Text(action, "item")), Price(action, npc, name)),
                    "buy" => NpcEffect.BuyAt(ItemNamed(Text(action, "item")), Price(action, npc, name)),
                    "procedure_repeat" => new NpcEffect(NpcEffectKind.ProcedureRepeat),
                    "procedure_slower" => new NpcEffect(NpcEffectKind.ProcedureSlower),
                    "procedure_help" => new NpcEffect(NpcEffectKind.ProcedureHelp),
                    "give_missing" => new NpcEffect(NpcEffectKind.GiveMissing),
                    "teach_recipe" => new NpcEffect(NpcEffectKind.TeachRecipe, Procedure: OptionalText(action, "procedure")),
                    "start_procedure" => new NpcEffect(NpcEffectKind.StartProcedure, Procedure: OptionalText(action, "procedure")),
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

    // The nouns whose request a gift answers (V21, "asked"): a list of catalogue lemmas, or none when the gift does not depend on the request.
    private static IReadOnlyList<string>? AskedNouns(JsonElement action, string npc, string name)
    {
        if (!action.TryGetProperty("asked", out var asked))
        {
            return null;
        }

        var nouns = asked.ValueKind == JsonValueKind.Array ? asked.EnumerateArray().Select(static n => n.ValueKind == JsonValueKind.String ? n.GetString() ?? string.Empty : string.Empty).ToList() : [];
        return nouns.Count > 0 && nouns.All(static n => !string.IsNullOrWhiteSpace(n))
            ? nouns
            : throw new InvalidDataException($"{RelativePath}: the 'asked' of {npc}'s '{name}' must be a list of nouns.");
    }

    // The nouns a gift answers with another NPC event (V21b, "instead"): a map of catalogue lemma to the suffix of the host event the NPC answers (pan: give_bread).
    private static IReadOnlyDictionary<string, string>? InsteadNouns(JsonElement action, string npc, string name)
    {
        if (!action.TryGetProperty("instead", out var instead))
        {
            return null;
        }

        var map = instead.ValueKind == JsonValueKind.Object
            ? instead.EnumerateObject().ToDictionary(static p => p.Name, static p => p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() ?? string.Empty : string.Empty, StringComparer.Ordinal)
            : [];
        return map.Count > 0 && map.All(static p => !string.IsNullOrWhiteSpace(p.Key) && !string.IsNullOrWhiteSpace(p.Value)) && action.TryGetProperty("asked", out _)
            ? map
            : throw new InvalidDataException($"{RelativePath}: the 'instead' of {npc}'s '{name}' must map nouns to event names, and the gift must have 'asked'.");
    }

    // The price of a sale: a number of coins from 1.
    private static int Price(JsonElement action, string npc, string name) =>
        action.TryGetProperty("price", out var price) && price.ValueKind == JsonValueKind.Number && price.TryGetInt32(out var coins) && coins >= 1
            ? coins
            : throw new InvalidDataException($"{RelativePath}: the price of {npc}'s '{name}' must be a number of coins from 1.");

    private static ItemId ItemNamed(string name) =>
        ItemDefinition.All.FirstOrDefault(d => string.Equals(d.Name, name, StringComparison.Ordinal))?.Id
        ?? throw new InvalidDataException($"{RelativePath} names an unknown item '{name}'.");

    // A text that may be left out (empty then): the procedure of teach_recipe and start_procedure, which is the NPC's own when it is not named (W08).
    private static string OptionalText(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString()! : string.Empty;

    private static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new InvalidDataException($"{RelativePath}: the text '{name}' is missing or empty.");
}
