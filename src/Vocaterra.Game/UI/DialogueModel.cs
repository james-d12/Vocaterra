using System.Globalization;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tip;
using Vocaterra.Game.Input;

namespace Vocaterra.Game.UI;

/// <summary>Who a line of the dialogue panel belongs to; the panel colours them differently.</summary>
public enum DialogueSpeaker
{
    /// <summary>What the player typed.</summary>
    Player,

    /// <summary>What the player said, as the recognizer heard it.</summary>
    PlayerSpoken,

    /// <summary>The verdict summary and the time of the turn (developer information, dim).</summary>
    Note,

    /// <summary>The guide's correction of the player's line (a recast, a hint or a correction).</summary>
    Correction,

    /// <summary>The NPC's own reply (the guide's, the shopkeeper's or the villager's: the panel's title says whose).</summary>
    Guide,

    /// <summary>The game's line with the world facts.</summary>
    World,

    /// <summary>A hint from the game about the player's progress (for example why a word did not gain colour).</summary>
    Hint,

    /// <summary>The plain-English feedback after a turn (S16, D-game-13): what happened and what to do, drawn in its own colour and marked as English. Never spoken.</summary>
    English,
}

/// <summary>A scripted spoken line waiting to be sent (<c>--wav</c>, <c>--speak</c>): the game submits it to the worker between ticks.</summary>
/// <param name="Source">A WAV file or a line to synthesize.</param>
/// <param name="Value">The file path, or the text to speak.</param>
public sealed record SpeechRequest(TurnSource Source, string Value);

/// <summary>What the dialogue panel is doing with the keyboard: showing the speech buttons (the keys R, S, H, N, W, 1 to 3 press them, V or Space talks) or the text box.</summary>
public enum PanelMode
{
    /// <summary>The speech buttons; the keys are shortcuts, nothing is typed.</summary>
    Buttons,

    /// <summary>The text box has the keyboard (the Escribir button, Tab or W opened it).</summary>
    Typing,
}

/// <summary>One line of the dialogue panel.</summary>
/// <param name="Speaker">Whose line it is.</param>
/// <param name="Text">The text.</param>
public sealed record DialogueLine(DialogueSpeaker Speaker, string Text);

/// <summary>
/// The dialogue panel as plain state, with no engine type: whether it is open, the text box, the lines shown so far and a request to send.
/// The panel layer draws it and the game feeds it keys; keeping it here makes the typing, the focus and the turn flow testable without a window.
/// Opening it takes the keyboard from the player (<see cref="InputFocus"/>) and closing gives it back.
/// </summary>
public sealed class DialogueModel
{
    /// <summary>How many lines are kept; older ones are dropped.</summary>
    public const int MaxLines = 60;

    private readonly List<DialogueLine> _lines = [];
    private readonly InputFocus _focus;
    private readonly string _openingLine;
    private readonly HashSet<string> _opened = new(StringComparer.Ordinal);
    private readonly PhraseSheet? _phrases;
    private readonly EnglishText? _english;
    private readonly bool _showVerdictNotes;
    private int _cancelled;

    /// <summary>The text of the hint line that offers typing (the watchdog, or two uncertain recognitions in a row).</summary>
    public const string TypeOfferText = "¿Escribes?";

    /// <summary>How many uncertain spoken lines in a row make the panel offer typing.</summary>
    public const int TypingOfferStreak = 2;

    /// <summary>Creates a closed panel.</summary>
    /// <param name="focus">Who has the keyboard; the model sets <see cref="InputFocus.TextEntryActive"/> while it is open.</param>
    /// <param name="openingLine">What the guide says the first time the panel opens (the prompt's text).</param>
    /// <param name="phrases">The phrase sheet, for the heard-versus-expected line and the labels of the offered choices; without it neither is shown.</param>
    /// <param name="english">The game's English text, for the plain-English feedback after a turn (S16); without it no feedback line is added.</param>
    /// <param name="showVerdictNotes">Whether each turn adds its verdict line (the core's summary, the confidence and the milliseconds, for example
    /// <c>greet, understood, 100 %, 3.5 ms</c>). A playtest hides it (S18); scripted and developer runs keep it.</param>
    public DialogueModel(InputFocus focus, string openingLine, PhraseSheet? phrases = null, EnglishText? english = null, bool showVerdictNotes = true)
    {
        ArgumentNullException.ThrowIfNull(focus);
        ArgumentNullException.ThrowIfNull(openingLine);
        _focus = focus;
        _openingLine = openingLine;
        _phrases = phrases;
        _english = english;
        _showVerdictNotes = showVerdictNotes;
    }

