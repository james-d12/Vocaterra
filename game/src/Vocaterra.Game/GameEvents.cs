using System.Globalization;

namespace Vocaterra.Game;

/// <summary>How a phrase of the phrase sheet (<c>content/PHRASES.md</c>) was used (D-game-10): the kinds S8 (phrase progress) and S11 (the session log) tell apart.</summary>
public enum PhraseUseKind
{
    /// <summary>The player answered the NPC's own question with the NPC's prompt and the help in view (the stall's "¿Qué necesitas?").</summary>
    Supported,

    /// <summary>The player said it on their own, with no question from the NPC to answer (asking the villager where the river is).</summary>
    Unaided,

    /// <summary>The player answered after the NPC offered choices (the repair ladder's third step): a recognition signal, not production.</summary>
    Choice,

    /// <summary>The player said "no sé" or asked for help: the NPC gave the model answer. Never a failure.</summary>
    DontKnow,

    /// <summary>The phrase was used with an NPC or in a situation other than the one it was taught in (the guide's "¿Quieres agua?", a request for bread at the villager).</summary>
    Transfer,

    /// <summary>The exchange ended without the phrase and the game carried on anyway: the player got what they needed without producing it. Supported, not a success.</summary>
    FailForward,
}

/// <summary>The wire names of the enumerations of the game events: stable lowercase snake case for the console and the session log.</summary>
public static class GameEventNames
{
    /// <summary>The wire name of a phrase-use kind: <c>supported</c>, <c>unaided</c>, <c>choice</c>, <c>dont_know</c>, <c>transfer</c>, <c>fail_forward</c>.</summary>
    public static string Of(PhraseUseKind kind) => kind switch
    {
        PhraseUseKind.Supported => "supported",
        PhraseUseKind.Unaided => "unaided",
        PhraseUseKind.Choice => "choice",
        PhraseUseKind.DontKnow => "dont_know",
        PhraseUseKind.Transfer => "transfer",
        PhraseUseKind.FailForward => "fail_forward",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown phrase-use kind."),
    };
}

/// <summary>
/// Something the game recorded for the learning measurement: a phrase use, a voice event or a world event. Plain values with no clock: the
/// <see cref="Tick"/> is the simulation's tick and <see cref="Time"/> the stored time (Unix seconds) when the line was submitted, so the same
/// run gives the same events. S8 reads the phrase uses into the phrase progress; S11 writes all of them into the session log.
/// </summary>
/// <param name="Tick">The simulation tick when the thing happened (a turn: when it was submitted).</param>
/// <param name="Time">The stored time in Unix seconds.</param>
public abstract record GameEvent(long Tick, long Time)
{
    /// <summary>A short wire name of the event's family: <c>phrase</c>, <c>voice</c> or <c>world</c>.</summary>
    public abstract string Family { get; }

    /// <summary>The event as one console line, for example <c>phrase pedir-agua supported at shopkeeper</c>.</summary>
    public abstract string Text { get; }
}

/// <summary>
/// A phrase of the phrase sheet was used (or the game carried on without it). <see cref="PhraseId"/> is the sheet's id, resolved from the intent
/// and the prompt of the turn (<see cref="PhraseIds"/>).
/// </summary>
/// <param name="Tick">The simulation tick of the turn.</param>
/// <param name="Time">The stored time of the turn.</param>
/// <param name="PhraseId">The id of the sheet row (<c>pedir-agua</c>).</param>
/// <param name="Kind">How it was used.</param>
/// <param name="Npc">The NPC it was said to (<c>shopkeeper</c>).</param>
/// <param name="Prompt">The prompt the turn answered (<c>stall-need</c>); empty for a fail-forward with no turn.</param>
/// <param name="Line">What the player said, as sent or heard; empty for a fail-forward with no line.</param>
/// <param name="Detail">The slot that matters (the item asked for, the place), or <see langword="null"/>.</param>
public sealed record PhraseUse(long Tick, long Time, string PhraseId, PhraseUseKind Kind, string Npc, string Prompt, string Line, string? Detail = null)
    : GameEvent(Tick, Time)
{
    /// <inheritdoc />
    public override string Family => "phrase";

    /// <inheritdoc />
    public override string Text =>
        string.Create(CultureInfo.InvariantCulture, $"phrase {PhraseId} {GameEventNames.Of(Kind)} at {Npc}{(Detail is null ? string.Empty : $" ({Detail})")}");
}

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

/// <summary>A thing that happened in the world: an item given, the beacon set, the bottle filled, a step done, the river found without Spanish.</summary>
/// <param name="Tick">The simulation tick.</param>
/// <param name="Time">The stored time.</param>
/// <param name="Name">What happened, one of <see cref="WorldEvents"/>.</param>
/// <param name="Npc">The NPC it is about, or <see langword="null"/>.</param>
/// <param name="Detail">What it is about (<c>botella</c>, <c>río</c>), or <see langword="null"/>.</param>
public sealed record WorldEvent(long Tick, long Time, string Name, string? Npc = null, string? Detail = null) : GameEvent(Tick, Time)
{
    /// <inheritdoc />
    public override string Family => "world";

    /// <inheritdoc />
    public override string Text => $"world {Name}{(Npc is null ? string.Empty : $" at {Npc}")}{(Detail is null ? string.Empty : $" ({Detail})")}";
}

/// <summary>The names of the world events (<see cref="WorldEvent.Name"/>).</summary>
public static class WorldEvents
{
    /// <summary>The panel opened on an NPC.</summary>
    public const string TalkOpened = "talk_opened";

    /// <summary>The panel closed.</summary>
    public const string TalkClosed = "talk_closed";

    /// <summary>An NPC put an item in the inventory (the detail is the item's noun and how it came: request, offer or fail_forward).</summary>
    public const string ItemGiven = "item_given";

    /// <summary>The villager's answer set the beacon on the nearest river block.</summary>
    public const string BeaconSet = "beacon_set";

    /// <summary>The player filled the bottle at the river.</summary>
    public const string BottleFilled = "bottle_filled";

    /// <summary>The player worked on water without a bottle: a hint was shown, nothing was blocked.</summary>
    public const string WaterWithoutBottle = "water_without_bottle";

    /// <summary>The player filled the bottle without ever having been pointed at the river by an NPC: they found it without Spanish.</summary>
    public const string FoundWithoutSpanish = "found_without_spanish";

    /// <summary>A step of the camp objective was done (the detail is the checklist's text of the step).</summary>
    public const string ObjectiveStep = "objective_step";

    /// <summary>The whole camp objective is complete.</summary>
    public const string ObjectiveComplete = "objective_complete";

    /// <summary>The guide made the transfer offer ("¿Quieres agua?") after the objective was complete.</summary>
    public const string TransferOffered = "transfer_offered";
}
