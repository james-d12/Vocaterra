using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.UI;

namespace Vocaterra.Game.Sound;

/// <summary>Where the mic check is.</summary>
public enum MicCheckPhase
{
    /// <summary>Waiting for the player to hold the talk key and say "hola".</summary>
    Ready,

    /// <summary>The key is held and the microphone records: the level meter moves.</summary>
    Listening,

    /// <summary>The clip is with the recognizer.</summary>
    Thinking,

    /// <summary>The recognizer heard "hola".</summary>
    Passed,

    /// <summary>The recognizer did not hear "hola": try again or skip.</summary>
    Failed,

    /// <summary>The screen is gone (continued after a pass, or skipped).</summary>
    Done,
}

/// <summary>
/// The mic check at the start (D-game-9, S7) as plain state: a live level meter from the capture's RMS while the talk key is held, the instruction
/// <see cref="Instruction"/>, what the recognizer heard (<c>Heard: «hola»</c>) and a pass, retry or skip result. It never blocks the game: it can be
/// skipped from any phase, and the result is logged as a voice event (<c>mic_check_passed</c>, <c>mic_check_failed</c>, <c>mic_check_skipped</c>).
/// </summary>
public sealed class MicCheckModel
{
    /// <summary>The instruction the screen shows.</summary>
    public const string Instruction = "Say \"hola\"";

    /// <summary>The title of the screen (S17b: English; the word the player says, <c>hola</c>, stays Spanish).</summary>
    public const string Title = "Microphone check";

    /// <summary>How to talk (the held key).</summary>
    public const string TalkHint = "Hold V or Space and say \"hola\"";

    /// <summary>The result line after a pass.</summary>
    public const string PassedText = "Got it, I can hear you.";

    /// <summary>The result line after a failed try.</summary>
    public const string FailedText = "I didn't catch that. Try again?";

    /// <summary>The label of the button that leaves the screen after a pass.</summary>
    public const string ContinueLabel = "Continue";

    /// <summary>The label of the button that tries again.</summary>
    public const string RetryLabel = "Try again";

    /// <summary>The label of the button that skips the check.</summary>
    public const string SkipLabel = "Skip";

    /// <summary>How fast the meter falls back, per second (a fraction of full scale).</summary>
    public const double FallPerSecond = 1.5;

    private readonly Action<VoiceEventKind, string?> _log;

    /// <summary>Creates the check, ready for the first try.</summary>
    /// <param name="log">Where the check's voice events go (the session's <see cref="GuideSession.LogMicCheck"/>).</param>
    public MicCheckModel(Action<VoiceEventKind, string?> log)
    {
        ArgumentNullException.ThrowIfNull(log);
        _log = log;
    }

    /// <summary>Where the check is.</summary>
    public MicCheckPhase Phase { get; private set; } = MicCheckPhase.Ready;

    /// <summary>Whether the screen is up.</summary>
    public bool IsActive => Phase != MicCheckPhase.Done;

    /// <summary>The meter, 0 to 1: it jumps to a louder level and falls back slowly.</summary>
    public double Level { get; private set; }

    /// <summary>
    /// How much of the meter to light, 0 to 1: the live level while the key is held, and the loudest level of the try (held) once the clip is with the
    /// recognizer and while the result is up. Speech sits low on a linear scale (an RMS of 0.1 is already loud talking), so the meter follows the
    /// square root.
    /// </summary>
    public double MeterFill => Math.Min(1.0, Math.Sqrt(Math.Clamp(Phase is MicCheckPhase.Thinking or MicCheckPhase.Passed or MicCheckPhase.Failed ? Peak : Level, 0, 1)) * 1.6);

    /// <summary>The loudest level of the current try, 0 to 1.</summary>
    public double Peak { get; private set; }

    /// <summary>What the recognizer heard in the last try, or <see langword="null"/> before any.</summary>
    public string? Heard { get; private set; }

    /// <summary>The tries made so far.</summary>
    public int Tries { get; private set; }

