using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Screens;
using Vocaterra.Game.Core.Scripted;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Sound;
using Vocaterra.Nlp.Core.Audio;

namespace Vocaterra.Game.Core.Language.Checks;

/// <summary>
/// The check battery (S11): outside the world, its own screen, its own grades; nothing it does reaches the progress or the world. The items come
/// in the seeded order of the kind; a comprehension item is answered with 1 to 3 (R hears the line again), a production item by voice (V held),
/// typed (W, then Enter) or with "No sé" (N), and Esc ends the battery (the rest is skipped). A scripted run answers from the script's steps.
/// </summary>
public sealed class CheckBattery : IScreen
{
    /// <summary>The frames a post- or recall-check's thank-you screen stays up before the run ends (outside a playtest).</summary>
    public const int EndFrames = 90;

    private static readonly (GameKey Key, GameKey Pad)[] OptionKeys = [(GameKey.D1, GameKey.NumPad1), (GameKey.D2, GameKey.NumPad2), (GameKey.D3, GameKey.NumPad3)];

    private readonly VoxelSession _session;
    private readonly Func<ICheckLog?> _log;
    private readonly Func<double> _milliseconds;
    private readonly SoundFrontend _sound;
    private readonly NpcVoice _voice;
    private readonly ListenSession _listen;
    private readonly OpenKeySwallow _swallow;
    private readonly IReadOnlyList<ScriptStep> _steps;
    private readonly bool _scriptedRun;
    private readonly bool _playtest;
    private readonly CheckBatteryHooks _hooks;
    private readonly Action<string> _console;
    private CheckScript? _script;
    private int _framesLeft;
    private int _playedAt = -1;

    /// <summary>Creates the battery of a run (none is running until <see cref="Begin"/>).</summary>
    /// <param name="session">The session the answers are graded by.</param>
    /// <param name="log">The session log (S11), once it is open.</param>
    /// <param name="milliseconds">The host's monotonic clock in milliseconds (only differences mean anything): how long an answer took.</param>
    /// <param name="sound">Plays an answer's cue.</param>
    /// <param name="voice">Says a comprehension item's line.</param>
    /// <param name="listen">The push-to-talk of the run.</param>
    /// <param name="swallow">The key that opened the text box (W) is not typed.</param>
    /// <param name="steps">The run's script, which a scripted battery answers from.</param>
    /// <param name="scriptedRun">Whether the run is scripted (no thank-you timer ends it).</param>
    /// <param name="playtest">Whether the run is a playtest (its thank-you screen waits for Esc).</param>
    /// <param name="hooks">What the battery needs from the game around it.</param>
    /// <param name="console">Where the battery's lines go.</param>
    public CheckBattery(
        VoxelSession session,
        Func<ICheckLog?> log,
        Func<double> milliseconds,
        SoundFrontend sound,
        NpcVoice voice,
        ListenSession listen,
        OpenKeySwallow swallow,
        IReadOnlyList<ScriptStep> steps,
        bool scriptedRun,
        bool playtest,
        CheckBatteryHooks hooks,
        Action<string> console)
    {
        _session = session;
        _log = log;
        _milliseconds = milliseconds;
        _sound = sound;
        _voice = voice;
        _listen = listen;
        _swallow = swallow;
        _steps = steps;
        _scriptedRun = scriptedRun;
        _playtest = playtest;
        _hooks = hooks;
        _console = console;
    }

    /// <summary>The battery on screen, or <see langword="null"/> when none is running (a pre-check that has handed over to the world is gone).</summary>
    public CheckScreen? Screen { get; private set; }

    /// <summary>Whether the battery has finished (its end is in the log).</summary>
    public bool Done { get; private set; }

    /// <inheritdoc />
    public string Name => $"check ({CheckKinds.Of(Screen!.Kind)})";

    /// <inheritdoc />
    public bool IsOpen => Screen is { IsDone: false };

    /// <summary>The battery holds the keyboard but not the world's mouse look (its own screen covers the world, which does not run).</summary>
    public bool FreesMouse => false;

    /// <summary>Whether the finished battery ends the run (a post- or recall-check).</summary>
    public bool EndsRun { get; private set; }

    /// <summary>Whether the scripted battery waits for an answer its script does not give.</summary>
    public bool ScriptRests => _script is { Rests: true };

    /// <summary>The battery is waiting for a learner's answer that the scripted run has not given, or it has ended and shows its thank-you screen.</summary>
    public bool RestsForShot => Screen is { IsDone: false, Phase: CheckPhase.Showing } && ScriptRests || (Done && EndsRun);

    /// <summary>Whether the battery's text box takes the letters typed.</summary>
    public bool TakesText => Screen is { IsDone: false, IsTyping: true };

