using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Npc;

namespace Vocaterra.Game.Core.Session;

// The offer of an order (issue #54): an order is a task the player takes on, not one that appears on meeting its NPC. When the talk opens on an NPC with an order not yet
// given, the NPC asks for the goods (<npc>.offer_order puts its agent in its offering_order state, a yes-or-no question). «Sí» gives the order (<npc>.accept_order): from
// then on it is shown, counted and delivered. «No» costs nothing and the NPC asks again from the next day (<npc>.decline_order); any other line or walking away leaves
// the offer for the next talk. A lesson that tells the order gives it at its step (lessonStep), with no offer.
public abstract partial class GuideSession
{
    // The order the NPC the panel is open on has just offered, while its agent waits for a yes or a no; null otherwise.
    private (string Npc, string Order)? _orderOffer;

    // The orders the player has taken on.
    private readonly SortedSet<string> _givenOrders = new(StringComparer.Ordinal);

    // The orders the player said no to since the last night: their NPCs do not offer them again until the next day.
    private readonly HashSet<string> _ordersDeclinedToday = new(StringComparer.Ordinal);

    /// <summary>The order the NPC the panel is open on has offered and waits for a yes or a no to (issue #54), or <see langword="null"/>.</summary>
    public string? OrderOffered => _orderOffer?.Order;

    /// <summary>The orders the player said no to since the last night (issue #54), ordinal: their NPCs offer them again from the next day.</summary>
    public IReadOnlyCollection<string> OrdersDeclinedToday => _ordersDeclinedToday.Order(StringComparer.Ordinal).ToList();

    /// <summary>
    /// The order an NPC could offer the player now (issue #54): one brought to it that is neither given nor done, or <see langword="null"/>. The 3D scene has the objectives;
    /// the base game has none.
    /// </summary>
    /// <param name="npc">The NPC the panel opens on.</param>
    protected virtual string? OrderToOfferAt(string npc) => null;

    /// <summary>The lesson that tells an order and gives it at its step (<c>lesson</c> with <c>lessonStep</c>), or <see langword="null"/>. The 3D scene has the objectives.</summary>
    /// <param name="order">The order's objective id.</param>
    protected virtual string? LessonGivingOrder(string order) => null;

    /// <summary>The orders the player has taken on (issue #54), ordinal: a «sí» to the NPC's offer, or the step of the lesson that tells the order. Saved as <c>givenOrders</c>.</summary>
    public IReadOnlyList<string> GivenOrders => [.. _givenOrders];

    /// <summary>Whether the player has taken on an order (issue #54): only then is it shown, counted and delivered.</summary>
    /// <param name="order">The order's objective id.</param>
    public bool HasOrder(string order) => _givenOrders.Contains(order);

    /// <summary>Restores the orders a save says the player has taken on (issue #54).</summary>
    /// <param name="orders">The orders' objective ids.</param>
    internal void RestoreGivenOrders(IEnumerable<string> orders)
    {
        _givenOrders.Clear();
        _givenOrders.UnionWith(orders);
    }

    /// <summary>The player takes on an order (issue #54): it is shown, counted and can be delivered from now on. Logs <see cref="WorldEvents.OrderAccepted"/> the first time.</summary>
    /// <param name="npc">The NPC who ordered.</param>
    /// <param name="order">The order's objective id.</param>
    /// <param name="how">How it was given, for the log: <c>offer</c> (a «sí»), <c>lesson</c> (its lesson's step) or <c>skip</c> (the test aid).</param>
    internal void GiveOrder(string npc, string order, string how)
    {
        if (_givenOrders.Add(order))
        {
            Log(new WorldEvent(Tick, StoredTime.UnixSeconds, WorldEvents.OrderAccepted, npc, $"{order}, {how}"));
        }
    }