    /// <summary>Whether the panel is open (and so has the keyboard).</summary>
    public bool IsOpen { get; private set; }

    /// <summary>The NPC the panel is open on (its agent id); the guide until another NPC is opened.</summary>
    public string Npc { get; private set; } = NpcService.GuideAgent;

    /// <summary>The name of that NPC as the panel's title shows it (<c>Guía</c>, <c>Tendero</c>, <c>Aldeano</c>).</summary>
    public string NpcName { get; private set; } = "Guía";

    /// <summary>The text box.</summary>
    public TextEntry Entry { get; } = new();

    /// <summary>The lines so far, oldest first.</summary>
    public IReadOnlyList<DialogueLine> Lines => _lines;

    /// <summary>The last turn the guide took, or <see langword="null"/>.</summary>
    public GuideTurn? LastTurn { get; private set; }

    /// <summary>How long the last turn took to evaluate, in milliseconds (measured by the caller, shown in the note line); zero before any.</summary>
    public double LastTurnMilliseconds { get; private set; }

    /// <summary>The number of turns taken.</summary>
    public int TurnCount { get; private set; }

    /// <summary>Turns sent to the worker whose answer has not come back (the cancelled ones too, until their outcome arrives).</summary>
    public int Pending { get; private set; }

    /// <summary>Whether a turn that has not been cancelled is on the worker: the panel shows "Pensando…" and Esc cancels it.</summary>
    public bool IsThinking => Pending > _cancelled;

    /// <summary>Whether the panel shows the speech buttons or the text box.</summary>
    public PanelMode Mode { get; private set; } = PanelMode.Buttons;

    /// <summary>Whether the text box has the keyboard.</summary>
    public bool IsTyping => IsOpen && Mode == PanelMode.Typing;

    /// <summary>The persistent <c>Heard: «…» · Expected: «…»</c> line of the last spoken turn, or <see langword="null"/> (no spoken turn yet at this NPC, or the last turn was typed).</summary>
    public HeardLine? Heard { get; private set; }

    /// <summary>The choices the repair ladder offered with the last turn, as buttons; empty when none.</summary>
    public IReadOnlyList<ChoiceOption> Choices { get; private set; } = [];

    /// <summary>Whether typing is on offer ("¿Escribes?"): the Escribir button is focused and Enter presses it.</summary>
    public bool TypingOffered { get; private set; }

    /// <summary>How many spoken lines in a row were uncertain recognitions.</summary>
    public int UncertainStreak { get; private set; }

    /// <summary>The index in <see cref="Lines"/> of the NPC line the Repetir or Más despacio button showed again, or -1.</summary>
    public int HighlightedLine { get; private set; } = -1;

    /// <summary>The simulation tick until which <see cref="HighlightedLine"/> is drawn highlighted.</summary>
    public long HighlightUntilTick { get; private set; }

    /// <summary>Whether the push-to-talk key is held and the microphone is recording: the panel shows "Listening…".</summary>
    public bool Listening { get; set; }

    /// <summary>Whether the speech models are not loaded, so the talk key does nothing and the hints say to type (S17b). The game sets it each frame.</summary>
    public bool SpeechUnavailable { get; set; }

    /// <summary>The English message about the microphone or the speech models (none present, silence heard), shown in the title bar until the next recording, or <see langword="null"/>.</summary>
    public string? SpeechNotice { get; set; }

    /// <summary>A scripted spoken line waiting to be submitted, or <see langword="null"/>.</summary>
    public SpeechRequest? PendingSpeech { get; private set; }

    /// <summary>The seconds of a scripted microphone recording (<c>--listen</c>) waiting to start, or <see langword="null"/>.</summary>
    public int? PendingListenSeconds { get; private set; }

    /// <summary>Whether Enter was pressed on a line: the game then evaluates it, between ticks, and calls <see cref="Submit"/>.</summary>
    public bool SubmitRequested { get; private set; }

