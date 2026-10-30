using System.Globalization;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Phrases;

namespace Vocaterra.Game.Core.Npc;

/// <summary>What a host event does to the world (the game's meaning of the core's opaque event names).</summary>
public enum NpcEffectKind
{
    /// <summary>Nothing happens in the world (the event is a note for the log, or for a later milestone).</summary>
    None,

    /// <summary>The NPC puts an item in the inventory: one, a fixed number, or the number the learner asked for (VILLAGE_PLAN V02).</summary>
    GiveItem,

    /// <summary>The NPC sends the player to the nearest block of a noun: the beacon is set on it.</summary>
    PointAtNoun,

    /// <summary>The NPC sends the player to a named place of the scene, a point or a named prop (VILLAGE_PLAN V02): the beacon is set on it.</summary>
    PointAtPlace,

    /// <summary>
    /// The NPC sends the player to the place the learner named, read from a slot of the verdict (VILLAGE_PLAN V02, <c>place</c>): a word a label of the scene
    /// shows, or else a catalogue noun's nearest block (<see cref="Navigation.ScenePlaces"/>); a word that is neither is answered with <c>&lt;npc&gt;.unknown_place</c>.
    /// </summary>
    PointAtSlot,
}

/// <summary>
/// One entry of the host-event table: a world effect of a host event.
/// </summary>
/// <param name="Kind">What the effect does.</param>
/// <param name="Item">For <see cref="NpcEffectKind.GiveItem"/>: the item.</param>
/// <param name="Noun">For <see cref="NpcEffectKind.PointAtNoun"/>: the catalogue noun the beacon goes to.</param>
/// <param name="Count">For <see cref="NpcEffectKind.GiveItem"/>: how many, when <paramref name="CountSlot"/> does not say.</param>
/// <param name="CountSlot">For <see cref="NpcEffectKind.GiveItem"/>: the slot of the verdict that says how many (<see cref="QuantitySlot"/>), or <see langword="null"/> for <paramref name="Count"/>.</param>
/// <param name="Place">For <see cref="NpcEffectKind.PointAtPlace"/>: the scene's name of the place (<c>stall</c>, <c>river</c>).</param>
/// <param name="Slot">For <see cref="NpcEffectKind.PointAtSlot"/>: the slot of the verdict that names the place (<c>place</c>).</param>
public sealed record NpcEffect(
    NpcEffectKind Kind,
    ItemId Item = ItemId.Wood,
    string Noun = "",
    int Count = 1,
    string? CountSlot = null,
    string Place = "",
    string Slot = "")
{
    /// <summary>The verdict's slot of a number the learner said (<c>quiero dos panes</c>: <c>2</c>), the core's <c>[NUM:qty]</c>.</summary>
    public const string QuantitySlot = "qty";

    /// <summary>The most items a number the learner said can ask for at once (VILLAGE_PLAN 3.7): a larger number is read as this many.</summary>
    public const int MaxQuantity = 5;

    /// <summary>The effect that gives <paramref name="count"/> of <paramref name="item"/> (one by default).</summary>
    public static NpcEffect Give(ItemId item, int count = 1) => new(NpcEffectKind.GiveItem, item, Count: count);

    /// <summary>The effect that gives as many of <paramref name="item"/> as the learner asked for (<see cref="Quantity"/>).</summary>
    public static NpcEffect GiveAsked(ItemId item) => new(NpcEffectKind.GiveItem, item, CountSlot: QuantitySlot);

    /// <summary>The effect that points at the nearest block of <paramref name="noun"/>.</summary>
    public static NpcEffect PointAt(string noun) => new(NpcEffectKind.PointAtNoun, Noun: noun);

    /// <summary>The effect that points at the scene's place <paramref name="place"/>.</summary>
    public static NpcEffect PointAtPlaceNamed(string place) => new(NpcEffectKind.PointAtPlace, Place: place);

    /// <summary>The effect that points at the place the verdict's slot <paramref name="slot"/> names.</summary>
    public static NpcEffect PointAtSlotNamed(string slot) => new(NpcEffectKind.PointAtSlot, Slot: slot);

    /// <summary>
    /// How many items the effect moves for a verdict: the number the learner said for a count read from the slot (1 when the line had none, at most
    /// <see cref="MaxQuantity"/>), otherwise the fixed <see cref="Count"/>.
    /// </summary>
    /// <param name="slots">The verdict's slots.</param>
    public int CountFor(IReadOnlyDictionary<string, string> slots) => CountSlot is { } slot ? Quantity(slots, slot) : Count;

    /// <summary>
    /// The number a slot of the verdict holds (the core gives a number word as its digits, <c>dos</c> as <c>2</c>), from 1 to <see cref="MaxQuantity"/>: 1 when the
    /// slot is missing or holds no number, <see cref="MaxQuantity"/> for a larger one.
    /// </summary>
    /// <param name="slots">The verdict's slots.</param>
    /// <param name="slot">The slot (<see cref="QuantitySlot"/>).</param>
    public static int Quantity(IReadOnlyDictionary<string, string> slots, string slot = QuantitySlot)
    {
        ArgumentNullException.ThrowIfNull(slots);
        return slots.TryGetValue(slot, out var value) && long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number > 0
            ? (int)Math.Min(number, MaxQuantity)
            : 1;
    }
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

    /// <summary>
    /// What an NPC is asked to answer when its directions name a place the scene does not have (VILLAGE_PLAN V02, <see cref="NpcEffectKind.PointAtSlot"/>): the host
    /// event <c>&lt;npc&gt;.unknown_place</c>, raised to its agent as a host turn (<see cref="Of"/>).
    /// </summary>
    public const string UnknownPlace = "unknown_place";

    /// <summary>The host event <paramref name="what"/> of an NPC: <c>villager.unknown_place</c>.</summary>
    /// <param name="npc">The NPC's agent id.</param>
    /// <param name="what">What it answers (<see cref="UnknownPlace"/>).</param>
    public static string Of(string npc, string what) => npc + "." + what;
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
