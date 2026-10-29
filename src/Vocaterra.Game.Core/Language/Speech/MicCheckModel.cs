using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Session;

namespace Vocaterra.Game.Core.Language.Speech;

/// <summary>
/// The mic check at the start (D-game-9, S7) as plain state: a live level meter from the capture's RMS while the talk key is held, the instruction
/// <see cref="Instruction"/>, what the recognizer heard (<c>Heard: «hola»</c>) and a pass, retry or skip result. It never blocks the game: it can be
/// skipped from any phase, and the result is logged as a voice event (<c>mic_check_passed</c>, <c>mic_check_failed</c>, <c>mic_check_skipped</c>).
/// </summary>
public sealed class MicCheckModel
{

    /// <summary>How fast the meter falls back, per second (a fraction of full scale).</summary>
    public const double FallPerSecond = 1.5;

    private readonly Action<VoiceEventKind, string?> _log;
    private readonly LocaleText _ui;
    private readonly string _word;

    /// <summary>Creates the check, ready for the first try.</summary>
    /// <param name="log">Where the check's voice events go (the session's <see cref="GuideSession.LogMicCheck"/>).</param>
    /// <param name="ui">The support language (<c>ui.mic*</c>, C04).</param>
    /// <param name="word">The word the player says (<c>hola</c>, the greeting of <c>game/words.json</c>); it stays the target language.</param>
    public MicCheckModel(Action<VoiceEventKind, string?> log, LocaleText ui, string word)
    {
        ArgumentNullException.ThrowIfNull(log);
        _log = log;
        _ui = ui ?? throw new ArgumentNullException(nameof(ui));
        _word = word ?? throw new ArgumentNullException(nameof(word));
    }

    /// <summary>The instruction the screen shows (<c>Say "hola"</c>).</summary>
    public string Instruction => _ui.Ui("micSay", ("word", _word));

    /// <summary>The title of the screen (S17b: English; the word the player says, <c>hola</c>, stays Spanish).</summary>
    public string Title => _ui.Ui("micTitle");

    /// <summary>How to talk (the held key).</summary>
    public string TalkHint => _ui.Ui("micTalkHint", ("word", _word));

    /// <summary>The result line after a pass.</summary>
    public string PassedText => _ui.Ui("micPassed");

    /// <summary>The result line after a failed try.</summary>
    public string FailedText => _ui.Ui("micFailed");

    /// <summary>The label of the button that leaves the screen after a pass.</summary>
    public string ContinueLabel => _ui.Ui("micContinue");

    /// <summary>The label of the button that tries again.</summary>
    public string RetryLabel => _ui.Ui("micRetry");

    /// <summary>The label of the button that skips the check.</summary>
    public string SkipLabel => _ui.Ui("micSkip");

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
        MicCheckPhase.Listening => _ui.Ui("dialogueListening"),
        MicCheckPhase.Thinking => _ui.Ui("dialogueThinking"),
        MicCheckPhase.Passed => PassedText,
        MicCheckPhase.Failed => FailedText,
        _ => TalkHint,
    };

    /// <summary>The line <c>Heard: «…»</c> once something was heard, or <see langword="null"/>.</summary>
    public string? HeardText => Heard is null ? null : Heard.Length == 0 ? _ui.Ui("heardNothing") : _ui.Ui("heard", ("heard", Heard));

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
    public string LabelOf(MicCheckButton button) => LabelOf(_ui, button);

    /// <summary>The label of a button in a support language (<c>ui.micContinue</c>, <c>ui.micRetry</c>, <c>ui.micSkip</c>).</summary>
    /// <param name="ui">The support language.</param>
    /// <param name="button">The button.</param>
    public static string LabelOf(LocaleText ui, MicCheckButton button) => (ui ?? throw new ArgumentNullException(nameof(ui))).Ui(button switch
    {
        MicCheckButton.Continue => "micContinue",
        MicCheckButton.Retry => "micRetry",
        _ => "micSkip",
    });
}
