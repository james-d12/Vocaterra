using System.Globalization;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;

namespace Vocaterra.Game.Core.Language.Phrases;

/// <summary>Where the review deck stands (X06, R06).</summary>
public enum DeckStatus
{
    /// <summary>Nothing is on offer and nothing is running.</summary>
    Idle,

    /// <summary>The guide has offered a warm-up at the talk just opened; the learner has not answered the offer yet.</summary>
    Offered,

    /// <summary>The learner asked for a review from the book ("Review now"); it starts when they talk to the guide.</summary>
    Pending,

    /// <summary>The warm-up is under way: the guide asks the items one after another.</summary>
    Running,

    /// <summary>The warm-up ended (every item answered or skipped, or the learner skipped the offer or left).</summary>
    Finished,
}

/// <summary>
/// The review deck (X06, R06): a short warm-up of the phrases that are due. At the start of a session (the second and later, or a loaded game) the guide offers it at the
/// first talk; the learner accepts or skips (never gated, not called a test). The book's "Review now" asks for the same warm-up and it starts at the guide. Each item is the
/// guide's review of a due phrase (<see cref="PhraseReview"/>: her request line, no card, the answer graded like any review), so the core's grading, the help key, "no sé" and
/// the second try of "no sé" work as they do everywhere, and the credit flows through <see cref="PhraseProgress"/> and its spacing gaps (D-game-6): the deck invents no credit
/// of its own. Plain state with no engine type, no clock and no random numbers; the only time is the stored time the session owns.
/// </summary>
/// <remarks>
/// <para>
/// <b>Which items.</b> <see cref="Select"/>: the phrases <see cref="PhraseProgress.DuePhrases"/> lists (a credited use, its help not yet gone, its gap passed or the play session
/// changed), so nothing the learner has not met is ever asked; the guide must have a request line for the phrase (<see cref="PhraseReviews.Create"/>). The most overdue first, then
/// the phrase with the most "no sé" uses, then the phrase sheet's order; at most <see cref="MaxItems"/>.
/// </para>
/// <para>
/// <b>When.</b> The offer is made once per play session, at the first talk with the guide when no lesson holds the conversation, no review is open and an item is due, and only
/// when <see cref="Enabled"/> (the front-end turns it on). It is not saved: a new play session offers again.
/// </para>
/// </remarks>
public sealed class ReviewDeck
{
    /// <summary>The most items a warm-up asks (the brief asks for three to five: a few, never homework).</summary>
    public const int MaxItems = 4;

    /// <summary>The id the strip's view and offer of the warm-up carry in place of a lesson's.</summary>
    public const string StripId = "warm-up";

    private readonly GuideSession _session;
    private readonly List<string> _items = [];
    private readonly Queue<string> _lines = new();
    private readonly Queue<string> _notes = new();
    private string? _npc;
    private string? _first;
    private bool _offered;
    private bool _claim;
    private int _index;
    private int _reviewed;
    private int _version;

    internal ReviewDeck(GuideSession session) => _session = session;

    /// <summary>
    /// Whether the guide makes the offer by herself at the first talk of a session. Off by default so a headless game talks to the guide as before; the front-end switches it on.
    /// "Review now" works either way.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>Where the deck stands.</summary>
    public DeckStatus Status { get; private set; }

    /// <summary>The phrases of the warm-up on offer or under way, in the order they are asked.</summary>
    public IReadOnlyList<string> Items => _items;

    /// <summary>The item in progress, from 0 (the number of items when the warm-up ended).</summary>
    public int Index => _index;

    /// <summary>How many items the learner has answered in this warm-up.</summary>
    public int Reviewed => _reviewed;

    /// <summary>A number that changes whenever what the strip draws changes.</summary>
    public int Version => _version;

    /// <summary>Whether the guide has offered the warm-up in this play session (it offers at most once).</summary>
    public bool OfferedThisSession => _offered;

    /// <summary>How many phrases a warm-up would ask now (at most <see cref="MaxItems"/>; 0 when nothing the guide can ask is due).</summary>
    public int Ready => Select(_session.PhraseProgress, _session.StoredTime.UnixSeconds, _session.Credit, Askable).Count;

    /// <summary>The phrase the warm-up was asked to begin with ("Review now" on a due row), or <see langword="null"/>.</summary>
    public string? FirstWanted => _first;

