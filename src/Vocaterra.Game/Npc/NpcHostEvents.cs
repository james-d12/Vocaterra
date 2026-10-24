using Vocaterra.Game.Language.Phrases;
using Vocaterra.Simulation.Items;

namespace Vocaterra.Game.Npc;

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
/// The one table that maps the NPCs' host events to the world (S6). The core knows the events as opaque strings in the agents' files
/// (<c>agents/*.json</c>); what each one means in the game (a bottle, a bread, the beacon on the river) is decided here and nowhere else, so the
/// game's words stay in <c>content/game-v1</c>. The same table serves a turn the player's line caused and a host turn (<c>Advance</c>) such as the
/// shopkeeper's fail-forward.
/// </summary>
/// <remarks>
/// The core's transitions are chosen by the intent alone, so an event does not know what the player asked for: <c>stall.give_water</c> hands
/// over the bottle whatever the request was (the stall has nothing else to give but the bread, which it offers next), and
/// <c>villager.give</c> gives bread whatever was asked. A content change that splits the intents would split these rows.
/// </remarks>
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

    /// <summary>The villager greets first (host turn).</summary>
    public const string VillagerGreet = "villager.greet";

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

    private static readonly Dictionary<(string Npc, string HostEvent), NpcEffect> Table = new()
    {
        [(NpcService.ShopkeeperAgent, GiveWater)] = NpcEffect.Give(ItemId.Bottle),
        [(NpcService.ShopkeeperAgent, GiveBread)] = NpcEffect.Give(ItemId.Bread),
        [(NpcService.VillagerAgent, VillagerPoint)] = NpcEffect.PointAt("río"),
        [(NpcService.VillagerAgent, VillagerGive)] = NpcEffect.Give(ItemId.Bread),
        [(NpcService.GuideAgent, GuideGiveWater)] = NpcEffect.Give(ItemId.WaterBottle),
    };

    /// <summary>The world effect of a host event of an NPC; <see cref="NpcEffectKind.None"/> for an event the table does not know.</summary>
    /// <param name="npc">The NPC's agent id.</param>
    /// <param name="hostEvent">The host event name.</param>
    public static NpcEffect Of(string npc, string hostEvent)
    {
        ArgumentNullException.ThrowIfNull(npc);
        ArgumentNullException.ThrowIfNull(hostEvent);
        return Table.TryGetValue((npc, hostEvent), out var effect) ? effect : new NpcEffect(NpcEffectKind.None);
    }

    /// <summary>The (NPC, event) pairs the table maps, in ordinal order (for the tests that check the table against the agents' files).</summary>
    public static IReadOnlyList<(string Npc, string HostEvent)> Mapped() =>
        Table.Keys.OrderBy(static k => k.Npc, StringComparer.Ordinal).ThenBy(static k => k.HostEvent, StringComparer.Ordinal).ToList();
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
    public static string? For(string intent, IReadOnlyDictionary<string, string> slots)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(slots);
        return intent switch
        {
            "request_item" => RequestWater,
            "confirm" => YesPlease,
            "deny" => NoThanks,
            "ask_directions" => slots.TryGetValue("place", out var place) && string.Equals(place, "río", StringComparison.Ordinal) ? WhereIsTheRiver : null,
            "greet" => Hello,
            "thank" => Thanks,
            "farewell" => Goodbye,
            "dont_know" => DontKnow,
            "ask_help" => Help,
            _ => null,
        };
    }

    /// <summary>
    /// The learner phrases an NPC prompt is waiting for and shows support for (S8): the stall's question is answered with <see cref="RequestWater"/>, its
    /// bread offer with <see cref="YesPlease"/> or <see cref="NoThanks"/>, and at the villager the taught phrase is <see cref="WhereIsTheRiver"/>.
    /// The guide's transfer offer has none: the transfer is meant to be unaided, with a different NPC. The pleasantries (<c>hola</c>, <c>gracias</c>,
    /// <c>adios</c>) and the service words have no scaffold.
    /// </summary>
    /// <param name="prompt">The prompt id (<c>stall-need</c>).</param>
    public static IReadOnlyList<string> FocusAt(string prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        return prompt switch
        {
            NpcService.StallNeedPrompt => [RequestWater],
            NpcService.StallOfferPrompt => [YesPlease, NoThanks],
            NpcService.VillagerPrompt => [WhereIsTheRiver],
            _ => [],
        };
    }

    /// <summary>Whether a phrase has a support scaffold to fade: a phrase some prompt shows a card for (<see cref="FocusAt"/>). Every other phrase stays at <see cref="SupportLevel.None"/>.</summary>
    /// <param name="phraseId">The sheet's id.</param>
    public static bool HasScaffold(string phraseId) => phraseId is RequestWater or YesPlease or NoThanks or WhereIsTheRiver;

    /// <summary>
    /// The NPC a phrase was taught with, or <see langword="null"/> when it has no home (the service words, which are used with anyone). Using a
    /// phrase with another NPC after the objective is a transfer.
    /// </summary>
    public static string? HomeOf(string phraseId) => phraseId switch
    {
        RequestWater or YesPlease or NoThanks => NpcService.ShopkeeperAgent,
        WhereIsTheRiver => NpcService.VillagerAgent,
        _ => null,
    };
}
