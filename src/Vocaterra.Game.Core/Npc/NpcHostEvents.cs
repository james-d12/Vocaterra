using System.Globalization;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Language.Words;

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

    /// <summary>The NPC takes items from the inventory (VILLAGE_PLAN V03), all of them or none.</summary>
    TakeItem,

    /// <summary>
    /// The NPC sells an item at a price in coins (VILLAGE_PLAN V03): as many as the learner asked for (the verdict's <c>qty</c>; one with no number; no cap since V19, the
    /// coins and the bag are the bound) for that many times the price. Otherwise nothing changes hands, and the NPC says why (<see cref="NpcEffect.ExchangeMiss"/>): asked
    /// for another item (V15) <c>&lt;npc&gt;.wont_sell</c>, for none (<c>quiero cero panes</c>, V19) <c>&lt;npc&gt;.none_asked</c>, with too few coins
    /// <c>&lt;npc&gt;.cant_pay</c>, and with no room in the bag for the items (V19) <c>&lt;npc&gt;.no_room</c>.
    /// </summary>
    Sell,

    /// <summary>
    /// The NPC buys an item at a price in coins (VILLAGE_PLAN V15, the stall's <see cref="Sell"/> turned round): the learner sells (<c>vendo madera</c>) and is paid the price
    /// for each item taken, as many as the learner said (<c>qty</c>, at most what the bag holds) or, with no number, every one the bag holds. Otherwise nothing changes
    /// hands, and the NPC says why (<see cref="NpcEffect.ExchangeMiss"/>): another item offered <c>&lt;npc&gt;.wont_buy</c>, none in the bag <c>&lt;npc&gt;.cant_sell</c>,
    /// none offered (<c>vendo cero bloques de madera</c>, V19) <c>&lt;npc&gt;.none_offered</c>, and no room in the bag for the coins (V19) <c>&lt;npc&gt;.no_room</c>.
    /// </summary>
    Buy,
}

