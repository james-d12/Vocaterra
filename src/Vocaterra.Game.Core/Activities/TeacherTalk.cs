using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;
using Vocaterra.Nlp.Core.Audio;

namespace Vocaterra.Game.Core.Activities;

/// <summary>
/// The learner's talk to the teacher from the station (W08, design 10.8 and 10.14): V or Space records a line, W opens a text box, and the line goes to the teacher the session put in
/// its teaching state (<see cref="GuideSession.TeachingNpc"/>), whose prompt is the NPC file's <c>teaching</c> prompt. The answer is a turn like any other (it is applied by the
/// session, its host events reach the runner and the bag, and the front-end plays its lines through <c>NpcVoice</c>); this class only keeps what the station's talk line shows, and
/// takes the turns it submitted so that they do not go to the dialogue panel. The station stays open throughout.
/// </summary>
public sealed class TeacherTalk
{
    private readonly VoxelSession _session;
    private readonly ListenSession? _listen;
    private readonly Func<GuideCell?> _cursor;
    private readonly HashSet<long> _mine = [];
    private string? _problem;

    /// <summary>Creates the talk of a station.</summary>
    /// <param name="session">The session whose teacher is talked to.</param>
    /// <param name="listen">The push-to-talk of the run, or <see langword="null"/> when the front-end has none (the spoken line is then unavailable).</param>
    /// <param name="cursor">The cell under the crosshair, for the turn's scene (a teacher has no use for it, but every turn carries it).</param>
    public TeacherTalk(VoxelSession session, ListenSession? listen, Func<GuideCell?> cursor)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _listen = listen;
        _cursor = cursor ?? throw new ArgumentNullException(nameof(cursor));
    }

    /// <summary>The teacher who listens (the session's <see cref="GuideSession.TeachingNpc"/>), or <see langword="null"/> when nobody does.</summary>
    public string? Teacher => _session.TeachingNpc;

    /// <summary>Whether the learner is typing a line (the keys then belong to the text box).</summary>
    public bool IsTyping { get; private set; }

    /// <summary>The text box.</summary>
    public TextEntry Entry { get; } = new();

    /// <summary>Whether the microphone is recording.</summary>
    public bool Listening => _listen?.Listening ?? false;

    /// <summary>Whether a line of the talk is on the worker.</summary>
    public bool Thinking => _mine.Count > 0;

    /// <summary>The teacher's last answer, in the order she said it (empty before the first answer and after the next act).</summary>
    public IReadOnlyList<string> Answer { get; private set; } = [];

    /// <summary>Swallows the key that opened the text box, which the platform types too.</summary>
    public OpenKeySwallow Swallow { get; } = new();

    /// <summary>What the talk line shows now, or <see langword="null"/> when there is nothing to show.</summary>
    public StationTalkView? View()
    {
        if (Teacher is null)
        {
            return null;
        }

        var name = _session.Data.Npcs.Find(Teacher)?.Name ?? Teacher;
        if (IsTyping)
        {
            return new StationTalkView(name, StationTalkState.Typing, Entry.Text);
        }

        if (Listening)
        {
            return new StationTalkView(name, StationTalkState.Listening, string.Empty);
        }

        if (Thinking)
        {
            return new StationTalkView(name, StationTalkState.Thinking, string.Empty);
        }

        if (_problem is not null)
        {
            return new StationTalkView(name, StationTalkState.Problem, _problem);
        }

        return Answer.Count > 0 ? new StationTalkView(name, StationTalkState.Answer, string.Join(" ", Answer)) : null;
    }

    /// <summary>Forgets the answer shown (the learner acted, so the line makes room for the notice).</summary>
    public void ClearAnswer()
    {
        Answer = [];
        _problem = null;
    }

    /// <summary>The station closed: a recording is dropped and the text box is closed.</summary>
    public void Reset()
    {
        Cancel();
        ClearAnswer();
        Entry.Clear();
        IsTyping = false;
    }

    /// <summary>Opens the text box (W). The key that opened it is swallowed from the platform's text event.</summary>
    /// <returns>Whether a teacher listens.</returns>
    public bool BeginTyping()
    {
        if (Teacher is null || Listening)
        {
            return false;
        }

        ClearAnswer();
        IsTyping = true;
        Swallow.Arm('w');
        return true;
    }

    /// <summary>Closes the text box without sending.</summary>
    public void EndTyping() => IsTyping = false;

    /// <summary>
    /// The text box's keys, while it is open: Enter sends, Esc and Tab close, and the editing keys move and delete. Letters arrive by <see cref="Type"/>.
    /// </summary>
    /// <param name="input">The frame's keys.</param>
    /// <param name="edits">The repeats of the editing keys.</param>
    public void HandleTyping(IInputFrame input, TextEdits edits)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Pressed(GameKey.Escape) || input.Pressed(GameKey.Tab))
        {
            EndTyping();
            return;
        }

        if (input.Pressed(GameKey.Enter))
        {
            _ = Send();
            return;
        }

        if (edits.Backspace)
        {
            _ = Entry.Backspace();
        }

        if (edits.Delete)
        {
            _ = Entry.Delete();
        }

        if (edits.Left)
        {
            Entry.MoveLeft();
        }

        if (edits.Right)
        {
            Entry.MoveRight();
        }
    }

    /// <summary>A letter of the window's text event: the text box takes it, with the accents of Alt.</summary>
    /// <param name="character">The character typed.</param>
    /// <param name="alt">Whether Alt is held (Alt plus a vowel is the accented letter).</param>
    public void Type(char character, bool alt)
    {
        if (!IsTyping || char.IsControl(character) || Swallow.Take(character))
        {
            return;
        }

        _ = Entry.Insert(_session.Data.Words.Typing.ForTyped(character, alt));
    }

    /// <summary>Types a whole line and sends it (a scripted run's <c>--station-say</c>, and the tests).</summary>
    /// <param name="line">The line.</param>
    /// <returns>Whether it was sent.</returns>
    public bool Say(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        Entry.Clear();
        _ = Entry.Insert(line);
        ClearAnswer();
        return Send();
    }

    /// <summary>Sends the typed line to the teacher (Enter). A blank line sends nothing.</summary>
    /// <returns>Whether a line was sent.</returns>
    public bool Send()
    {
        if (Teacher is null || Entry.IsBlank || _session.Worker is null)
        {
            return false;
        }

        var line = Entry.Take();
        IsTyping = false;
        _ = _mine.Add(_session.SubmitTyped(line, _cursor()).Id);
        return true;
    }

    /// <summary>Starts the recording (V or Space, held). The worker needs the recognizer and the machine a microphone; otherwise the line says why.</summary>
    /// <param name="key">The key that is held.</param>
    /// <returns>Whether the recording started.</returns>
    public bool Speak(GameKey key)
    {
        ClearAnswer();
        if (Teacher is null || _listen is null || _session.Worker is not { } worker)
        {
            return false;
        }

        if (!worker.CanHear)
        {
            _problem = worker.Load?.Problem ?? _session.Locale.Ui("speechUnavailable");
            return false;
        }

        if (!_listen.Begin(new ListenTarget(Heard), key))
        {
            _problem = _listen.Problem;
            return false;
        }

        return true;
    }

    /// <summary>The key went up, or Esc: the recording is dropped and nothing is sent.</summary>
    public void Cancel() => _listen?.Cancel();

    /// <summary>Once a frame: the recording ends when its key is up.</summary>
    /// <param name="input">The frame's keys.</param>
    public void Frame(IInputFrame input)
    {
        ArgumentNullException.ThrowIfNull(input);
        _listen?.ReleaseIfUp(input);
        Swallow.Tick();
    }

    /// <summary>Whether the applied turn is one this talk submitted (the dialogue panel must not show it).</summary>
    /// <param name="applied">The applied turn.</param>
    public bool Takes(AppliedTurn applied)
    {
        ArgumentNullException.ThrowIfNull(applied);
        return _mine.Contains(applied.Outcome.Request.Id);
    }

    /// <summary>Takes the teacher's answer: the line the station shows.</summary>
    /// <param name="applied">The applied turn, for which <see cref="Takes"/> is true.</param>
    public void Complete(AppliedTurn applied)
    {
        ArgumentNullException.ThrowIfNull(applied);
        _ = _mine.Remove(applied.Outcome.Request.Id);
        _problem = applied.Outcome.Problem;
        Answer = applied.Turn?.GuideLines.ToList() ?? [];
    }

    // The clip of a recording: it goes to the worker as a spoken turn, or the line says why there is none.
    private void Heard(PcmClip? clip)
    {
        if (clip is null)
        {
            _problem = _listen?.Problem;
            return;
        }

        _ = _mine.Add(_session.SubmitClip(clip, _cursor()).Id);
    }
}