    /// <summary>
    /// Opens the panel on the guide and gives it the keyboard. The first time, the guide's opening line is added; when the guide opens with a review
    /// question, that is added as her line (every time the panel opens with one).
    /// </summary>
    /// <param name="reviewQuestion">The review question ("¿Qué es esto?"), or <see langword="null"/> when no word is due.</param>
    public void Open(string? reviewQuestion = null)
    {
        if (IsOpen)
        {
            return;
        }

        SwitchTo(NpcService.GuideAgent, "Guía");
        IsOpen = true;
        Mode = PanelMode.Buttons;
        _focus.TextEntryActive = true;
        if (_opened.Add(NpcService.GuideAgent))
        {
            Add(DialogueSpeaker.Guide, _openingLine);
        }

        if (reviewQuestion is not null)
        {
            Add(DialogueSpeaker.Guide, reviewQuestion);
        }
    }

    /// <summary>
    /// Opens the panel on an NPC (S6) and gives it the keyboard. The NPC's lines are added when they came from a core host turn (every time) or
    /// when it is the first talk with this NPC or the page is empty (the prompt's text); a talk with another NPC than the last starts a clean page. The guide's review
    /// question, when she asks one, follows her opening.
    /// </summary>
    /// <param name="opening">What the NPC says first (<c>GuideSession.OpenTalk</c>).</param>
    /// <param name="displayName">The NPC's name as the title shows it.</param>
    /// <param name="reviewQuestion">The guide's review question, or <see langword="null"/>.</param>
    /// <param name="englishNote">The English note above the review question (the second try, X04), or <see langword="null"/>.</param>
    public void Open(NpcOpening opening, string displayName, string? reviewQuestion = null, string? englishNote = null)
    {
        ArgumentNullException.ThrowIfNull(opening);
        ArgumentNullException.ThrowIfNull(displayName);
        if (IsOpen)
        {
            return;
        }

        SwitchTo(opening.Npc, displayName);
        IsOpen = true;
        Mode = PanelMode.Buttons;
        _focus.TextEntryActive = true;
        var first = _opened.Add(opening.Npc);
        if (opening.HostTurn || first || _lines.Count == 0)
        {
            foreach (var line in opening.Lines)
            {
                Add(DialogueSpeaker.Guide, line);
            }
        }

        if (englishNote is not null)
        {
            Add(DialogueSpeaker.English, englishNote);
        }

        if (reviewQuestion is not null)
        {
            Add(DialogueSpeaker.Guide, reviewQuestion);
        }
    }

    /// <summary>
    /// Adds the lines of an opening that came after the panel was opened (S8: the shopkeeper's first question follows the key-word cards by itself).
    /// Does nothing when the panel is closed.
    /// </summary>
    /// <param name="opening">What the NPC says now.</param>
    public void AddOpening(NpcOpening opening)
    {
        ArgumentNullException.ThrowIfNull(opening);
        if (!IsOpen)
        {
            return;
        }

        foreach (var line in opening.Lines)
        {
            Add(DialogueSpeaker.Guide, line);
        }
    }

    // A talk with another NPC than the last one starts on a clean page.
    private void SwitchTo(string npc, string displayName)
    {
        if (!string.Equals(Npc, npc, StringComparison.Ordinal))
        {
            _lines.Clear();
            Heard = null;
            Choices = [];
            HighlightedLine = -1;
            UncertainStreak = 0;
        }

        Npc = npc;
        NpcName = displayName;
    }

    /// <summary>Adds a hint line from the game to the panel (for example <c>Bien. Repasa más tarde.</c>).</summary>
    public void AddHint(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Add(DialogueSpeaker.Hint, text);
    }

    /// <summary>Adds a line the open NPC says (S16: a lesson's model or question line), so it is shown, repeated and glossed like any other of the NPC's lines.</summary>
    /// <param name="text">The Spanish line.</param>
    public void AddNpcLine(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (IsOpen)
        {
            Add(DialogueSpeaker.Guide, text);
        }
    }

    /// <summary>Adds a line of English from the game (S16: a lesson's hint), drawn like the feedback after a turn.</summary>
    /// <param name="text">The English text.</param>
    public void AddEnglish(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Add(DialogueSpeaker.English, text);
    }

