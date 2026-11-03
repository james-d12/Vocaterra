using System.Text.Json;

namespace Vocaterra.Game.Core.Event;

/// <summary>
/// What a tip, a lesson's <c>do</c> step or an objective's step waits for (content-plan 3.4, C06): one world event (<see cref="WorldEvents"/>, or for a tip the
/// moments <c>start</c> and <c>aimed_npc</c>), with an optional NPC and an optional detail that must match exactly. The matcher reads world events only, so
/// every trigger can be seen in the session log. In a file: <c>{ "event": "item_picked_up", "detail": "madera" }</c>, <c>{ "event": "talk_opened", "npc": "guide" }</c>.
/// </summary>
/// <param name="Event">The event's name.</param>
/// <param name="Npc">The NPC the event must be about (its agent id), or <see langword="null"/> for any.</param>
/// <param name="Detail">The detail the event must have, exactly (ordinal: <c>firering</c> is not <c>firering.player</c>), or <see langword="null"/> for any.</param>
public sealed record Trigger(string Event, string? Npc = null, string? Detail = null)
{
    /// <summary>The moment a session starts (a tip's trigger; no world event).</summary>
    public const string Start = "start";

    /// <summary>The first time the crosshair is on an NPC (a tip's trigger; no world event).</summary>
    public const string AimedNpc = "aimed_npc";

    /// <summary>Every event a tip may wait for: <c>start</c>, <c>aimed_npc</c> and every world event.</summary>
    public static IReadOnlyList<string> TipEvents { get; } = [Start, AimedNpc, .. WorldEvents.All];

    /// <summary>Whether a world event is this trigger's: the same name, and the NPC and the detail when the trigger names them.</summary>
    /// <param name="world">The event.</param>
    public bool Matches(WorldEvent world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return string.Equals(world.Name, Event, StringComparison.Ordinal)
            && (Npc is null || string.Equals(world.Npc, Npc, StringComparison.Ordinal))
            && (Detail is null || string.Equals(world.Detail, Detail, StringComparison.Ordinal));
    }

    /// <summary>Whether a moment that is no world event (<c>start</c>, <c>aimed_npc</c>) is this trigger's: a trigger on it names no NPC and no detail.</summary>
    /// <param name="moment">The moment.</param>
    public bool Matches(string moment) => Npc is null && Detail is null && string.Equals(Event, moment, StringComparison.Ordinal);

    /// <summary>Reads a trigger from its object: <c>event</c>, and optionally <c>npc</c> and <c>detail</c>.</summary>
    /// <param name="element">The object.</param>
    /// <param name="events">The events this file's triggers may name.</param>
    /// <param name="where">Where the trigger is, for the messages (<c>game/tips.json: the tip 'look'</c>).</param>
    /// <exception cref="InvalidDataException">The object is not a trigger, or the event is not one of <paramref name="events"/>.</exception>
    public static Trigger Parse(JsonElement element, IReadOnlyList<string> events, string where)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"{where}: the trigger must be an object with an event.");
        }

        var name = Text(element, "event") ?? throw new InvalidDataException($"{where}: the trigger names no event.");
        if (!events.Contains(name, StringComparer.Ordinal))
        {
            throw new InvalidDataException($"{where}: the trigger waits for '{name}', which is not an event the game raises ({string.Join(", ", events)}).");
        }

        var npc = Text(element, "npc"); // an NPC of game/npcs.json: GameDataValidator checks it (C08)

        foreach (var property in element.EnumerateObject())
        {
            if (property.Name is not ("event" or "npc" or "detail"))
            {
                throw new InvalidDataException($"{where}: the trigger has the field '{property.Name}'; a trigger has event, npc and detail.");
            }
        }

        return new Trigger(name, npc, Text(element, "detail"));
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString() : null;
}