/// <summary>
/// One entry of the host-event table: a world effect of a host event.
/// </summary>
/// <param name="Kind">What the effect does.</param>
/// <param name="Item">For <see cref="NpcEffectKind.GiveItem"/>, <see cref="NpcEffectKind.TakeItem"/>, <see cref="NpcEffectKind.Sell"/> and <see cref="NpcEffectKind.Buy"/>: the item.</param>
/// <param name="Noun">For <see cref="NpcEffectKind.PointAtNoun"/>: the catalogue noun the beacon goes to.</param>
/// <param name="Count">For <see cref="NpcEffectKind.GiveItem"/> and <see cref="NpcEffectKind.TakeItem"/>: how many, when <paramref name="CountSlot"/> does not say.</param>
/// <param name="CountSlot">For <see cref="NpcEffectKind.GiveItem"/> and <see cref="NpcEffectKind.TakeItem"/>: the slot of the verdict that says how many (<see cref="QuantitySlot"/>), or <see langword="null"/> for <paramref name="Count"/>.</param>
/// <param name="Place">For <see cref="NpcEffectKind.PointAtPlace"/>: the scene's name of the place (<c>stall</c>, <c>river</c>).</param>
/// <param name="Slot">For <see cref="NpcEffectKind.PointAtSlot"/>: the slot of the verdict that names the place (<c>place</c>).</param>
/// <param name="Price">For <see cref="NpcEffectKind.Sell"/> and <see cref="NpcEffectKind.Buy"/>: the price of one item, in coins.</param>
public sealed record NpcEffect(
    NpcEffectKind Kind,
    ItemId Item = ItemId.Wood,
    string Noun = "",
    int Count = 1,
    string? CountSlot = null,
    string Place = "",
    string Slot = "",
    int Price = 0)
{
    /// <summary>The verdict's slot of a number the learner said (<c>quiero dos panes</c>: <c>2</c>), the core's <c>[NUM:qty]</c>.</summary>
    public const string QuantitySlot = "qty";

    /// <summary>The verdict's slot of the item a line names (<c>vendo madera</c>: <c>madera</c>), the core's <c>[NOUN:item]</c>.</summary>
    public const string ItemSlot = "item";

    /// <summary>
    /// The most items a gift or a take by the number the learner said moves at once (<see cref="CountSlot"/>, a <c>give_item</c> or <c>take_item</c> whose count is
    /// <c>qty</c>; VILLAGE_PLAN 3.7): a larger number is read as this many, so no talk makes items out of nothing. A purchase and a sale have no cap since V19 (the
    /// review's R9): their bound is the coins, the goods and the bag.
    /// </summary>
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

    /// <summary>The effect that takes <paramref name="count"/> of <paramref name="item"/> (one by default).</summary>
    public static NpcEffect Take(ItemId item, int count = 1) => new(NpcEffectKind.TakeItem, item, Count: count);

    /// <summary>The effect that sells <paramref name="item"/> at <paramref name="price"/> coins each.</summary>
    public static NpcEffect SellAt(ItemId item, int price) => new(NpcEffectKind.Sell, item, Price: price);

    /// <summary>The effect that buys <paramref name="item"/> from the learner at <paramref name="price"/> coins each (VILLAGE_PLAN V15).</summary>
    public static NpcEffect BuyAt(ItemId item, int price) => new(NpcEffectKind.Buy, item, Price: price);

    /// <summary>
    /// The verdict's slots as the host's effects read them (V15b): an indefinite article before the item (<c>vendo un bloque de madera</c>, <c>vendo una fruta</c>) counts
    /// one when the line names no number, since the core reads <c>un</c> as an article and fills no <see cref="QuantitySlot"/>. A purchase and a gift give one with no
    /// number anyway; a sale (<see cref="NpcEffectKind.Buy"/>) takes one instead of every one the bag holds. The verdict itself is unchanged.
    /// </summary>
    /// <param name="slots">The verdict's slots.</param>
    /// <param name="line">What the learner said.</param>
    public static IReadOnlyDictionary<string, string> CountedSlots(IReadOnlyDictionary<string, string> slots, string line)
    {
        ArgumentNullException.ThrowIfNull(slots);
        ArgumentNullException.ThrowIfNull(line);
        if (slots.ContainsKey(QuantitySlot) || !PhraseSheet.Plain(line).Split(' ').Any(static w => w is "un" or "una"))
        {
            return slots;
        }

        var counted = slots.ToDictionary(static s => s.Key, static s => s.Value, StringComparer.Ordinal);
        counted[QuantitySlot] = "1";
        return counted;
    }

    /// <summary>
    /// How many items a purchase from the learner takes (<see cref="NpcEffectKind.Buy"/>): the number the learner said (<c>vendo dos frutas</c>, <c>vendo tres bloques de
    /// madera</c>, <c>vendo un bloque de madera</c> through <see cref="CountedSlots"/>; none for <c>cero</c>) or, with no number, every one the bag holds (<c>vendo
    /// madera</c>: the wood the learner carries); never more than the bag holds.
    /// </summary>
    /// <param name="slots">The verdict's slots.</param>
    /// <param name="held">How many of the item the bag holds.</param>
    public int SaleCountFor(IReadOnlyDictionary<string, string> slots, int held)
    {
        ArgumentNullException.ThrowIfNull(slots);
        var asked = slots.ContainsKey(QuantitySlot) ? Quantity(slots) : held;
        return Math.Max(0, Math.Min(asked, held));
    }

    /// <summary>
    /// How many items the effect moves for a verdict: for a sale, the number the learner said (1 when the line had none, 0 for <c>cero</c>); for a count read from the
    /// slot, that number at most <see cref="MaxQuantity"/>; otherwise the fixed <see cref="Count"/>.
    /// </summary>
    /// <param name="slots">The verdict's slots.</param>
    public int CountFor(IReadOnlyDictionary<string, string> slots) =>
        Kind == NpcEffectKind.Sell ? Quantity(slots) : CountSlot is { } slot ? Math.Min(Quantity(slots, slot), MaxQuantity) : Count;

    /// <summary>
    /// Whether the learner's line names this effect's item (VILLAGE_PLAN V15): its item slot is a catalogue noun of the item (<c>madera</c> the wood, <c>fruta</c> the fruit,
    /// <c>pan</c> the bread), or a word the catalogue lacks; a line with no item slot is read as asking for this effect's item too. A catalogue noun of another item, or of
    /// no item at all (V19, the review's R7: <c>quiero casa</c> at the bakery bought bread, <c>vendo piedra</c> to Domingo sold the wood), names another thing: the purchase
    /// or the sale changes nothing, and the NPC answers <c>&lt;npc&gt;.wont_sell</c> or <c>&lt;npc&gt;.wont_buy</c>.
    /// </summary>
    /// <param name="slots">The verdict's slots.</param>
    /// <param name="nouns">The noun catalogue.</param>
    public bool ItemSaidIn(IReadOnlyDictionary<string, string> slots, NounCatalog nouns)
    {
        ArgumentNullException.ThrowIfNull(slots);
        ArgumentNullException.ThrowIfNull(nouns);
        return !slots.TryGetValue(ItemSlot, out var word) || nouns.ForLemma(word) is not { } noun || noun.Items.Contains(Item);
    }

    /// <summary>What a sale costs for a verdict: the price times the number asked for (<see cref="CountFor"/>, <see cref="PriceOf"/>); zero for any other effect.</summary>
    /// <param name="slots">The verdict's slots.</param>
    public int CostFor(IReadOnlyDictionary<string, string> slots) => Kind == NpcEffectKind.Sell ? PriceOf(CountFor(slots)) : 0;

    /// <summary>
    /// The price of <paramref name="count"/> items in coins: the price of one times the count, at most <see cref="int.MaxValue"/> (V19, the review's R9: a number of
    /// twelve digits said in a purchase overflows no sum; it costs more coins than any bag holds).
    /// </summary>
    /// <param name="count">How many items.</param>
    public int PriceOf(int count) => (int)Math.Min((long)Price * Math.Max(0, count), int.MaxValue);

    /// <summary>
    /// What the bag cannot do of a purchase (<see cref="NpcEffectKind.Sell"/>) or a sale (<see cref="NpcEffectKind.Buy"/>) for a verdict, as the miss its NPC answers
    /// (<see cref="NpcHostEvents.Of"/>), or <see langword="null"/> when the exchange can be made and for any other effect. The NPC's worker asks it of the turn's copy
    /// of the bag, and the session again of the bag itself before anything changes hands, so the NPC never says a success line for an exchange that is not made (V19,
    /// the review's R8). In order, a purchase: another item (<see cref="NpcHostEvents.WontSell"/>), none asked for (<see cref="NpcHostEvents.NoneAsked"/>, R10), too few
    /// coins (<see cref="NpcHostEvents.CantPay"/>), no room for the items once their coins are paid (<see cref="NpcHostEvents.NoRoom"/>, R8); a sale: another item
    /// (<see cref="NpcHostEvents.WontBuy"/>), none in the bag (<see cref="NpcHostEvents.CantSell"/>), none offered (<see cref="NpcHostEvents.NoneOffered"/>, R10), no room
    /// for the coins once the items are taken (<see cref="NpcHostEvents.NoRoom"/>).
    /// </summary>
    /// <param name="slots">The verdict's slots, counted (<see cref="CountedSlots"/>).</param>
    /// <param name="bag">The bag, or <see langword="null"/> for a turn that carries none (then it holds nothing).</param>
    /// <param name="nouns">The noun catalogue.</param>
    public string? ExchangeMiss(IReadOnlyDictionary<string, string> slots, Inventory? bag, NounCatalog nouns)
    {
        ArgumentNullException.ThrowIfNull(slots);
        ArgumentNullException.ThrowIfNull(nouns);
        bag ??= new Inventory();
        if (Kind == NpcEffectKind.Sell)
        {
            var count = CountFor(slots);
            var cost = CostFor(slots);
            return !ItemSaidIn(slots, nouns) ? NpcHostEvents.WontSell
                : count <= 0 ? NpcHostEvents.NoneAsked
                : bag.Count(ItemId.Coin) < cost ? NpcHostEvents.CantPay
                : !bag.CanExchange(ItemId.Coin, cost, Item, count) ? NpcHostEvents.NoRoom
                : null;
        }

        if (Kind == NpcEffectKind.Buy)
        {
            var held = bag.Count(Item);
            var count = SaleCountFor(slots, held);
            return !ItemSaidIn(slots, nouns) ? NpcHostEvents.WontBuy
                : held == 0 ? NpcHostEvents.CantSell
                : count <= 0 ? NpcHostEvents.NoneOffered
                : !bag.CanExchange(Item, count, ItemId.Coin, PriceOf(count)) ? NpcHostEvents.NoRoom
                : null;
        }

        return null;
    }

    /// <summary>
    /// The number a slot of the verdict holds (the core gives a number word as its digits, <c>dos</c> as <c>2</c>): 1 when the slot is missing or holds no number; 0 for
    /// <c>cero</c> (V19, the review's R10: it was read as one, so <c>quiero cero panes</c> bought a loaf); <see cref="int.MaxValue"/> for a number too large for an
    /// <see cref="int"/> (R9: no number said overflows a count). No cap: the callers bound it (<see cref="CountFor"/>, <see cref="SaleCountFor"/>).
    /// </summary>
    /// <param name="slots">The verdict's slots.</param>
    /// <param name="slot">The slot (<see cref="QuantitySlot"/>).</param>
    public static int Quantity(IReadOnlyDictionary<string, string> slots, string slot = QuantitySlot)
    {
        ArgumentNullException.ThrowIfNull(slots);
        if (!slots.TryGetValue(slot, out var value) || value.Length == 0 || !value.All(char.IsAsciiDigit))
        {
            return 1;
        }

        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : int.MaxValue; // digits only: just too large fails
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

    /// <summary>What an NPC is asked to answer when the learner's coins do not cover a purchase (VILLAGE_PLAN V03, <see cref="NpcEffectKind.Sell"/>): <c>&lt;npc&gt;.cant_pay</c>.</summary>
    public const string CantPay = "cant_pay";

    /// <summary>What an NPC that sells is asked to answer when the learner asks it for another item than the one it sells (VILLAGE_PLAN V15): <c>&lt;npc&gt;.wont_sell</c>.</summary>
    public const string WontSell = "wont_sell";

    /// <summary>What an NPC that buys is asked to answer when the learner offers its item and the bag holds none (VILLAGE_PLAN V15, <see cref="NpcEffectKind.Buy"/>): <c>&lt;npc&gt;.cant_sell</c>.</summary>
    public const string CantSell = "cant_sell";

    /// <summary>What an NPC that buys is asked to answer when the learner offers another item than the one it buys (VILLAGE_PLAN V15): <c>&lt;npc&gt;.wont_buy</c>.</summary>
    public const string WontBuy = "wont_buy";

    /// <summary>
    /// What an NPC that sells or buys is asked to answer when the bag has no room for what the exchange brings (V19, the review's R8): the items of a purchase once their
    /// coins are paid, or the coins of a sale once its items are taken: <c>&lt;npc&gt;.no_room</c>.
    /// </summary>
    public const string NoRoom = "no_room";

    /// <summary>What an NPC that sells is asked to answer when the learner asks for none of its item (V19, the review's R10: <c>quiero cero panes</c>): <c>&lt;npc&gt;.none_asked</c>.</summary>
    public const string NoneAsked = "none_asked";

    /// <summary>What an NPC that buys is asked to answer when the learner offers none of its item (V19, the review's R10: <c>vendo cero bloques de madera</c>): <c>&lt;npc&gt;.none_offered</c>.</summary>
    public const string NoneOffered = "none_offered";

    /// <summary>
    /// What an NPC is asked to say when the learner brings the goods of its order (VILLAGE_PLAN V03, an objective with <c>deliverTo</c>): <c>&lt;npc&gt;.delivered</c>,
    /// raised as the opening of the talk; its thanks are the NPC's own line and its reward is that line's host event (a <c>give_item</c>).
    /// </summary>
    public const string Delivered = "delivered";

    /// <summary>The host event <paramref name="what"/> of an NPC: <c>villager.unknown_place</c>.</summary>
    /// <param name="npc">The NPC's agent id.</param>
    /// <param name="what">What it answers: <see cref="UnknownPlace"/>, <see cref="CantPay"/>, <see cref="WontSell"/>, <see cref="CantSell"/>, <see cref="WontBuy"/>,
    /// <see cref="NoRoom"/>, <see cref="NoneAsked"/>, <see cref="NoneOffered"/> or <see cref="Delivered"/>.</param>
    public static string Of(string npc, string what) => npc + "." + what;
}

/// <summary>
/// The phrase sheet's ids (<c>content/PHRASES.md</c>) as the game resolves them: a phrase is found from the intent the core resolved, the prompt
/// it was said at and the slots. The phrases in play and the row the prompt teaches come first (<c>PhraseSheet.PhraseFor</c>, VILLAGE_PLAN V12b); this is the
/// fixed mapping after them (<see cref="For"/>). A test reads the sheet and checks that every id here is a row of it.
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