    /// <summary>Closes the panel, gives the keyboard back to the player and drops a pending send. The lines and the half-typed text stay.</summary>
    public void Close()
    {
        IsOpen = false;
        Mode = PanelMode.Buttons;
        TypingOffered = false;
        SubmitRequested = false;
        PendingSpeech = null;
        PendingListenSeconds = null;
        Listening = false;
        _focus.TextEntryActive = false;
    }

    /// <summary>Asks the game to record from the microphone for a number of seconds, as if the key were held (the demo's <c>--listen</c>). Does nothing when the panel is closed.</summary>
    public void RequestListen(int seconds)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(seconds, 1);
        if (IsOpen)
        {
            PendingListenSeconds = seconds;
        }
    }

    /// <summary>Takes the scripted recording waiting to start (and clears it), or returns <see langword="null"/>.</summary>
    public int? TakeListenRequest()
    {
        var seconds = PendingListenSeconds;
        PendingListenSeconds = null;
        return seconds;
    }

    /// <summary>Asks the game to send a scripted spoken line (the demo's <c>--wav</c> and <c>--speak</c>). Does nothing when the panel is closed.</summary>
    public void RequestSpeech(TurnSource source, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (IsOpen)
        {
            PendingSpeech = new SpeechRequest(source, value);
        }
    }

    /// <summary>Takes the scripted spoken line waiting to be sent (and clears it), or returns <see langword="null"/>.</summary>
    public SpeechRequest? TakeSpeechRequest()
    {
        var request = PendingSpeech;
        PendingSpeech = null;
        return request;
    }

    /// <summary>
    /// Takes the typed line for the worker: shows it as the player's line and counts the turn as pending. Returns <see langword="null"/>
    /// (and sends nothing) when the panel is closed or the box is blank. The answer comes back through <see cref="Complete"/>.
    /// </summary>
    public string? TakeLine()
    {
        SubmitRequested = false;
        if (!IsOpen || Entry.IsBlank)
        {
            return null;
        }

        var line = Entry.Take();
        Add(DialogueSpeaker.Player, line);
        SpeechNotice = null;
        StartTurn();
        return line;
    }

    // A turn goes to the worker: the offered choices and the typing offer are answered, the panel thinks.
    private void StartTurn()
    {
        Pending++;
        Choices = [];
        TypingOffered = false;
    }

    /// <summary>Switches the panel to the text box (the Escribir button, W or Tab). The keyboard types until <see cref="EndTyping"/>.</summary>
    public void BeginTyping()
    {
        if (IsOpen)
        {
            Mode = PanelMode.Typing;
            TypingOffered = false;
        }
    }

    /// <summary>Switches the panel back to the speech buttons (Esc or Tab in the text box). What was typed stays in the box.</summary>
    public void EndTyping() => Mode = PanelMode.Buttons;

    /// <summary>
    /// Offers typing: adds the hint line <see cref="TypeOfferText"/> and focuses the Escribir button (Enter presses it). The watchdog calls it when
    /// a turn takes too long; two uncertain recognitions in a row call it too. Does nothing when the panel is closed or already typing.
    /// </summary>
    public void OfferTyping()
    {
        if (!IsOpen || Mode == PanelMode.Typing || TypingOffered)
        {
            return;
        }

        TypingOffered = true;
        Add(DialogueSpeaker.Hint, TypeOfferText);
    }

    /// <summary>
    /// Esc while "Pensando…": the turns on the worker are marked cancelled here, so the panel stops thinking at once. Their outcomes still arrive (the
    /// session dropped them) and are counted down by <see cref="Complete"/>. Returns whether there was a turn to cancel.
    /// </summary>
    public bool CancelPending()
    {
        if (!IsThinking)
        {
            return false;
        }

        _cancelled = Pending;
        TypingOffered = false;
        return true;
    }

    /// <summary>
    /// A button or a choice sent a line (the player's own line is shown as what they picked): shows it, counts the turn as pending and answers the
    /// offered choices.
    /// </summary>
    /// <param name="shown">What the panel shows as the player's line (the choice's label, "Ayuda", "No sé").</param>
    public void BeginLine(string shown)
    {
        ArgumentNullException.ThrowIfNull(shown);
        Add(DialogueSpeaker.Player, shown);
        SpeechNotice = null;
        StartTurn();
    }

    /// <summary>
    /// The text of the NPC's last line (what Repetir plays again), or <see langword="null"/> when the NPC has said nothing yet. Corrections and the
    /// game's world lines are not the NPC's speech and are skipped.
    /// </summary>
    public string? LastNpcLine
    {
        get
        {
            for (var i = _lines.Count - 1; i >= 0; i--)
            {
                if (_lines[i].Speaker == DialogueSpeaker.Guide)
                {
                    return _lines[i].Text;
                }
            }

            return null;
        }
    }

    /// <summary>Highlights the NPC's last line until the given tick (Repetir and Más despacio re-show it; S9 plays its audio).</summary>
    /// <param name="untilTick">The simulation tick the highlight ends at.</param>
    /// <returns>Whether there was a line to highlight.</returns>
    public bool HighlightLastNpcLine(long untilTick)
    {
        for (var i = _lines.Count - 1; i >= 0; i--)
        {
            if (_lines[i].Speaker == DialogueSpeaker.Guide)
            {
                HighlightedLine = i;
                HighlightUntilTick = untilTick;
                return true;
            }
        }

        return false;
    }

    /// <summary>Counts a spoken turn sent to the worker as pending (its line is shown when the answer comes back, as the recognizer heard it).</summary>
    public void BeginSpoken()
    {
        SpeechNotice = null;
        StartTurn();
    }

    /// <summary>
    /// Shows a finished turn: for a spoken one the transcript, then the verdict note and the guide's lines. A request that failed shows its
    /// problem as a hint. Returns the turn, or <see langword="null"/> when there was none.
    /// </summary>
    /// <param name="applied">What <c>GuideSession.Collect</c> returned for the request.</param>
    public GuideTurn? Complete(AppliedTurn applied)
    {
        ArgumentNullException.ThrowIfNull(applied);
        Pending = Math.Max(0, Pending - 1);
        if (applied.Outcome.Cancelled)
        {
            _cancelled = Math.Max(0, _cancelled - 1); // dropped by the session: nothing is shown, the turn never happened
            return null;
        }

        TypingOffered = false;
        if (applied.Turn is not { } turn)
        {
            Add(DialogueSpeaker.Hint, applied.Outcome.Problem ?? "I couldn't check that line. Try again.");
            return null;
        }

        if (turn.Input == InputKind.Spoken)
        {
            Add(DialogueSpeaker.PlayerSpoken, turn.Line);
        }

        Show(turn, applied.Outcome.Timings.TotalMilliseconds);
        return turn;
    }

    /// <summary>Asks the game to send the typed line (Enter). Does nothing when the box is blank or the panel is closed.</summary>
    public void RequestSubmit()
    {
        if (IsOpen && !Entry.IsBlank)
        {
            SubmitRequested = true;
        }
    }

    /// <summary>
    /// Sends the typed line: shows it, calls <paramref name="say"/> for the guide's turn, and shows the verdict note and the guide's lines.
    /// Returns the turn, or <see langword="null"/> when the panel is closed or the box is blank.
    /// </summary>
    /// <param name="say">The game's call to the guide for one line.</param>
    /// <param name="elapsedMilliseconds">Reads how long the call took, after it returns (the caller owns the timer).</param>
    public GuideTurn? Submit(Func<string, GuideTurn> say, Func<double>? elapsedMilliseconds = null)
    {
        ArgumentNullException.ThrowIfNull(say);
        SubmitRequested = false;
        if (!IsOpen || Entry.IsBlank)
        {
            return null;
        }

        var line = Entry.Take();
        Add(DialogueSpeaker.Player, line);
        var turn = say(line);
        Show(turn, elapsedMilliseconds?.Invoke() ?? 0);
        return turn;
    }

    private void Show(GuideTurn turn, double milliseconds)
    {
        LastTurn = turn;
        LastTurnMilliseconds = milliseconds;
        TurnCount++;
        Heard = _phrases is null ? null : HeardLine.For(turn, _phrases);
        Choices = turn.Choices is { Count: > 0 } offered && _phrases is not null ? _phrases.OptionsFor(offered) : [];
        UncertainStreak = turn.Uncertain ? UncertainStreak + 1 : 0;
        var heard = turn.Confidence is { } confidence
            ? string.Create(CultureInfo.InvariantCulture, $"heard at {(int)Math.Round(confidence * 100, MidpointRounding.AwayFromZero)} %, ")
            : string.Empty;
        var summary = turn.Uncertain ? "not understood" : turn.Summary; // an unsure recognition has no verdict to summarize
        if (_showVerdictNotes)
        {
            Add(DialogueSpeaker.Note, string.Create(CultureInfo.InvariantCulture, $"{heard}{summary}, {LastTurnMilliseconds:0.0} ms"));
        }

        if (turn.Feedback is not null)
        {
            Add(DialogueSpeaker.Correction, turn.Feedback);
        }

        // A turn the lesson took (S17) shows none of the guide's ordinary reply or world line: the lesson speaks for her (see GuideTurn.GuideLines).
        foreach (var reply in turn.LessonHandled ? [] : turn.Replies)
        {
            Add(DialogueSpeaker.Guide, reply);
        }

        if (turn.Repair is not null)
        {
            Add(DialogueSpeaker.Guide, turn.Repair);
        }

        if (turn.WorldLine is not null && !turn.LessonHandled)
        {
            Add(DialogueSpeaker.World, turn.WorldLine);
        }

        if (turn.FollowUp is not null)
        {
            foreach (var line in turn.FollowUp)
            {
                Add(DialogueSpeaker.Guide, line);
            }
        }

        if (_english is not null)
        {
            foreach (var line in _english.FeedbackFor(turn))
            {
                Add(DialogueSpeaker.English, line); // the plain-English feedback after the turn (S16, D-game-13)
            }
        }

        if (UncertainStreak >= TypingOfferStreak)
        {
            OfferTyping();
        }
    }

    private void Add(DialogueSpeaker speaker, string text)
    {
        _lines.Add(new DialogueLine(speaker, text));
        if (_lines.Count > MaxLines)
        {
            var dropped = _lines.Count - MaxLines;
            _lines.RemoveRange(0, dropped);
            HighlightedLine = HighlightedLine < dropped ? -1 : HighlightedLine - dropped;
        }
    }
}

