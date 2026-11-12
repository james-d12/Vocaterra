using Vocaterra.Game.Core.Npc;

namespace Vocaterra.Game.Core.Session;

// The mayor's offers about the player's land (Y11, D-game-32): when the talk opens on the NPC who sells the plot, he offers the stake while the player has none and no plot (<npc>.offer_stake
// puts his agent in its offering_stake state, a yes-or-no question) and the plot once one is marked (<npc>.offer_plot, offering_plot). «Sí» is the agent's own answer: its transitions raise the
// host events that give the stake or sell the plot. Any other line that leaves the offering state withdraws the offer; walking away leaves it for the next talk (EndStaleLandOffer). A class
// held back while the NPC waits for the answer follows in the same talk, as for an order's offer (ReleaseClass).
public abstract partial class GuideSession
{
    // The NPC the panel is open on has just offered land, while its agent waits for an answer; null otherwise.
    private string? _landOffer;

    /// <summary>Whether the NPC whose talk is open has offered the stake or the plot and waits for a yes or a no (Y11).</summary>
    public bool LandOffered => _landOffer is not null;

    private static bool IsLandOfferState(string state) =>
        string.Equals(state, NpcHostEvents.StakeOfferState, StringComparison.Ordinal) || string.Equals(state, NpcHostEvents.PlotOfferState, StringComparison.Ordinal);

    // The opening's offer about the land (OpenNow): its host event, or null. Not while anything else holds the talk (as for a class's offer) or a lesson owns the NPC's conversation.
    private string? LandOfferOf(string npc) =>
        !MayOfferClass(npc) || Lessons.OwnsConversation(npc) ? null : LandOfferAt(npc);

    // The talk opened with the offer (OpenNow): the session waits for the answer, and the English note says it is the player's choice.
    private void NoteLandOffer(string npc)
    {
        _landOffer = npc;
        RetryNote = Locale.Ui(string.Equals(_npcs.StateOf(npc), NpcHostEvents.StakeOfferState, StringComparison.Ordinal) ? "landStakeAsk" : "landPlotAsk", ("npc", NameOf(npc)));
    }

    // The talk opens on an NPC an earlier talk or a load left waiting on its offer: it is back in its usual state first, silently (the offer is made again if the world still wants it).
    private void EndStaleLandOffer(string npc)
    {
        if (IsLandOfferState(_npcs.StateOf(npc)))
        {
            _ = _npcs.HostTurn(npc, NpcHostEvents.Of(npc, NpcHostEvents.OfferGone), StoredTime, StateDigest());
        }
    }

    // A turn the session applied (AfterTurn): a line that took the NPC out of its offering state answered the offer (the host events it raised did the rest); one that kept it there («¿qué?»,
    // «no sé», «¿cuánto cuesta?») leaves the offer standing. A turn that was on the worker when the panel closed still counts.
    private void LandOfferAfter(GuideTurn turn)
    {
        if (_landOffer is not { } npc || turn.Unsure || !string.Equals(npc, turn.Npc, StringComparison.Ordinal))
        {
            return;
        }

        if (IsLandOfferState(turn.AgentState))
        {
            RetryNote = Locale.Ui(string.Equals(turn.AgentState, NpcHostEvents.StakeOfferState, StringComparison.Ordinal) ? "landStakeAsk" : "landPlotAsk", ("npc", NameOf(npc)));
            return;
        }

        _landOffer = null;
        RetryNote = null;
        if (PendingTurns == 0)
        {
            ReleaseClass(npc);
        }
    }
}
