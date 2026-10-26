using Vocaterra.Game.Core.Language.Speech;

namespace Vocaterra.Game.Core.Event;

/// <summary>
/// The kinds of voice event (D-game-9): everything about the microphone, the recognition and the speech controls that is not the language
/// itself. S6 raised only the uncertain recognition; S7 adds the mic check, the cancel and the timeout, and one event for every press of a
/// speech button. None of them is a language failure.
/// </summary>
public enum VoiceEventKind
{
    /// <summary>
    /// A spoken line was an uncertain recognition: the core did not understand it, or the recognizer's confidence was under
    /// <see cref="UncertaintyRule.ConfidenceThreshold"/>. The NPC asked for another try. Never counted as a language failure.
    /// </summary>
    Uncertain,

    /// <summary>The player cancelled a turn that was still being recognized or evaluated (Esc while "Pensando…"), or a recording.</summary>
    Cancel,

    /// <summary>The watchdog fired: a turn took longer than the front-end's limit and typing was offered.</summary>
    Timeout,

    /// <summary>The mic check at the start was passed (the recognizer heard "hola").</summary>
    MicCheckPassed,

    /// <summary>The mic check was not passed (nothing heard, or something else): the player may try again or skip.</summary>
    MicCheckFailed,

    /// <summary>The mic check was skipped.</summary>
    MicCheckSkipped,

    /// <summary>The Repetir button: the NPC's last line again.</summary>
    Replay,

    /// <summary>The Más despacio button: the NPC's last line again, slowly.</summary>
    Slower,

    /// <summary>The Ayuda button: the player asked for help.</summary>
    Hint,

    /// <summary>The No sé button: the player said they do not know.</summary>
    NoSe,

    /// <summary>The Escribir button: the player chose to type.</summary>
    TypeFallback,

    /// <summary>A choice offered by the repair ladder was picked (the turn that follows is marked as a choice).</summary>
    Choice,
}

/// <summary>The wire names of the voice events for the console and the session log.</summary>
public static class VoiceEventNames
{
    /// <summary>The wire name of a voice-event kind: stable lowercase snake case.</summary>
    public static string Of(VoiceEventKind kind) => kind switch
    {
        VoiceEventKind.Uncertain => "uncertain",
        VoiceEventKind.Cancel => "cancel",
        VoiceEventKind.Timeout => "timeout",
        VoiceEventKind.MicCheckPassed => "mic_check_passed",
        VoiceEventKind.MicCheckFailed => "mic_check_failed",
        VoiceEventKind.MicCheckSkipped => "mic_check_skipped",
        VoiceEventKind.Replay => "replay",
        VoiceEventKind.Slower => "slower",
        VoiceEventKind.Hint => "hint",
        VoiceEventKind.NoSe => "no_se",
        VoiceEventKind.TypeFallback => "typed_fallback",
        VoiceEventKind.Choice => "choice",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown voice-event kind."),
    };
}

/// <summary>A voice event: something about the microphone and the recognition rather than the language (D-game-9).</summary>
/// <param name="Tick">The simulation tick of the turn (or of the moment, for a control).</param>
/// <param name="Time">The stored time of the turn.</param>
/// <param name="Kind">What happened.</param>
/// <param name="Npc">The NPC the line was said to, or the NPC the panel was open on; <see langword="null"/> for the mic check at the start.</param>
/// <param name="Detail">What was heard, the line concerned or the reason, or <see langword="null"/>.</param>
public sealed record VoiceEvent(long Tick, long Time, VoiceEventKind Kind, string? Npc, string? Detail = null) : GameEvent(Tick, Time)
{
    /// <inheritdoc />
    public override string Family => "voice";

    /// <inheritdoc />
    public override string Text =>
        $"voice {VoiceEventNames.Of(Kind)}{(Npc is null ? string.Empty : $" at {Npc}")}{(Detail is null ? string.Empty : $" (\"{Detail}\")")}";
}