/// <summary>Breaks text into lines that fit a width, measured by a function the engine layer supplies (the font), so the logic is testable.</summary>
public static class TextWrap
{
    /// <summary>
    /// Wraps <paramref name="text"/> at spaces so that no line measures wider than <paramref name="maxWidth"/>; a single word wider than the
    /// width is cut where it no longer fits. An empty text gives one empty line.
    /// </summary>
    public static IReadOnlyList<string> Wrap(string text, float maxWidth, Func<string, float> measure)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(measure);
        var lines = new List<string>();
        var current = string.Empty;
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = current.Length == 0 ? word : current + " " + word;
            if (measure(candidate) <= maxWidth)
            {
                current = candidate;
                continue;
            }

            if (current.Length > 0)
            {
                lines.Add(current);
                current = string.Empty;
            }

            var rest = word;
            while (measure(rest) > maxWidth && rest.Length > 1)
            {
                var cut = rest.Length - 1;
                while (cut > 1 && measure(rest[..cut]) > maxWidth)
                {
                    cut--;
                }

                lines.Add(rest[..cut]);
                rest = rest[cut..];
            }

            current = rest;
        }

        lines.Add(current);
        return lines;
    }

    /// <summary>
    /// The part of a one-line text that fits <paramref name="maxWidth"/> and keeps the caret in view, with the caret's place in it: the
    /// text is cut at the start so that everything before the caret fits, then at the end so that the rest fits.
    /// </summary>
    public static (string Visible, int CaretOffset) Window(string text, int caret, float maxWidth, Func<string, float> measure)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(measure);
        caret = Math.Clamp(caret, 0, text.Length);
        var start = 0;
        while (start < caret && measure(text[start..caret]) > maxWidth)
        {
            start++;
        }

        var end = text.Length;
        while (end > caret && measure(text[start..end]) > maxWidth)
        {
            end--;
        }

        return (text[start..end], caret - start);
    }
}
