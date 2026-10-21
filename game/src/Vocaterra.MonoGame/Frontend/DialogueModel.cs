using System.Globalization;
using Vocaterra.Game;

namespace Vocaterra.MonoGame.Frontend;

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

    /// <summary>The guide's own reply.</summary>
    Guide,

    /// <summary>The game's line with the world facts.</summary>
    World,

    /// <summary>A hint from the game about the player's progress (for example why a word did not gain colour).</summary>
    Hint,
}

/// <summary>A scripted spoken line waiting to be sent (<c>--wav</c>, <c>--speak</c>): the game submits it to the worker between ticks.</summary>
/// <param name="Source">A WAV file or a line to synthesize.</param>
/// <param name="Value">The file path, or the text to speak.</param>
public sealed record SpeechRequest(TurnSource Source, string Value);

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
    private bool _opened;

    /// <summary>Creates a closed panel.</summary>
    /// <param name="focus">Who has the keyboard; the model sets <see cref="InputFocus.TextEntryActive"/> while it is open.</param>
    /// <param name="openingLine">What the guide says the first time the panel opens (the prompt's text).</param>
    public DialogueModel(InputFocus focus, string openingLine)
    {
        ArgumentNullException.ThrowIfNull(focus);
        ArgumentNullException.ThrowIfNull(openingLine);
        _focus = focus;
        _openingLine = openingLine;
    }

    /// <summary>Whether the panel is open (and so has the keyboard).</summary>
    public bool IsOpen { get; private set; }

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

    /// <summary>Turns sent to the worker whose answer has not come back: the panel shows "Pensando…" while it is above zero.</summary>
    public int Pending { get; private set; }

    /// <summary>Whether the push-to-talk key is held and the microphone is recording: the panel shows "Escuchando…".</summary>
    public bool Listening { get; set; }

    /// <summary>The Spanish message about the microphone (none present, silence heard), shown under the lines until the next recording, or <see langword="null"/>.</summary>
    public string? SpeechNotice { get; set; }

    /// <summary>A scripted spoken line waiting to be submitted, or <see langword="null"/>.</summary>
    public SpeechRequest? PendingSpeech { get; private set; }

    /// <summary>The seconds of a scripted microphone recording (<c>--listen</c>) waiting to start, or <see langword="null"/>.</summary>
    public int? PendingListenSeconds { get; private set; }

    /// <summary>Whether Enter was pressed on a line: the game then evaluates it, between ticks, and calls <see cref="Submit"/>.</summary>
    public bool SubmitRequested { get; private set; }

    /// <summary>
    /// Opens the panel and gives it the keyboard. The first time, the guide's opening line is added; when the guide opens with a review
    /// question, that is added as her line (every time the panel opens with one).
    /// </summary>
    /// <param name="reviewQuestion">The review question ("¿Qué es esto?"), or <see langword="null"/> when no word is due.</param>
    public void Open(string? reviewQuestion = null)
    {
        if (IsOpen)
        {
            return;
        }

        IsOpen = true;
        _focus.TextEntryActive = true;
        if (!_opened)
        {
            _opened = true;
            Add(DialogueSpeaker.Guide, _openingLine);
        }

        if (reviewQuestion is not null)
        {
            Add(DialogueSpeaker.Guide, reviewQuestion);
        }
    }

    /// <summary>Adds a hint line from the game to the panel (for example <c>Bien. Repasa más tarde.</c>).</summary>
    public void AddHint(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Add(DialogueSpeaker.Hint, text);
    }

    /// <summary>Closes the panel, gives the keyboard back to the player and drops a pending send. The lines and the half-typed text stay.</summary>
    public void Close()
    {
        IsOpen = false;
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
        Pending++;
        return line;
    }

    /// <summary>Counts a spoken turn sent to the worker as pending (its line is shown when the answer comes back, as the recognizer heard it).</summary>
    public void BeginSpoken()
    {
        SpeechNotice = null;
        Pending++;
    }

    /// <summary>
    /// Shows a finished turn: for a spoken one the transcript, then the verdict note and the guide's lines. A request that failed shows its
    /// problem as a hint. Returns the turn, or <see langword="null"/> when there was none.
    /// </summary>
    /// <param name="applied">What <c>GameSession.Collect</c> returned for the request.</param>
    public GuideTurn? Complete(AppliedTurn applied)
    {
        ArgumentNullException.ThrowIfNull(applied);
        Pending = Math.Max(0, Pending - 1);
        if (applied.Turn is not { } turn)
        {
            Add(DialogueSpeaker.Hint, applied.Outcome.Problem ?? "No se pudo evaluar la frase.");
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
        var heard = turn.Confidence is { } confidence
            ? string.Create(CultureInfo.InvariantCulture, $"oído con {(int)Math.Round(confidence * 100, MidpointRounding.AwayFromZero)} %, ")
            : string.Empty;
        Add(DialogueSpeaker.Note, string.Create(CultureInfo.InvariantCulture, $"{heard}{turn.Summary}, {LastTurnMilliseconds:0.0} ms"));
        if (turn.Feedback is not null)
        {
            Add(DialogueSpeaker.Correction, turn.Feedback);
        }

        foreach (var reply in turn.Replies)
        {
            Add(DialogueSpeaker.Guide, reply);
        }

        if (turn.Repair is not null)
        {
            Add(DialogueSpeaker.Guide, turn.Repair);
        }

        if (turn.WorldLine is not null)
        {
            Add(DialogueSpeaker.World, turn.WorldLine);
        }
    }

    private void Add(DialogueSpeaker speaker, string text)
    {
        _lines.Add(new DialogueLine(speaker, text));
        if (_lines.Count > MaxLines)
        {
            _lines.RemoveRange(0, _lines.Count - MaxLines);
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
