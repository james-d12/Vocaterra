using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Phrases;

namespace Vocaterra.Game.Core.Npc;

/// <summary>What a host event does to the world (the game's meaning of the core's opaque event names).</summary>
public enum NpcEffectKind
{
    /// <summary>Nothing happens in the world (the event is a note for the log, or for a later milestone).</summary>
    None,

    /// <summary>The NPC puts an item in the inventory.</summary>
    GiveItem,

    /// <summary>The NPC sends the player to the nearest block of a noun: the beacon is set on it.</summary>
    PointAtNoun,
}

/// <summary>
/// One entry of the host-event table: a world effect of a host event.
/// </summary>
/// <param name="Kind">What the effect does.</param>
/// <param name="Item">For <see cref="NpcEffectKind.GiveItem"/>: the item.</param>
/// <param name="Noun">For <see cref="NpcEffectKind.PointAtNoun"/>: the catalogue noun the beacon goes to.</param>
public sealed record NpcEffect(NpcEffectKind Kind, ItemId Item = ItemId.Wood, string Noun = "")
{
    /// <summary>The effect that gives one <paramref name="item"/>.</summary>
    public static NpcEffect Give(ItemId item) => new(NpcEffectKind.GiveItem, item);

    /// <summary>The effect that points at the nearest block of <paramref name="noun"/>.</summary>
    public static NpcEffect PointAt(string noun) => new(NpcEffectKind.PointAtNoun, Noun: noun);
}

/// <summary>
/// The names of the NPCs' host events the game's code reacts to (S6). The core knows the events as opaque strings in the agents' files (<c>agents/*.json</c>);
/// what an event does in the world (a bottle, a bread, the beacon on the river) is data since C08, <c>game/host-events.json</c> (<see cref="HostEventTable"/>).
/// The names here are the ones the code itself waits for (the guide's offer, the shopkeeper's later visits); an NPC's ordinary opening event is its NPC file's
/// (<see cref="NpcEntry.Opening"/>, VILLAGE_PLAN V01).
/// </summary>
public static class NpcHostEvents
{
    /// <summary>The shopkeeper opens with "¿Qué necesitas?" (host turn).</summary>
    public const string ShopkeeperAsk = "shopkeeper.ask";

    /// <summary>The shopkeeper hands over the bottle without a request (the fail-forward host turn).</summary>
    public const string ShopkeeperGive = "shopkeeper.give";

    /// <summary>The shopkeeper repeats the bread offer when the player comes back to a pending offer (host turn).</summary>
    public const string ShopkeeperOffer = "shopkeeper.offer";

    /// <summary>The shopkeeper hands over the empty bottle (raised by a request, or by <see cref="ShopkeeperGive"/>).</summary>
    public const string GiveWater = "stall.give_water";

    /// <summary>The shopkeeper hands over the bread.</summary>
    public const string GiveBread = "stall.give_bread";

    /// <summary>The villager points at the river.</summary>
    public const string VillagerPoint = "villager.point";

    /// <summary>The villager gives bread.</summary>
    public const string VillagerGive = "villager.give";

    /// <summary>The guide makes the transfer offer "¿Quieres agua?" (host turn).</summary>
    public const string GuideOffer = "guide.offer";

    /// <summary>The guide's offer was made (raised by the offer's line).</summary>
    public const string GuideOffered = "guide.offered";

    /// <summary>The player accepted the guide's offer: she gives water.</summary>
    public const string GuideGiveWater = "guide.give_water";
}

/// <summary>
/// The phrase sheet's ids (<c>content/PHRASES.md</c>) as the game resolves them: a phrase is found from the intent the core resolved, the prompt
/// it was said at and the slots (<see cref="For"/>). A test reads the sheet and checks that every id here is a row of it.
/// </summary>
public static class PhraseIds
{
    /// <summary>"Quiero agua, por favor." and its family of requests.</summary>
    public const string RequestWater = "pedir-agua";

    /// <summary>"Sí, por favor."</summary>
    public const string YesPlease = "si-por-favor";

    /// <summary>"No, gracias."</summary>
    public const string NoThanks = "no-gracias";

    /// <summary>"¿Dónde está el río?"</summary>
    public const string WhereIsTheRiver = "donde-esta-el-rio";

    /// <summary>"Hola."</summary>
    public const string Hello = "hola";

    /// <summary>"Gracias."</summary>
    public const string Thanks = "gracias";

    /// <summary>"Adiós."</summary>
    public const string Goodbye = "adios";

    /// <summary>"No sé."</summary>
    public const string DontKnow = "no-se";

    /// <summary>"Ayuda."</summary>
    public const string Help = "ayuda";

    /// <summary>"Sí." (S17: the lesson's yes; a lesson step has it in play, and the plain "sí" is otherwise <see cref="YesPlease"/>).</summary>
    public const string Yes = "si";

    /// <summary>"No." (S17: the lesson's no; otherwise <see cref="NoThanks"/>).</summary>
    public const string No = "no";

    /// <summary>"¿Qué es esto?" (S17).</summary>
    public const string WhatIsThis = "que-es-esto";

    /// <summary>"Otra vez, por favor." (S17).</summary>
    public const string AgainPlease = "otra-vez";

    /// <summary>"Más despacio, por favor." (S17).</summary>
    public const string SlowerPlease = "mas-despacio";

    /// <summary>Every phrase id the game can log, in ordinal order.</summary>
    public static IReadOnlyList<string> All { get; } =
        new[] { RequestWater, YesPlease, NoThanks, WhereIsTheRiver, Hello, Thanks, Goodbye, DontKnow, Help, Yes, No, WhatIsThis, AgainPlease, SlowerPlease }.Order(StringComparer.Ordinal).ToList();

    /// <summary>
    /// The sheet's id of the phrase a communicated turn used, or <see langword="null"/> when the turn is not one of the sheet's phrases (a
    /// question about a tree, a recipe). A request is the phrase of <see cref="RequestWater"/> whatever the item: the chunk "quiero X, por favor"
    /// is the phrase, the item is the detail. A question about a place is <see cref="WhereIsTheRiver"/> only when the place is the river.
    /// </summary>
    /// <param name="intent">The intent the core resolved.</param>
    /// <param name="slots">The slots the core filled.</param>
    /// <param name="river">The river's noun (<c>río</c>, <c>game/words.json</c>).</param>
    public static string? For(string intent, IReadOnlyDictionary<string, string> slots, string river)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(slots);
        ArgumentNullException.ThrowIfNull(river);
        return intent switch
        {
            "request_item" => RequestWater,
            "confirm" => YesPlease,
            "deny" => NoThanks,
            "ask_directions" => slots.TryGetValue("place", out var place) && string.Equals(place, river, StringComparison.Ordinal) ? WhereIsTheRiver : null,
            "greet" => Hello,
            "thank" => Thanks,
            "farewell" => Goodbye,
            "dont_know" => DontKnow,
            "ask_help" => Help,
            _ => null,
        };
    }
}