    /// <summary>Starts a battery: its plan from the bundle and the seed, its log line, and for a scripted run the script's answers from a step.</summary>
    /// <param name="kind">The battery: pre, post or recall.</param>
    /// <param name="scripted">Whether the script answers it.</param>
    /// <param name="scriptIndex">The script's step the answers start at.</param>
    public void Begin(CheckKind kind, bool scripted, int scriptIndex)
    {
        // A battery starts fresh: the pre-check's end must not stand for the post-check (S12).
        Done = false;
        EndsRun = false;
        _framesLeft = 0;
        _playedAt = -1;
        var plan = CheckPlan.For(_session.Data.Checks, kind, _session.WorldSeed);
        Screen = new CheckScreen(kind, plan, _milliseconds, result => _log()!.CheckResult(result, CheckKinds.Of(kind), _session.PlaySession, _session.Simulation.Tick));
        _log()!.CheckStart(CheckKinds.Of(kind), _session.PlaySession, plan.Count);
        _console($"check: {CheckKinds.Of(kind)}, {plan.Count} items");
        if (!scripted)
        {
            return;
        }

        // A scripted run prints the order and the right option of each item, so that its --pick steps can be written (a console line only).
        var order = plan.Select(static e => e.Item.Kind == CheckItemKind.Comprehension ? $"{e.Item.Id} (pick {e.ShownCorrect + 1})" : e.Item.Id);
        _console($"check order: {string.Join(", ", order)}");
        var hooks = new CheckScriptHooks(picked => Pick(picked - 1), SubmitTyped, SubmitSpoken, _hooks.TakeShot, _hooks.ShotPending);
        _script = new CheckScript(_steps, Screen, hooks, _console, scriptIndex);
    }

    /// <summary>A finished turn of the session: a check answer grades the battery's item. Returns whether the turn was a check answer.</summary>
    public bool Graded(AppliedTurn applied)
    {
        if (applied.Outcome.Request.Check is null)
        {
            return false;
        }

        if (Screen is null)
        {
            return true;
        }

        if (applied.Outcome.Grade is { } grade)
        {
            Screen.Graded(grade);
            _sound.ObserveTurn(null, null, isCheck: true);
            _console($"check: {applied.Outcome.Request.Check.Id} heard \"{grade.Transcript}\" -> {grade.Intent}, {(grade.Correct ? "correct" : grade.DontKnow ? "no sé" : "not correct")}{(grade.ErrorTags.Count > 0 ? $" ({string.Join(", ", grade.ErrorTags)})" : string.Empty)}");
        }
        else
        {
            Screen.Failed(applied.Outcome.Problem ?? _session.Locale.Ui("checkNotGraded"));
        }

        return true;
    }

    /// <summary>
    /// The keys of the battery: the comprehension items (1 to 3, R to hear the line again), the production items (V held to speak, W to type,
    /// Enter to send, N for no sé) and Esc (end the battery; the rest is skipped, and an answer in the air is waited for).
    /// </summary>
    public void HandleInput(IInputFrame input)
    {
        var check = Screen!;
        if (input.Pressed(GameKey.Escape))
        {
            if (check.IsTyping)
            {
                check.IsTyping = false;
            }
            else if (check.Listening)
            {
                CancelListening();
            }
            else if (check.Phase != CheckPhase.Thinking)
            {
                Skip();
            }

            return;
        }

        if (check.Phase != CheckPhase.Showing || check.Current is not { } entry)
        {
            return;
        }

        if (entry.Item.Kind == CheckItemKind.Comprehension)
        {
            for (var i = 0; i < OptionKeys.Length; i++)
            {
                if (input.Pressed(OptionKeys[i].Key) || input.Pressed(OptionKeys[i].Pad))
                {
                    Pick(i);
                    return;
                }
            }

            if (input.Pressed(GameKey.R))
            {
                PlayLine(entry);
            }

            return;
        }

        if (check.IsTyping)
        {
            if (input.Pressed(GameKey.Enter))
            {
                if (!check.Entry.IsBlank)
                {
                    SubmitTyped(check.Entry.Text);
                }
            }
            else if (input.Pressed(GameKey.Back))
            {
                _ = check.Entry.Backspace();
            }

            return;
        }

        if (check.Listening)
        {
            _listen.ReleaseIfUp(input); // V held to speak
            return;
        }

        if (input.Pressed(GameKey.V))
        {
            BeginListening();
        }
        else if (input.Pressed(GameKey.W) || input.Pressed(GameKey.Tab))
        {
            check.IsTyping = true;
            if (input.Pressed(GameKey.W))
            {
                _swallow.Arm('w'); // the platform types the key once more: the text event must not take it
            }
        }
        else if (input.Pressed(GameKey.N))
        {
            SubmitDontKnow();
        }
    }

    /// <summary>A letter of the text box (S11): the key that opened it (W) is swallowed, the rest is typed, with the accents of Alt.</summary>
    public void Type(char character, bool alt)
    {
        if (_swallow.Take(character))
        {
            return;
        }

        if (!char.IsControl(character))
        {
            _ = Screen!.Entry.Insert(AccentShortcuts.ForTyped(character, alt));
        }
    }

    /// <summary>Esc while the microphone records (or F8): the recording is dropped and nothing is sent.</summary>
    public void CancelListening()
    {
        _listen.Cancel();
        if (Screen is { } check)
        {
            check.Listening = false;
        }
    }