    /// <summary>
    /// The phrases a warm-up would ask now: the due ones the guide has a request line for, the most overdue first, then those with the most "no sé" uses, then the sheet's
    /// order, at most <paramref name="max"/>; <paramref name="first"/> goes to the front when it is among them.
    /// </summary>
    /// <param name="progress">The phrase progress.</param>
    /// <param name="now">The stored time in Unix seconds.</param>
    /// <param name="rules">The credit rules (the gaps).</param>
    /// <param name="askable">Whether the guide can ask a phrase (it has a request line).</param>
    /// <param name="max">The most items.</param>
    /// <param name="first">A phrase to put first, or <see langword="null"/>.</param>
    public static IReadOnlyList<string> Select(PhraseProgress progress, long now, CreditRules rules, Func<string, bool> askable, int max = MaxItems, string? first = null)
    {
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(askable);
        var due = progress.DuePhrases(now, rules).Where(d => askable(d.PhraseId)).ToList();
        var ordered = due
            .Select((d, i) => (Due: d, Order: i))
            .OrderByDescending(static x => x.Due.OverdueSeconds)
            .ThenByDescending(x => progress.RecordOf(x.Due.PhraseId).DontKnow)
            .ThenBy(static x => x.Order)
            .Select(static x => x.Due.PhraseId)
            .ToList();
        if (first is not null && ordered.Remove(first))
        {
            ordered.Insert(0, first);
        }

        return ordered.Take(Math.Max(0, max)).ToList();
    }

    /// <summary>The warm-up on offer at an NPC's panel as the strip draws it (a lesson offer with the warm-up flag), or <see langword="null"/> when none is on offer there.</summary>
    /// <param name="npc">The NPC the panel is open on.</param>
    public LessonOffer? OfferFor(string npc)
    {
        ArgumentNullException.ThrowIfNull(npc);
        if (Status != DeckStatus.Offered || !string.Equals(npc, _npc, StringComparison.Ordinal))
        {
            return null;
        }

        var locale = _session.Locale;
        return new LessonOffer(StripId, locale.Ui("warmupTitle"), locale.Plural("ui.warmupOffer", _items.Count), false, Warmup: true);
    }

    /// <summary>The item in progress as the strip draws it (a review step of a lesson named Warm-up), or <see langword="null"/> when the warm-up is not running at this NPC.</summary>
    /// <param name="npc">The NPC the panel is open on.</param>
    public LessonView? ViewFor(string npc)
    {
        ArgumentNullException.ThrowIfNull(npc);
        if (Status != DeckStatus.Running || !string.Equals(npc, _npc, StringComparison.Ordinal) || _index >= _items.Count)
        {
            return null;
        }

        var english = _session.Locale;
        return new LessonView(
            StripId,
            english.Ui("warmupTitle"),
            _index + 1,
            _items.Count,
            LessonStepType.Review,
            english.Ui("stepReview"),
            english.Ui("warmupAsk"),
            null,
            null,
            null,
            [],
            -1,
            false,
            true,
            false,
            []);
    }

    /// <summary>The objective line while a review asked for from the book waits for the talk with the guide, or <see langword="null"/>.</summary>
    public string? PendingHud => Status == DeckStatus.Pending ? _session.Locale.Ui("reviewNowHud", ("npc", _session.Locale.NpcName(NpcService.GuideAgent).ToLowerInvariant())) : null;

    /// <summary>Takes the Spanish request lines the front-end is to add to the panel (the guide asking the next item), oldest first.</summary>
    public IReadOnlyList<string> TakeLines()
    {
        var lines = _lines.ToArray();
        _lines.Clear();
        return lines;
    }

    /// <summary>Takes the English notes the front-end is to show (the warm-up is done), oldest first.</summary>
    public IReadOnlyList<string> TakeNotes()
    {
        var notes = _notes.ToArray();
        _notes.Clear();
        return notes;
    }

    /// <summary>The learner accepts the offer: the guide asks the first item. Returns whether a warm-up began.</summary>
    public bool Accept()
    {
        if (Status != DeckStatus.Offered)
        {
            return false;
        }

        return Begin(announce: true);
    }

    /// <summary>The learner skips the offer or the rest of the warm-up under way. Returns whether anything was skipped.</summary>
    public bool SkipAll()
    {
        if (Status is not (DeckStatus.Offered or DeckStatus.Running or DeckStatus.Pending))
        {
            return false;
        }

        Finish(note: false);
        return true;
    }

    /// <summary>The learner skips the item in progress (the next one is asked, or the warm-up ends). Returns whether an item was skipped.</summary>
    public bool SkipItem()
    {
        if (Status != DeckStatus.Running)
        {
            return false;
        }

        _session.LogEvent(new WorldEvent(_session.CurrentTick, _session.StoredTime.UnixSeconds, WorldEvents.ReviewSkipped, _npc, _items[_index]));
        Advance();
        return true;
    }

    /// <summary>
    /// "Review now" (the book's button, X06): asks for the warm-up; it starts at the next talk with the guide, as a lesson practised again does. Refused (<see langword="false"/>)
    /// when nothing is due, or a warm-up is already under way.
    /// </summary>
    /// <param name="first">A phrase to ask first (the due row whose button was pressed), or <see langword="null"/>.</param>
    public bool Request(string? first = null)
    {
        if (Status == DeckStatus.Running || Select(_session.PhraseProgress, _session.StoredTime.UnixSeconds, _session.Credit, Askable, 1).Count == 0)
        {
            return false;
        }

        _first = first;
        Status = DeckStatus.Pending;
        _version++;
        _session.LessonReplayAsked(NpcService.GuideAgent); // the beacon goes to the guide
        return true;
    }

    // ---- What the session tells the deck.

