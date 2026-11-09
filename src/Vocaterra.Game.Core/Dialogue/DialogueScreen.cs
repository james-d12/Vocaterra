using System.Globalization;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Screens;
using Vocaterra.Game.Core.Scripted;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Sound;
using Vocaterra.Nlp.Core.Audio;

namespace Vocaterra.Game.Core.Dialogue;

/// <summary>
/// The dialogue panel's behaviour (S3, S7, S16): a talk opened on an NPC and closed again, the speech buttons and the choices (keys or the
/// mouse), the text box, push-to-talk for a spoken turn, the lesson strip's keys and buttons, and the watchdog that offers typing when a turn
/// thinks too long. The panel's state is <see cref="DialogueModel"/>; the panel takes the keyboard while it is open.
/// </summary>
public sealed class DialogueScreen : IScreen
{
    // The panel's and the lesson strip's keys come from the one control table (ControlTable); the keypad's digits answer as the top row's do.
    private static readonly (GameKey Key, PanelKey Panel)[] PanelKeys =
    [
        (ControlTable.Key("repeat"), PanelKey.R), (ControlTable.Key("slower"), PanelKey.S), (ControlTable.Key("help"), PanelKey.H), (ControlTable.Key("dontKnow"), PanelKey.N),
        (ControlTable.Key("typeLine", 0), PanelKey.W), (ControlTable.Key("typeLine", 1), PanelKey.Tab),
        .. WithPad([(ControlTable.Key("choose", 0), PanelKey.D1), (ControlTable.Key("choose", 1), PanelKey.D2), (ControlTable.Key("choose", 2), PanelKey.D3)]),
    ];

    private static readonly (GameKey Key, LessonKey Lesson)[] LessonKeys =
    [
        (ControlTable.Key("lessonContinue"), LessonKey.Enter), (ControlTable.Key("lessonSkip"), LessonKey.K), (ControlTable.Key("lessonStart"), LessonKey.L), (ControlTable.Key("lessonMore"), LessonKey.M),
        .. WithPad([(ControlTable.Key("lessonPick", 0), LessonKey.D1), (ControlTable.Key("lessonPick", 1), LessonKey.D2), (ControlTable.Key("lessonPick", 2), LessonKey.D3), (ControlTable.Key("lessonPick", 3), LessonKey.D4)]),
    ];

    private static (GameKey Key, T Action)[] WithPad<T>((GameKey Key, T Action)[] digits) =>
        [.. digits, .. digits.Select(static d => (ControlTable.PadOf(d.Key), d.Action))];

    private readonly DialogueModel _dialogue;
    private readonly VoxelSession _session;
    private readonly NpcVoice _voice;
    private readonly SpeechActionRunner _actions;
    private readonly LessonFrontend _lesson;
    private readonly ListenSession _listen;
    private readonly OpenKeySwallow _swallow;
    private readonly TextEditKeys _textEdits;
    private readonly SoundFrontend _sound;
    private readonly Func<int, int, LessonAction?> _lessonActionAt;
    private readonly Func<int, int, SpeechAction?> _speechActionAt;
    private readonly Func<string?> _aimedNpc;
    private readonly bool _scriptedRun;
    private readonly Action<string> _console;
    private readonly TurnWatchdog _watchdog = new();

