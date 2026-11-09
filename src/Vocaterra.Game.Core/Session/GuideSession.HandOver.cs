using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Game.Core.Session;

// The hand-over (issue #42): an order whose file names handOver is not delivered by walking up with its goods. The NPC asks for them when the talk opens
// (<npc>.order_ready puts its agent in its receiving state, whose prompt takes hand_over), and the learner says what they give: «Aquí tienes la madera». A line
// that names one of the order's nouns delivers it (<npc>.delivered: the thanks and the reward, as the walk-up did). Rule 8 holds (D-game-8, D-game-9): «no sé»
// or HandOverMisses missed lines hand the goods over anyway, and an unsure recognition is never a miss.
public abstract partial class GuideSession
{
    /// <summary>How many missed lines at an NPC's receiving prompt hand the goods over anyway (issue #42, rule 8): two.</summary>
    public const int HandOverMisses = 2;

    // The order the NPC the panel is open on waits to be handed, while it waits; null otherwise. A talk asks again from the start.
    private HandOverWait? _handOver;

    // A hand-over decided while another turn was still on the worker: it is done once that turn is in (Collect), and its thanks follow as an opening.
    private (HandOverWait Wait, bool FailForward)? _handOverDue;

    // The talk opened on an NPC waiting for the goods, so its class was held back (OpenTalk): it is offered, resumed or started once the goods are handed over.
    private bool _classAfterHandOver;

    /// <summary>
    /// Whether the NPC the panel is open on waits for the learner to hand an order over (issue #42): nothing else takes the talk meanwhile. A lesson engaged at the NPC
    /// waits too: the next line is evaluated at the NPC's receiving prompt, not the step's, the card is the hand-over's, and Ayuda and No sé go to the NPC. A class
    /// at the NPC is not offered, started or resumed at the opening of such a talk: it follows the hand-over.
    /// </summary>
    public bool HandOverOpen => _handOver is not null;

    // The prompt of the lesson step that waits for a turn, unless a hand-over waits for the next line (issue #42).
    private string? LessonPrompt => HandOverOpen ? null : Lessons.ActivePrompt;

    // Whether a turn is a line said to the NPC that waits for the goods, at its receiving prompt.
    private bool IsHandOverTurn(GuideTurn turn) =>
        _handOver is { } wait && string.Equals(turn.Npc, wait.Npc, StringComparison.Ordinal) && string.Equals(turn.Prompt, wait.Prompt, StringComparison.Ordinal);

    /// <summary>
    /// The nouns that name the goods of an order handed over in Spanish (issue #42, <c>handOver</c> in its file), or <see langword="null"/> when walking up with the
    /// goods is enough. The 3D scene has the objectives.
    /// </summary>
    /// <param name="order">The order's objective id.</param>
    protected virtual IReadOnlyList<string>? HandOverNounsOf(string order) => null;

    // The talk opens on an NPC with an order's goods (OpenNow): when the order is handed over in Spanish and the agent asks for it, the opening is that question.
    // Returns null when the walk-up delivers it as before.
    private NpcOpening? BeginHandOver(string npc, string order)
    {
        if (HandOverNounsOf(order) is not { Count: > 0 } nouns
            || _npcs.HostTurn(npc, NpcHostEvents.Of(npc, NpcHostEvents.OrderReady), StoredTime, StateDigest()) is not { } asked)
        {
            return null;
        }

        _handOver = new HandOverWait(npc, order, nouns, asked.Prompt);
        RetryNote = Locale.Ui("handOverAsk", ("npc", NameOf(npc)));
        Log(new WorldEvent(Tick, StoredTime.UnixSeconds, WorldEvents.OrderAsked, npc, order));
        return asked;
    }

    // The talk opens with no goods to hand over on an NPC a load or an earlier talk left asking for them (its receiving state): it is back in its usual state first.
    private void EndStaleHandOver(string npc)
    {
        if (string.Equals(_npcs.StateOf(npc), NpcHostEvents.ReceivingState, StringComparison.Ordinal))
        {
            _ = _npcs.HostTurn(npc, NpcHostEvents.Of(npc, NpcHostEvents.OrderGone), StoredTime, StateDigest());
        }
    }

    // A turn the session applied (AfterTurn): a line said at the waiting NPC's receiving prompt hands the order over, asks for help, or misses. Returns the turn as
    // the player is to see it: with the NPC's thanks after it once the goods are taken, and an English note on a miss.
    private GuideTurn HandOverAfter(GuideTurn turn, GuideTurn shown)
    {
        if (_handOver is not { } wait || !string.Equals(turn.Npc, wait.Npc, StringComparison.Ordinal) || !string.Equals(turn.Prompt, wait.Prompt, StringComparison.Ordinal)
            || turn.Unsure || (turn.Uncertain && turn.Input == InputKind.Spoken))
        {
            return shown; // another NPC, a lesson's or a review's prompt, or the microphone's trouble (D-game-9): no attempt
        }

        var handed = turn.Communicated && turn.HostEvents.Contains(NpcHostEvents.Of(wait.Npc, NpcHostEvents.HandOver), StringComparer.Ordinal);
        if (handed && NamesGoods(turn, wait.Nouns))
        {
            return Deliver(wait, shown, failForward: false);
        }

        if (turn.Communicated && string.Equals(turn.Intent, "ask_help", StringComparison.Ordinal))
        {
            return shown; // the NPC's help line models the answer and the card goes up (AfterTurn): never a miss
        }

        if (turn.Communicated && string.Equals(turn.Intent, "dont_know", StringComparison.Ordinal))
        {
            return Deliver(wait, shown, failForward: true); // «no sé» is never shamed: the model is said, and the goods go
        }

        wait.Misses++;
        if (handed)
        {
            shown = shown with { Replies = [] }; // not «¡Muy bien!» for goods the NPC did not ask for
        }

        if (wait.Misses >= HandOverMisses)
        {
            return Deliver(wait, shown, failForward: true);
        }

        var note = Locale.Ui(handed ? "handOverWrongGoods" : "handOverRetry", ("npc", NameOf(wait.Npc)), ("spanish", ModelOf(wait)));
        return shown with { EnglishNotes = [.. shown.EnglishNotes, note] };
    }