    /// <summary>Once a frame while the battery runs: the scripted answers (screenshot runs), the first sound of an item, and the end of the battery.</summary>
    public void Drive()
    {
        if (Screen is null)
        {
            return;
        }

        if (Done)
        {
            if (EndsRun && _framesLeft > 0 && --_framesLeft == 0 && !_scriptedRun)
            {
                _hooks.Exit(); // a post- or recall-check ends the run after its thank-you screen
            }

            return;
        }

        if (Screen.IsDone)
        {
            Finish();
            return;
        }

        if (_hooks.MicCheckActive())
        {
            return; // the mic check comes first
        }

        _script?.Fill();
        if (Screen.Current is { Item.Kind: CheckItemKind.Comprehension } entry && _playedAt != Screen.Index)
        {
            _playedAt = Screen.Index;
            PlayLine(entry);
        }
    }

    // The learner picked an option of the comprehension item on the screen (1 to 3, as shown).
    private void Pick(int shown)
    {
        if (Screen is not { } check)
        {
            _console("check: no battery is running");
            return;
        }

        if (check.Pick(shown))
        {
            _sound.ObserveTurn(null, null, isCheck: true);
            _console($"check: picked {shown + 1} ({(check.Results[^1].Correct ? "the right one" : "not the right one")}, {check.Results[^1].LatencyMs:0} ms)");
        }
    }

    // A typed answer of the production item (W, then Enter; or the scripted --say).
    private void SubmitTyped(string text) => Submit(TurnSource.Typed, text, null, item => $"check: typed \"{text}\" for {item}");

    // A line to synthesize and answer as spoken (the scripted --speak).
    private void SubmitSpoken(string text) => Submit(TurnSource.Speak, text, null, item => $"check: spoken \"{text}\" for {item}");

    // The "No sé" button of a production item: graded as dont_know, never a failure.
    private void SubmitDontKnow() => Submit(TurnSource.Button, _session.DontKnowLine, null, item => $"check: no sé for {item}");

    private void Submit(TurnSource source, string? text, PcmClip? clip, Func<string, string> line)
    {
        if (Screen is not { } check || !check.Sent())
        {
            return;
        }

        var item = check.Plan[check.Index].Item;
        _ = _session.SubmitCheck(item, source, text, clip, null);
        _console(line(item.Id));
    }

    private void BeginListening()
    {
        var check = Screen!;
        if (check.Phase != CheckPhase.Showing || check.Current is not { Item.Kind: CheckItemKind.Production })
        {
            return;
        }

        if (!_session.Worker!.CanHear)
        {
            check.Notice = _session.Worker.Load?.Problem ?? _session.Locale.Ui("checkVoiceOff");
            return;
        }

        if (_listen.Begin(new ListenTarget(Heard), GameKey.V))
        {
            check.Listening = true;
            check.Notice = null;
        }
        else
        {
            check.Notice = _listen.Problem;
        }
    }

    // The key went up (or the recording reached its cap): the clip is the answer, sent to the worker as a spoken check answer.
    private void Heard(PcmClip? clip)
    {
        var check = Screen!;
        check.Listening = false;
        if (clip is null)
        {
            check.Notice = _listen.Problem;
            return;
        }

        Submit(TurnSource.Clip, null, clip, item => $"check: listened {clip.DurationSeconds:0.00} s for {item}");
    }

    private void Skip()
    {
        CancelListening();
        Screen?.SkipRest();
        _console("check: ended early (Esc); the rest is skipped");
    }

    // The comprehension item's line plays once when the item comes up, and again on R.
    private void PlayLine(CheckPlanEntry entry) => _voice.Play(Speaker(entry.Item.Line), entry.Item.Spanish, slow: false);

    // The speaker of a check line: a prompt's line is said by the prompt's agent; a phrase's model line by the guide.
    private string Speaker(string line)
    {
        const string PromptPrefix = "prompt.";
        if (line.StartsWith(PromptPrefix, StringComparison.Ordinal) && _session.Bundle.Prompts.TryGetValue(line[PromptPrefix.Length..], out var prompt))
        {
            return prompt.AgentId ?? SpeechSpeakers.Guide;
        }

        return SpeechSpeakers.Guide;
    }

    // The battery is done: the end is in the log, a pre-check hands over to the world (its script goes on from where the battery stopped), and a
    // post- or recall-check shows its thank-you screen and ends the run.
    private void Finish()
    {
        var check = Screen!;
        Done = true;
        var answered = check.Results.Count(static r => r.Input != CheckInput.Skipped);
        var skipped = check.Results.Count - answered;
        var kind = CheckKinds.Of(check.Kind);
        _log()?.CheckEnd(kind, answered, skipped);
        _console($"check {kind}: {answered} answered, {skipped} skipped");
        if (check.Kind == CheckKind.Pre)
        {
            if (_script is { } script)
            {
                _hooks.SkipScenarioTo(script.Index);
            }

            _script = null;
            Screen = null;
            return;
        }

        _script = null;
        EndsRun = true;
        if (!_playtest)
        {
            _framesLeft = EndFrames;
            return;
        }

        // The playtest's end: the thank-you holds until Esc (no timer).
        _framesLeft = -1;
        _hooks.PlaytestEnded(check);
    }
}