    /// <summary>Creates the panel's behaviour.</summary>
    /// <param name="dialogue">The panel's state.</param>
    /// <param name="session">The session the talks are with.</param>
    /// <param name="voice">Says the NPCs' lines.</param>
    /// <param name="actions">Runs the speech buttons and the choices.</param>
    /// <param name="lesson">The lesson strip.</param>
    /// <param name="listen">The push-to-talk of the run.</param>
    /// <param name="swallow">The key that opened the text box is not typed.</param>
    /// <param name="textEdits">The repeats of the text box's editing keys.</param>
    /// <param name="sound">Plays the buttons' clicks.</param>
    /// <param name="lessonActionAt">The lesson strip's button at a point of the canvas, or <see langword="null"/> (the strip's layout, from the game).</param>
    /// <param name="speechActionAt">The panel's speech button at a point of the canvas, or <see langword="null"/> (the panel's layout, from the game).</param>
    /// <param name="aimedNpc">The NPC under the crosshair, or <see langword="null"/>.</param>
    /// <param name="scriptedRun">Whether the run is scripted (it has no real time, so no watchdog).</param>
    /// <param name="console">Where the panel's lines go.</param>
    public DialogueScreen(
        DialogueModel dialogue,
        VoxelSession session,
        NpcVoice voice,
        SpeechActionRunner actions,
        LessonFrontend lesson,
        ListenSession listen,
        OpenKeySwallow swallow,
        TextEditKeys textEdits,
        SoundFrontend sound,
        Func<int, int, LessonAction?> lessonActionAt,
        Func<int, int, SpeechAction?> speechActionAt,
        Func<string?> aimedNpc,
        bool scriptedRun,
        Action<string> console)
    {
        _dialogue = dialogue;
        _session = session;
        _voice = voice;
        _actions = actions;
        _lesson = lesson;
        _listen = listen;
        _swallow = swallow;
        _textEdits = textEdits;
        _sound = sound;
        _lessonActionAt = lessonActionAt;
        _speechActionAt = speechActionAt;
        _aimedNpc = aimedNpc;
        _scriptedRun = scriptedRun;
        _console = console;
    }

    /// <inheritdoc />
    public string Name => $"dialogue with {_dialogue.NpcName}";

    /// <inheritdoc />
    public bool IsOpen => _dialogue.IsOpen;

    /// <inheritdoc />
    public bool FreesMouse => true;

    /// <summary>
    /// Opens the panel on an NPC: its opening is a core host turn when its state has one (the shopkeeper's question, the villager's hello, the
    /// guide's transfer offer), otherwise its prompt's text, and the guide's review question when one is due. The key that opened the panel
    /// (<paramref name="openedBy"/>: T or E) is swallowed by the text event; T opens the text box at once.
    /// </summary>
    public void Open(string npc, char? openedBy)
    {
        var opening = _session.OpenTalk(npc, teachKeyWords: true);
        _dialogue.Open(opening, _session.Data.Npcs.CalledOf(npc), _session.ReviewLine, _session.RetryNote);
        _voice.Say(npc, opening.Lines);
        if (openedBy == 't')
        {
            _dialogue.BeginTyping(); // T is "type": the text box has the keyboard from the start
        }

        _swallow.Arm(openedBy); // the platform types the key once, in the next frame at the latest
        var how = opening.HostTurn ? "host turn" : "prompt";
        _console($"talk: {npc} ({how}, {opening.Prompt}): {(_session.KeyWordsPending ? $"[key words: {string.Join(", ", _session.KeyWordCards.Select(static c => c.Phrase))}]" : string.Join(" / ", opening.Lines))}{(_session.ReviewLine is { } review ? $" / {review}" : string.Empty)}");
    }

    /// <summary>The panel closes (Esc, or the script walks on): a recording in progress is dropped and the talk ends.</summary>
    public void Close()
    {
        CancelListening();
        _watchdog.Reset();
        _dialogue.Close();
        _voice.Stop();
        _session.EndTalk();
    }

