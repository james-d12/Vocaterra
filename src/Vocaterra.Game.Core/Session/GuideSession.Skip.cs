using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Game.Core.Session;

// The test aid --skip-to-session (V12c, SessionSkip): the part of an order's delivery that needs the conversations.
public abstract partial class GuideSession
{
    /// <summary>
    /// Delivers an order as the panel's opening does when the player walks up with its goods (<see cref="OpenTalk"/>, VILLAGE_PLAN V03), without a panel: the NPC's thanks
    /// is its <c>&lt;npc&gt;.delivered</c> host turn, the goods go, the reward its host events give comes in, and <see cref="WorldEvents.OrderDelivered"/> is logged (the
    /// order's step waits for it). Returns <see langword="false"/>, and changes nothing in the world, when the NPC's agent does not take the delivery.
    /// </summary>
    /// <param name="npc">The NPC the order is brought to.</param>
    /// <param name="order">The order's objective id.</param>
    internal bool DeliverWithoutTalk(string npc, string order)
    {
        var opening = _npcs.Open(npc, ObjectiveComplete, StoredTime, StateDigest(), NpcHostEvents.Of(npc, NpcHostEvents.Delivered));
        if (!opening.Delivered)
        {
            return false;
        }

        TakeOrder(npc, order); // the goods go before the reward comes in
        ApplyHostEvents(npc, opening.HostEvents, "order", SlotMap.Empty);
        Log(new WorldEvent(Tick, StoredTime.UnixSeconds, WorldEvents.OrderDelivered, npc, order));
        return true;
    }
}
