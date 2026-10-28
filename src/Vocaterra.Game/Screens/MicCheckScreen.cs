using FontStashSharp;
using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Sound;
using Vocaterra.Game.Input;
using Vocaterra.Game.UI;
using Vocaterra.Nlp.Core.Audio;

namespace Vocaterra.Game.Screens;

/// <summary>
/// The mic check (S7) before the world: hold V or Space and say "hola", the meter moves with the level, the recognizer says what it heard; Enter
/// or Space goes on after a pass, Enter retries after a fail, Esc skips. The recognizer must be loaded and the machine must have a microphone,
/// otherwise the screen says why: nothing here ever stops the game. A scripted run has the worker synthesize "hola" and hear it.
/// </summary>
public sealed class MicCheckScreen : IScreen
{
    private readonly VoxelSession _session;
    private readonly ListenSession _listen;
    private readonly SoundFrontend _sound;
    private readonly UiCanvas _canvas;
    private readonly Func<SpriteFontBase> _font;
    private readonly bool _scriptedRun;
    private readonly Func<bool> _moreToCome;
    private readonly Action<string> _console;
    private bool _scriptedSent;
    private int _shownTicks;

    /// <summary>Creates the mic check of a run.</summary>
    /// <param name="model">The check's state, or <see langword="null"/> when the run has no mic check.</param>
    /// <param name="session">The session whose worker hears the clip.</param>
    /// <param name="listen">The push-to-talk of the run.</param>
    /// <param name="sound">Plays the buttons' clicks and the scripted recording's beeps.</param>
    /// <param name="canvas">The interface canvas the screen is laid out on.</param>
    /// <param name="font">The screen's font.</param>
    /// <param name="scriptedRun">Whether the run is scripted.</param>
    /// <param name="moreToCome">Whether a scripted run has steps (or a battery) after the check, so the check goes on by itself.</param>
    /// <param name="console">Where the check's lines go.</param>
    public MicCheckScreen(MicCheckModel? model, VoxelSession session, ListenSession listen, SoundFrontend sound, UiCanvas canvas, Func<SpriteFontBase> font, bool scriptedRun, Func<bool> moreToCome, Action<string> console)
    {
        Model = model;
        _session = session;
        _listen = listen;
        _sound = sound;
        _canvas = canvas;
        _font = font;
        _scriptedRun = scriptedRun;
        _moreToCome = moreToCome;
        _console = console;
    }

    /// <summary>The check's state, which the layer draws, or <see langword="null"/> when the run has none.</summary>
    public MicCheckModel? Model { get; }

    /// <summary>Whether the check is on screen.</summary>
    public bool Active => Model is { IsActive: true };

    /// <inheritdoc />
    public string Name => "mic check";

    /// <inheritdoc />
    public bool IsOpen => Active;

    /// <inheritdoc />
    public bool FreesMouse => true;

    /// <summary>The check's keys: hold V or Space to talk, Enter or Space to go on after a pass, Esc to skip.</summary>
    public void HandleInput(IInputFrame input)
    {
        var check = Model!;
        _listen.ReleaseIfUp(input);
        if (input.Pressed(GameKey.Escape))
        {
            _listen.Cancel();
            check.Skip();
            return;
        }

        switch (check.Phase)
        {
            case MicCheckPhase.Passed:
                if (input.Pressed(GameKey.Enter) || input.Pressed(GameKey.Space))
                {
                    check.Continue();
                }

                break;
            case MicCheckPhase.Ready:
            case MicCheckPhase.Failed:
                if (input.Pressed(GameKey.V) || input.Pressed(GameKey.Space))
                {
                    BeginListening(input.Pressed(GameKey.V) ? GameKey.V : GameKey.Space);
                }
                else if (input.Pressed(GameKey.Enter) && check.Phase == MicCheckPhase.Failed)
                {
                    check.Retry();
                }

                break;
            default:
                break;
        }
    }

    /// <summary>A click at a point of the canvas: a button of the screen.</summary>
    public void Click(int x, int y)
    {
        var check = Model!;
        if (MicCheckLayer.LayoutFor(check, _canvas.Width, _canvas.Height, _font()).ButtonAt(x, y) is not { } button)
        {
            return;
        }

        _sound.QueueClick(SoundCue.UiClick);
        if (button == MicCheckButton.Skip)
        {
            _listen.Cancel();
        }

        check.Press(button);
    }

    /// <summary>Once a frame while nothing is recorded: the meter falls back.</summary>
    public void FeedWhileIdle(double seconds)
    {
        if (Model is { IsActive: true } check && !_listen.Listening)
        {
            check.Feed(0, seconds);
        }
    }

    /// <summary>F8 while the check records: the same as a clip too short to hear, the check asks for another try, and nothing is sent.</summary>
    public void InterruptForReport()
    {
        if (Model is { Phase: MicCheckPhase.Listening } check)
        {
            check.EndListening();
            check.HeardNothing();
        }
    }

    /// <summary>
    /// Once a frame in a scripted run: once the recognizer is loaded the worker synthesizes "hola" and hears it, as if it had been said. With steps
    /// to run after it (or a check battery waiting behind it, S12: the playtest's pre-check), the check goes on by itself once its result has been
    /// on screen for a moment; alone it stays up for the screenshot.
    /// </summary>
    public void DriveScripted()
    {
        if (Model is not { IsActive: true } check || !_scriptedRun)
        {
            return;
        }

        var worker = _session.Worker!;
        if (!_scriptedSent && worker.IsReady)
        {
            _scriptedSent = true;
            if (worker.CanHear)
            {
                _sound.Scripted.Begin(); // the scripted check stands for a recording: beep, silent window, beep (S24)
                _ = _session.SubmitHearSpeak(MicCheck.Word);
                check.BeginScripted();
            }
            else
            {
                check.BeginScripted();
                check.Complete(new MicCheckResult(string.Empty, false, 0));
            }
        }

        if (check.Phase is MicCheckPhase.Passed or MicCheckPhase.Failed && _moreToCome() && ++_shownTicks >= 60)
        {
            check.Press(check.Phase == MicCheckPhase.Passed ? MicCheckButton.Continue : MicCheckButton.Skip);
        }
    }

    // The talk key went down. The recognizer must be loaded and the machine must have a microphone; otherwise the screen says why.
    private void BeginListening(GameKey key)
    {
        var check = Model!;
        var worker = _session.Worker!;
        if (!worker.CanHear)
        {
            check.Notice = worker.IsReady ? worker.Load?.Problem ?? "Speech is not available." : "Loading speech…";
            return;
        }

        if (_listen.Begin(new ListenTarget(Heard, check.Feed), key))
        {
            check.Notice = null;
            check.BeginListening();
        }
        else
        {
            check.Notice = _listen.Problem;
        }
    }

    // The key went up (or the recording hit its cap). The clip goes to the worker, which hears it and answers with what it heard.
    private void Heard(PcmClip? clip)
    {
        var check = Model!;
        check.EndListening();
        if (clip is null)
        {
            check.Notice = _listen.Problem;
            check.HeardNothing();
            return;
        }

        _ = _session.SubmitHear(clip);
        _console($"mic check: {clip.DurationSeconds:0.00} s heard by the recognizer");
    }
}