    /// <summary>
    /// The panel's keys while it is open. In the speech-button mode R, S, H, N, W (or Tab), 1 to 3 press the buttons, V or Space held talks and
    /// Enter presses the Escribir button when typing is on offer; in the typing mode Enter sends, Tab or Esc go back to the buttons and the editing
    /// keys repeat. Esc undoes the newest thing first: a recording, a turn that is thinking, the typing mode, and last the panel itself.
    /// </summary>
    public void HandleInput(IInputFrame input)
    {
        _listen.ReleaseIfUp(input);
        if (input.Pressed(GameKey.Escape))
        {
            if (_listen.Listening)
            {
                CancelListening();
                _session.LogVoice(VoiceEventKind.Cancel, "recording"); // the recording is dropped and nothing is sent
            }
            else if (_actions.CancelThinking())
            {
                _watchdog.Reset();
            }
            else if (_dialogue.IsTyping)
            {
                _dialogue.EndTyping();
            }
            else
            {
                Close();
            }

            return;
        }

        // U06: the wheel scrolls the panel in every state, typing included (it types nothing): a notch up shows the older lines and goes back through them, a notch down
        // comes forward, and one more at the newest line folds the page back to the current exchange. The world's hotbar does not see it while a screen is open.
        if (input.Wheel != 0)
        {
            var notches = Math.Max(1, (int)Math.Round(Math.Abs(input.Wheel) / 120.0));
            for (var i = 0; i < notches; i++)
            {
                if (input.Wheel > 0)
                {
                    if (_dialogue.Expanded)
                    {
                        _dialogue.ScrollOlder();
                    }
                    else
                    {
                        _dialogue.ToggleExpanded();
                    }
                }
                else if (_dialogue.Expanded)
                {
                    if (_dialogue.ScrollBack == 0)
                    {
                        _dialogue.ToggleExpanded();
                    }
                    else
                    {
                        _dialogue.ScrollNewer();
                    }
                }
            }
        }

        // U02: PageUp shows or hides the older lines in every state (Tab is the Escribir key, D-game-9, and the letters are typing keys); O does the same off the text box.
        if (input.Pressed(ControlTable.Key("history", 0)) || (!_dialogue.IsTyping && input.Pressed(ControlTable.Key("history", 1))))
        {
            _dialogue.ToggleExpanded();
            return;
        }

        // U02b: while the page shows, Up and Down scroll it back to the older lines and forward to the newest (the arrow keys are not used here otherwise).
        if (_dialogue.Expanded && !_dialogue.IsTyping && (input.Pressed(GameKey.Up) || input.Pressed(GameKey.Down)))
        {
            if (input.Pressed(GameKey.Up))
            {
                _dialogue.ScrollOlder();
            }
            else
            {
                _dialogue.ScrollNewer();
            }

            return;
        }

        if (_dialogue.IsTyping)
        {
            if (input.Pressed(GameKey.Tab))
            {
                _dialogue.EndTyping();
                return;
            }

            if (input.Pressed(GameKey.Enter))
            {
                _dialogue.RequestSubmit();
            }

            var entry = _dialogue.Entry;
            var edits = _textEdits.Step(input);
            if (edits.Backspace)
            {
                _ = entry.Backspace();
            }

            if (edits.Delete)
            {
                _ = entry.Delete();
            }

            if (edits.Left)
            {
                entry.MoveLeft();
            }

            if (edits.Right)
            {
                entry.MoveRight();
            }

            return;
        }

        // The lesson strip's keys (S16): L starts the lesson on offer, Enter continues, K skips the step, 1 to 4 pick an answer of a listen step.
        foreach (var (key, lessonKey) in LessonKeys)
        {
            if (input.Pressed(key) && _lesson.PressKey(lessonKey))
            {
                return;
            }
        }

        if (input.Pressed(InputKeys.Of("listen")) || input.Pressed(InputKeys.Of("listenAlt")))
        {
            BeginListening(input.Pressed(InputKeys.Of("listen")) ? InputKeys.Of("listen") : InputKeys.Of("listenAlt"));
            return;
        }

        if (input.Pressed(GameKey.Enter) && _dialogue.TypingOffered)
        {
            Perform(SpeechAction.Type, null);
            return;
        }

        foreach (var (key, panelKey) in PanelKeys)
        {
            if (input.Pressed(key) && SpeechButtons.ForKey(panelKey) is { } action)
            {
                Perform(action, panelKey == PanelKey.W ? 'w' : null);
                return;
            }
        }
    }

    /// <summary>The mouse went down at a point of the canvas: a button of the lesson strip, or a button or choice of the panel.</summary>
    public void Click(int x, int y)
    {
        if (_lessonActionAt(x, y) is { } lessonAction)
        {
            if (_lesson.Perform(lessonAction)) // a button of the lesson strip (S16)
            {
                _sound.QueueClick(SoundCue.UiClick);
            }

            return;
        }

        if (_speechActionAt(x, y) is { } action)
        {
            Perform(action, null);
        }
    }