    // The order goes: the NPC's thanks (<npc>.delivered from its receiving state), the goods before the reward, and order_delivered for the order's step. With a turn
    // still on the worker the NPC's state may yet change, so it waits for that turn (CompleteHandOver) and its thanks come as an opening.
    private GuideTurn Deliver(HandOverWait wait, GuideTurn shown, bool failForward)
    {
        _handOver = null;
        if (PendingTurns > 0)
        {
            _handOverDue = (wait, failForward);
            return shown;
        }

        if (HandOverNow(wait, failForward) is not { } thanks)
        {
            return shown;
        }

        IReadOnlyList<string> notes = failForward ? [.. shown.EnglishNotes, Locale.Ui("handOverTaken", ("npc", NameOf(wait.Npc)), ("spanish", ModelOf(wait)))] : shown.EnglishNotes;
        return shown with { FollowUp = [.. shown.FollowUp ?? [], .. thanks.Lines], FailForward = shown.FailForward || failForward, EnglishNotes = notes };
    }

    // The delivery itself, on the game's thread with no turn on the worker. Returns the NPC's thanks, or null when the goods are gone or its agent does not take it.
    private NpcOpening? HandOverNow(HandOverWait wait, bool failForward)
    {
        if (!string.Equals(OrderReadyAt(wait.Npc), wait.Order, StringComparison.Ordinal))
        {
            EndStaleHandOver(wait.Npc); // the goods left the bag while the NPC waited (eaten, dropped): it asks no more
            ReleaseClass(wait.Npc);
            return null;
        }

        var thanks = _npcs.HostTurn(wait.Npc, NpcHostEvents.Of(wait.Npc, NpcHostEvents.Delivered), StoredTime, StateDigest());
        if (thanks is null)
        {
            return null;
        }

        TakeOrder(wait.Npc, wait.Order); // the goods go before the reward comes in
        ApplyHostEvents(wait.Npc, thanks.HostEvents, failForward ? "fail_forward" : "order", SlotMap.Empty);
        if (failForward && Data.Npcs.FocusAt(wait.Prompt).FirstOrDefault() is { } phrase)
        {
            Log(new PhraseUse(Tick, StoredTime.UnixSeconds, phrase, PhraseUseKind.FailForward, wait.Npc, wait.Prompt, string.Empty, wait.Order));
        }

        Log(new WorldEvent(Tick, StoredTime.UnixSeconds, WorldEvents.OrderDelivered, wait.Npc, wait.Order)); // the order's one step waits for it
        if (thanks.Lines.Count > 0)
        {
            _lastLine[wait.Npc] = thanks.Lines[^1];
        }

        ReleaseClass(wait.Npc);
        return thanks;
    }

    // The class held back at the opening (OpenTalk) comes now, after the thanks, in the same talk: offered, resumed or started as at an opening.
    private void ReleaseClass(string npc)
    {
        if (_classAfterHandOver && TalkOpenOn(npc))
        {
            _classAfterHandOver = false;
            Lessons.OnTalkOpened(npc);
        }
    }

    // A hand-over that waited for a turn on the worker (Deliver), once no turn is left: its thanks are the opening the panel shows next (TakeOpening).
    private void CompleteHandOver()
    {
        if (PendingTurns > 0 || _handOverDue is not { } due)
        {
            return;
        }

        _handOverDue = null;
        if (HandOverNow(due.Wait, due.FailForward) is { } thanks && _talkOpen)
        {
            _delayedOpening = thanks;
        }
    }

    // Whether the line names one of the order's goods: its item slot, or any noun it says (the second of «la madera y las monedas»).
    private static bool NamesGoods(GuideTurn turn, IReadOnlyList<string> nouns) =>
        (turn.Slots.TryGetValue("item", out var item) && nouns.Contains(item, StringComparer.Ordinal))
        || (turn.Words ?? []).Any(word => word.Lemma is { } lemma && string.Equals(word.Pos, "noun", StringComparison.Ordinal) && nouns.Contains(lemma, StringComparer.Ordinal));

    // The Spanish the notes model: the phrase the receiving prompt practises (its card; the validator checks there is one), else the nouns.
    private string ModelOf(HandOverWait wait) =>
        Data.Npcs.FocusAt(wait.Prompt).Select(Phrases.Find).OfType<PhraseRow>().FirstOrDefault()?.Spanish ?? string.Join(", ", wait.Nouns);

    private string NameOf(string npc) => Data.Npcs.Find(npc)?.Name ?? Data.Npcs.LabelOf(npc);

    // An order an NPC waits to be handed: its NPC, its id, the nouns that name its goods, the prompt the answer is evaluated at, and the lines missed so far.
    private sealed class HandOverWait(string npc, string order, IReadOnlyList<string> nouns, string prompt)
    {
        public string Npc { get; } = npc;

        public string Order { get; } = order;

        public IReadOnlyList<string> Nouns { get; } = nouns;

        public string Prompt { get; } = prompt;

        public int Misses { get; set; }
    }
}