    /// <summary>A message about the microphone that replaces the status line (no microphone, the voice is not loaded), or <see langword="null"/>. The player can always skip.</summary>
    public string? Notice { get; set; }

    /// <summary>The line under the instruction for the phase: how to talk, or the result.</summary>
    public string StatusText => Notice is not null && Phase is MicCheckPhase.Ready or MicCheckPhase.Failed ? Notice : Phase switch
    {
        MicCheckPhase.Listening => "Listening…",
        MicCheckPhase.Thinking => "Thinking…",
        MicCheckPhase.Passed => PassedText,
        MicCheckPhase.Failed => FailedText,
        _ => TalkHint,
    };

    /// <summary>The line <c>Heard: «…»</c> once something was heard, or <see langword="null"/>.</summary>
    public string? HeardText => Heard is null ? null : $"{HeardLine.HeardLabel}{(Heard.Length == 0 ? HeardLine.NothingHeard : $"«{Heard}»")}";

    /// <summary>The talk key went down: the meter starts to move. Does nothing unless the check is ready (or failed: a new try starts).</summary>
    public void BeginListening()
    {
        if (Phase is MicCheckPhase.Ready or MicCheckPhase.Failed)
        {
            Phase = MicCheckPhase.Listening;
            Peak = 0;
            Level = 0;
        }
    }

    /// <summary>One frame of the meter: <paramref name="level"/> is the RMS of what the microphone gave this frame (0 to 1), <paramref name="seconds"/> the frame's time.</summary>
    /// <param name="level">The level of this frame's samples.</param>
    /// <param name="seconds">The frame clock's seconds.</param>
    public void Feed(double level, double seconds)
    {
        var clamped = Math.Clamp(level, 0, 1);
        Level = Math.Max(clamped, Level - (FallPerSecond * Math.Max(0, seconds)));
        if (Phase == MicCheckPhase.Listening)
        {
            Peak = Math.Max(Peak, clamped);
        }
    }

    /// <summary>The talk key went up and the clip went to the recognizer.</summary>
    public void EndListening()
    {
        if (Phase == MicCheckPhase.Listening)
        {
            Phase = MicCheckPhase.Thinking;
            Tries++;
        }
    }

    /// <summary>The recording was silence (nothing to recognize): the try failed at once.</summary>
    public void HeardNothing() => Complete(new MicCheckResult(string.Empty, false, Peak));

    /// <summary>
    /// A scripted check (no microphone): the clip is with the recognizer without a recording. The phase goes to thinking.
    /// </summary>
    public void BeginScripted()
    {
        if (Phase is MicCheckPhase.Ready or MicCheckPhase.Failed)
        {
            Phase = MicCheckPhase.Thinking;
            Tries++;
        }
    }

    /// <summary>The recognizer answered. Passes or fails the try and logs it.</summary>
    /// <param name="result">What was heard.</param>
    public void Complete(MicCheckResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (Phase is not (MicCheckPhase.Thinking or MicCheckPhase.Listening))
        {
            return;
        }

        Heard = result.Heard;
        Peak = Math.Max(Peak, result.Level);
        Level = Math.Max(Level, result.Level);
        Phase = result.Passed ? MicCheckPhase.Passed : MicCheckPhase.Failed;
        _log(result.Passed ? VoiceEventKind.MicCheckPassed : VoiceEventKind.MicCheckFailed, result.Heard);
    }

    /// <summary>Leaves the screen after a pass.</summary>
    public void Continue()
    {
        if (Phase == MicCheckPhase.Passed)
        {
            Phase = MicCheckPhase.Done;
        }
    }

    /// <summary>Tries again after a failed try.</summary>
    public void Retry()
    {
        if (Phase == MicCheckPhase.Failed)
        {
            Phase = MicCheckPhase.Ready;
            Heard = null;
            Level = 0;
            Peak = 0;
        }
    }

    /// <summary>Skips the check (from any phase) and logs it. Nothing is lost: the game never needs the check to go on.</summary>
    public void Skip()
    {
        if (Phase != MicCheckPhase.Done)
        {
            Phase = MicCheckPhase.Done;
            _log(VoiceEventKind.MicCheckSkipped, Heard);
        }
    }