    /// <summary>A letter of the window's text event: the panel's text box takes it, with the accents of Alt. The key that opened the panel (T or E) is typed by the platform too, once, and swallowed.</summary>
    public void Type(char character, bool alt)
    {
        if (!_dialogue.IsTyping || char.IsControl(character) || _listen.Listening)
        {
            if (_dialogue.IsOpen)
            {
                _ = _swallow.Take(character);
            }

            return;
        }

        if (_swallow.Take(character))
        {
            return;
        }

        _ = _dialogue.Entry.Insert(AccentShortcuts.ForTyped(character, alt)); // Alt plus a vowel is the accented letter (S21 wired the hints' keys)
    }

    /// <summary>The microphone: the key went down. The worker must have the recognizer, the machine a microphone; otherwise the panel says why.</summary>
    public void BeginListening(GameKey key)
    {
        var worker = _session.Worker!;
        if (!_dialogue.IsOpen)
        {
            Open(_aimedNpc() ?? NpcService.GuideAgent, null);
        }

        if (!worker.CanHear)
        {
            _dialogue.SpeechNotice = worker.Load?.Problem ?? _session.Locale.Ui("speechUnavailable");
            return;
        }

        if (_listen.Begin(new ListenTarget(Heard), key))
        {
            _dialogue.Listening = true;
            _dialogue.SpeechNotice = null;
        }
        else
        {
            _dialogue.SpeechNotice = _listen.Problem;
        }
    }

    /// <summary>The panel closed, or F8, while recording: the clip is dropped.</summary>
    public void CancelListening()
    {
        _listen.Cancel();
        _dialogue.Listening = false;
    }

    /// <summary>
    /// Once a frame (S7): a turn that has been thinking for five seconds of the frame clock logs a timeout and offers typing. A scripted run has no
    /// real time, so it has no watchdog.
    /// </summary>
    public void RunWatchdog(double seconds)
    {
        if (_scriptedRun)
        {
            return;
        }

        if (_watchdog.Update(seconds, _dialogue.IsOpen && _dialogue.IsThinking))
        {
            _session.LogVoice(VoiceEventKind.Timeout, $"{TurnWatchdog.LimitSeconds:0} s");
            _dialogue.OfferTyping();
        }
    }

    /// <summary>A lesson step of the scripted run (S16): the same presses the strip's buttons make.</summary>
    public void RunLessonStep(ScriptStepKind kind, string value)
    {
        switch (kind)
        {
            case ScriptStepKind.LessonStart:
                if (!_session.Lessons.Start(value))
                {
                    _console($"lesson: cannot start '{value}' (unknown, or another lesson is active)");
                    return;
                }

                _lesson.Pump();
                break;
            case ScriptStepKind.LessonContinue:
                _ = _lesson.Perform(LessonAction.Continue);
                break;
            case ScriptStepKind.LessonSkip:
                _ = _lesson.Perform(LessonAction.Skip);
                break;
            case ScriptStepKind.LessonHelp:
                _ = _lesson.Perform(LessonAction.Help);
                break;
            default: // LessonPick
                _ = _lesson.Perform(LessonAction.Option1 + (int.Parse(value, CultureInfo.InvariantCulture) - 1));
                break;
        }
    }

    // A button or a choice of the panel was pressed (key or mouse). Opening the text box with W would also type the W the platform delivers: swallow it.
    private void Perform(SpeechAction action, char? typedByKey)
    {
        if (_actions.Perform(action))
        {
            _sound.QueueClick(SoundCue.UiClick);
            if (action == SpeechAction.Type && typedByKey is { } typed)
            {
                _swallow.Arm(typed);
            }

            _watchdog.Reset();
        }
    }

    // The key went up (or the recording reached its cap): the clip goes to the worker as a spoken turn.
    private void Heard(PcmClip? clip)
    {
        _dialogue.Listening = false;
        if (clip is null)
        {
            _dialogue.SpeechNotice = _listen.Problem;
            return;
        }

        _ = _session.SubmitClip(clip, _session.AimedCell);
        _dialogue.BeginSpoken();
        _console($"listened: {clip.DurationSeconds:0.00} s of speech sent to {_session.CurrentNpc}");
    }
}