    // The session asks before it opens the guide's panel whether the deck is to have this talk: then the guide does not open with her own review question or word review.
    internal bool Claims(string npc)
    {
        _claim = string.Equals(npc, NpcService.GuideAgent, StringComparison.Ordinal) && !_session.Lessons.OwnsConversation(npc) && !_session.Lessons.Engaged
            && (Status == DeckStatus.Pending || (Enabled && !_offered && Status == DeckStatus.Idle && _session.PlaySession >= 2 && Select(_session.PhraseProgress, _session.StoredTime.UnixSeconds, _session.Credit, Askable, 1).Count > 0));
        return _claim;
    }

    // The panel opened on an NPC and the lesson and the second try have had their say.
    internal void OnTalkOpened(string npc)
    {
        var claimed = _claim;
        _claim = false;
        if (Status is DeckStatus.Offered or DeckStatus.Running)
        {
            _ = SkipAll(); // a warm-up left standing at another talk is over (never gating)
        }

        if (!claimed || _session.PhraseReview is not null || _session.Review is not null || _session.PendingTurns > 0 || _session.Lessons.Engaged)
        {
            return;
        }

        _npc = npc;
        if (Status == DeckStatus.Pending)
        {
            _ = Begin(announce: false);
            return;
        }

        PickItems(null);
        if (_items.Count == 0)
        {
            return;
        }

        _offered = true;
        Status = DeckStatus.Offered;
        _version++;
        _session.LogEvent(new WorldEvent(_session.CurrentTick, _session.StoredTime.UnixSeconds, WorldEvents.WarmupOffered, npc, string.Join(", ", _items)));
    }

    // The panel closed: an offer or a warm-up left standing is over.
    internal void OnTalkClosed()
    {
        _claim = false;
        if (Status is DeckStatus.Offered or DeckStatus.Running)
        {
            Finish(note: false);
        }
    }

    // The learner's answer to the review in progress was understood (not a request for help).
    internal void AfterAnswer(PhraseReview answered)
    {
        if (Status != DeckStatus.Running || _index >= _items.Count || !string.Equals(_items[_index], answered.PhraseId, StringComparison.Ordinal))
        {
            return;
        }

        _reviewed++;
        _session.NoteReviewed(answered.PhraseId, "warm-up");
        Advance();
    }

    // ---- The deck's own steps.

    private bool Askable(string phraseId) => PhraseReviews.Create(phraseId, _session.Phrases, _session.Bundle) is not null;

    private void PickItems(string? first)
    {
        _items.Clear();
        _items.AddRange(Select(_session.PhraseProgress, _session.StoredTime.UnixSeconds, _session.Credit, Askable, MaxItems, first));
    }

    private bool Begin(bool announce)
    {
        PickItems(_first);
        _first = null;
        _index = 0;
        _reviewed = 0;
        if (_items.Count == 0)
        {
            Finish(note: false);
            return false;
        }

        Status = DeckStatus.Running;
        _session.LogEvent(new WorldEvent(_session.CurrentTick, _session.StoredTime.UnixSeconds, WorldEvents.WarmupStarted, _npc, string.Join(", ", _items)));
        Ask(announce);
        _version++;
        return true;
    }

    private void Advance()
    {
        _index++;
        _version++;
        if (_index >= _items.Count)
        {
            Finish(note: true);
            return;
        }

        Ask(announce: true);
    }

    // The guide asks the item in progress: her request line goes to the panel and is spoken, and the answer is graded as a review (no card).
    private void Ask(bool announce)
    {
        while (_index < _items.Count)
        {
            if (PhraseReviews.Create(_items[_index], _session.Phrases, _session.Bundle) is { } review)
            {
                _session.AskDeckReview(review);
                if (announce)
                {
                    _lines.Enqueue(review.Request); // at the talk's own start the panel shows it with the opening (ReviewLine)
                }

                _session.Support.Enqueue(new AudioCue(_npc ?? NpcService.GuideAgent, review.Request, null, "review"));
                return;
            }

            _index++;
        }

        Finish(note: true);
    }

    private void Finish(bool note)
    {
        var was = Status;
        Status = DeckStatus.Finished;
        if (was == DeckStatus.Running)
        {
            _session.AskDeckReview(null); // only the deck's own item: a review it did not ask (the guide's own, a second try) stays open
        }

        _version++;
        if (was == DeckStatus.Running)
        {
            _session.LogEvent(new WorldEvent(_session.CurrentTick, _session.StoredTime.UnixSeconds, WorldEvents.WarmupEnded, _npc, string.Create(CultureInfo.InvariantCulture, $"{_reviewed} of {_items.Count}")));
            if (note)
            {
                _notes.Enqueue(_session.Locale.Plural("ui.warmupDone", _reviewed));
            }
        }
        else if (was == DeckStatus.Offered)
        {
            _session.LogEvent(new WorldEvent(_session.CurrentTick, _session.StoredTime.UnixSeconds, WorldEvents.WarmupSkipped, _npc, null));
        }
    }
}