    // The opening's offer of an order (OpenNow): the order and its host event, or null. Not while anything else holds the talk (as for a class's offer) or a lesson owns
    // the NPC's conversation, not when the class the teacher offers now is the one that gives the order at its step (a «no» to the class lets the order be offered at the
    // next talk), and not again on a day the player said no.
    private (string Order, string HostEvent)? OrderOfferOf(string npc)
    {
        if (!MayOfferClass(npc) || Lessons.OwnsConversation(npc) || OrderToOfferAt(npc) is not { } order || _ordersDeclinedToday.Contains(order))
        {
            return null;
        }

        if (LessonGivingOrder(order) is { } lesson && string.Equals(Lessons.ClassOfferedAt(npc), lesson, StringComparison.Ordinal))
        {
            return null;
        }

        return (order, NpcHostEvents.Of(npc, NpcHostEvents.OfferOrder));
    }

    // The talk opened with the offer (OpenNow): the session waits for the answer, and the English note says it is the player's choice.
    private void NoteOrderOffer(string npc, string order)
    {
        _orderOffer = (npc, order);
        RetryNote = Locale.Ui("orderOfferAsk", ("npc", NameOf(npc)));
        Log(new WorldEvent(Tick, StoredTime.UnixSeconds, WorldEvents.OrderOffered, npc, order));
    }

    // The talk opens on an NPC an earlier talk or a load left waiting on its offer (its offering_order state): it is back in its usual state first, silently.
    private void EndStaleOrderOffer(string npc)
    {
        if (string.Equals(_npcs.StateOf(npc), NpcHostEvents.OrderOfferState, StringComparison.Ordinal))
        {
            _ = _npcs.HostTurn(npc, NpcHostEvents.Of(npc, NpcHostEvents.OfferGone), StoredTime, StateDigest());
        }
    }

    // A turn the session applied (AfterTurn): a «sí» to the offer gives the order, a «no» puts it off until the next day, and any other line that took the NPC out of its
    // offering state withdraws the offer (it is made again at the next talk). A turn that was on the worker when the panel closed still counts: the order is the NPC's.
    private void OrderOfferAfter(GuideTurn turn)
    {
        if (turn.Unsure)
        {
            return;
        }

        var standing = _orderOffer is { } offer && string.Equals(offer.Npc, turn.Npc, StringComparison.Ordinal) ? offer.Order : null;
        var accepted = turn.HostEvents.Contains(NpcHostEvents.Of(turn.Npc, NpcHostEvents.AcceptOrder), StringComparer.Ordinal);
        var declined = turn.HostEvents.Contains(NpcHostEvents.Of(turn.Npc, NpcHostEvents.DeclineOrder), StringComparer.Ordinal);
        if (standing is null && !accepted && !declined)
        {
            return;
        }

        if (standing is not null)
        {
            _orderOffer = null;
            RetryNote = null;
        }

        var order = standing ?? OrderToOfferAt(turn.Npc);
        if (order is not null && accepted)
        {
            GiveOrder(turn.Npc, order, "offer");
        }
        else if (order is not null && declined)
        {
            _ = _ordersDeclinedToday.Add(order);
            Log(new WorldEvent(Tick, StoredTime.UnixSeconds, WorldEvents.OrderDeclined, turn.Npc, order));
        }
        else if (order is not null && standing is not null && string.Equals(turn.AgentState, NpcHostEvents.OrderOfferState, StringComparison.Ordinal))
        {
            _orderOffer = (turn.Npc, order); // «¿qué?», «no sé»: the NPC asks again and the offer stands
            RetryNote = Locale.Ui("orderOfferAsk", ("npc", NameOf(turn.Npc)));
            return;
        }

        if (standing is not null && PendingTurns == 0)
        {
            ReleaseClass(turn.Npc); // the class held back while the NPC waited for the answer is offered now, in the same talk
        }
    }

    /// <summary>The night passed: the orders the player said no to are offered again.</summary>
    protected void ClearDeclinedOrders() => _ordersDeclinedToday.Clear();
}
