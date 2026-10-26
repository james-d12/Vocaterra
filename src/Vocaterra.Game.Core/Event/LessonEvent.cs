using System.Globalization;

namespace Vocaterra.Game.Core.Event;

/// <summary>The kinds of lesson event (S16): the lesson runner writes one for each thing a step does, so the session log can follow a lesson step by step.</summary>
public enum LessonEventKind
{
    /// <summary>A lesson began.</summary>
    LessonStarted,

    /// <summary>A step began.</summary>
    StepStarted,

    /// <summary>A step ended; the outcome says how (<c>done</c>, <c>skipped</c> or <c>moved_on</c>).</summary>
    StepDone,

    /// <summary>The help path of a step was used (the English hint and the model line again).</summary>
    Help,

    /// <summary>An attempt at a step did not work (an uncertain or wrong turn, a wrong pick) and the step stays for another try.</summary>
    Retry,

    /// <summary>The lesson ended: every step is done, skipped or moved on from.</summary>
    LessonCompleted,

    /// <summary>The learner asked to practise a completed lesson again from the book (S26); it starts when they talk to its NPC.</summary>
    ReplayRequested,
}

/// <summary>How a lesson step ended.</summary>
public enum StepOutcome
{
    /// <summary>The learner did what the step asked.</summary>
    Done,

    /// <summary>The learner skipped the step.</summary>
    Skipped,

    /// <summary>The step moved on without the learner doing it: an uncertain or wrong turn ran out of attempts (fail-forward, D-game-9). Never a language failure.</summary>
    MovedOn,
}

/// <summary>The wire names of the lesson enumerations: stable lowercase snake case for the console, the save and the session log.</summary>
public static class LessonEventNames
{
    /// <summary>The wire name of a lesson-event kind.</summary>
    /// <param name="kind">The kind.</param>
    public static string Of(LessonEventKind kind) => kind switch
    {
        LessonEventKind.LessonStarted => "lesson_started",
        LessonEventKind.StepStarted => "step_started",
        LessonEventKind.StepDone => "step_done",
        LessonEventKind.Help => "help",
        LessonEventKind.Retry => "retry",
        LessonEventKind.LessonCompleted => "lesson_completed",
        LessonEventKind.ReplayRequested => "replay_requested",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown lesson-event kind."),
    };

    /// <summary>The wire name of a step outcome: <c>done</c>, <c>skipped</c> or <c>moved_on</c>.</summary>
    /// <param name="outcome">The outcome.</param>
    public static string Of(StepOutcome outcome) => outcome switch
    {
        StepOutcome.Done => "done",
        StepOutcome.Skipped => "skipped",
        StepOutcome.MovedOn => "moved_on",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown step outcome."),
    };

    /// <summary>The step outcome a wire name stands for.</summary>
    /// <param name="name">The wire name.</param>
    /// <exception cref="ArgumentException">The name is not an outcome.</exception>
    public static StepOutcome OutcomeOf(string name) => name switch
    {
        "done" => StepOutcome.Done,
        "skipped" => StepOutcome.Skipped,
        "moved_on" => StepOutcome.MovedOn,
        _ => throw new ArgumentException($"'{name}' is not a step outcome.", nameof(name)),
    };
}

/// <summary>
/// A step of a lesson happened (S16): the lesson began or ended, a step began or ended with its outcome, the help path was used, or an attempt did not
/// work and the step stays for another try. None of it is a language failure (D-game-9); the session log writes each one.
/// </summary>
/// <param name="Tick">The simulation tick.</param>
/// <param name="Time">The stored time.</param>
/// <param name="Kind">What happened.</param>
/// <param name="Lesson">The lesson's id.</param>
/// <param name="Step">The step's number from 1 (0 for the lesson as a whole).</param>
/// <param name="StepId">The step's id (empty for the lesson as a whole).</param>
/// <param name="StepType">The step's type (<c>repeat</c>...; empty for the lesson as a whole).</param>
/// <param name="Outcome">For <see cref="LessonEventKind.StepDone"/>, the outcome.</param>
/// <param name="Detail">What the step was about, or the reason, or <see langword="null"/>.</param>
public sealed record LessonEvent(
    long Tick,
    long Time,
    LessonEventKind Kind,
    string Lesson,
    int Step,
    string StepId,
    string StepType,
    StepOutcome? Outcome = null,
    string? Detail = null)
    : GameEvent(Tick, Time)
{
    /// <inheritdoc />
    public override string Family => "lesson";

    /// <inheritdoc />
    public override string Text
    {
        get
        {
            var detail = Detail is null ? string.Empty : $" ({Detail})";
            return Kind switch
            {
                LessonEventKind.LessonStarted => $"lesson {Lesson} started{detail}",
                LessonEventKind.LessonCompleted => $"lesson {Lesson} completed{detail}",
                LessonEventKind.ReplayRequested => $"lesson {Lesson} practise again asked{detail}",
                LessonEventKind.StepDone => string.Create(CultureInfo.InvariantCulture, $"lesson {Lesson} step {Step} {StepType} ({StepId}) {LessonEventNames.Of(Outcome ?? StepOutcome.Done)}{detail}"),
                _ => string.Create(CultureInfo.InvariantCulture, $"lesson {Lesson} step {Step} {StepType} ({StepId}) {LessonEventNames.Of(Kind)}{detail}"),
            };
        }
    }
}