    /// <summary>The buttons the phase shows, in order: <c>Seguir</c> after a pass, <c>Otra vez</c> after a failed try, and <c>Saltar</c> unless the check passed.</summary>
    public IReadOnlyList<MicCheckButton> Buttons => Phase switch
    {
        MicCheckPhase.Passed => [MicCheckButton.Continue],
        MicCheckPhase.Failed => [MicCheckButton.Retry, MicCheckButton.Skip],
        MicCheckPhase.Done => [],
        _ => [MicCheckButton.Skip],
    };

    /// <summary>Presses a button of the screen.</summary>
    /// <param name="button">The button.</param>
    public void Press(MicCheckButton button)
    {
        switch (button)
        {
            case MicCheckButton.Continue:
                Continue();
                break;
            case MicCheckButton.Retry:
                Retry();
                break;
            default:
                Skip();
                break;
        }
    }

    /// <summary>The label of a button.</summary>
    /// <param name="button">The button.</param>
    public static string LabelOf(MicCheckButton button) => button switch
    {
        MicCheckButton.Continue => ContinueLabel,
        MicCheckButton.Retry => RetryLabel,
        _ => SkipLabel,
    };
}

/// <summary>The buttons of the mic-check screen.</summary>
public enum MicCheckButton
{
    /// <summary><c>Seguir</c>: leave the screen after a pass.</summary>
    Continue,

    /// <summary><c>Otra vez</c>: try again.</summary>
    Retry,

    /// <summary><c>Saltar</c>: skip the check.</summary>
    Skip,
}

/// <summary>Where the mic-check screen's pieces are for a window, so that drawing and the mouse hit-test agree.</summary>
public readonly record struct MicCheckLayout
{
    /// <summary>The width of the screen's box.</summary>
    public const int Width = 560;

    /// <summary>The height of the screen's box.</summary>
    public const int Height = 330;

    /// <summary>Computes the layout.</summary>
    /// <param name="viewportWidth">The window's width.</param>
    /// <param name="viewportHeight">The window's height.</param>
    /// <param name="buttons">The buttons to lay out.</param>
    /// <param name="measure">The width of a text in pixels.</param>
    public MicCheckLayout(int viewportWidth, int viewportHeight, IReadOnlyList<MicCheckButton> buttons, Func<string, float> measure)
    {
        ArgumentNullException.ThrowIfNull(buttons);
        ArgumentNullException.ThrowIfNull(measure);
        var width = Math.Min(Width, Math.Max(viewportWidth - 32, 200));
        Panel = new PixelBox((viewportWidth - width) / 2, Math.Max(8, (viewportHeight - Height) / 2), width, Height);
        Meter = new PixelBox(Panel.X + 40, Panel.Y + 150, Panel.Width - 80, 26);
        var placed = new List<(MicCheckButton Button, PixelBox Box)>();
        var x = Panel.X + 40;
        var y = Panel.Bottom - 54;
        foreach (var button in buttons)
        {
            var w = (int)Math.Ceiling(measure(MicCheckModel.LabelOf(button))) + 28;
            placed.Add((button, new PixelBox(x, y, w, 32)));
            x += w + 12;
        }

        Buttons = placed;
    }

    /// <summary>The screen's box.</summary>
    public PixelBox Panel { get; }

    /// <summary>The level meter's box.</summary>
    public PixelBox Meter { get; }

    /// <summary>The buttons with their boxes.</summary>
    public IReadOnlyList<(MicCheckButton Button, PixelBox Box)> Buttons { get; }

    /// <summary>The button under a point, or <see langword="null"/>.</summary>
    /// <param name="x">The pointer's x.</param>
    /// <param name="y">The pointer's y.</param>
    public MicCheckButton? ButtonAt(int x, int y)
    {
        foreach (var (button, box) in Buttons)
        {
            if (box.Contains(x, y))
            {
                return button;
            }
        }

        return null;
    }
}
