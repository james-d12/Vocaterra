using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Language.Words;

namespace Vocaterra.Game.Core.Activities;

/// <summary>Where a step stands in the strip's row of dots.</summary>
public enum StripDotState
{
    /// <summary>The step is done.</summary>
    Done,

    /// <summary>The step the next act is judged against.</summary>
    Current,

    /// <summary>A step still to come.</summary>
    Todo,
}

/// <summary>Which of the strip's three actions a button is.</summary>
public enum StripActionKind
{
    /// <summary><c>Repetir</c> (R): play the row again.</summary>
    Repeat,

    /// <summary><c>Más despacio</c> (S): play the slow recording.</summary>
    Slower,

    /// <summary><c>Ayuda</c> (H): the English and the highlight, and the row steps up a rung.</summary>
    Help,
}

/// <summary>One of the strip's three actions.</summary>
/// <param name="Kind">Which action.</param>
/// <param name="Label">The label in the target language (<c>game/words.json</c> buttons).</param>
/// <param name="Key">The key that presses it.</param>
/// <param name="Enabled">Whether it can be pressed now (a run at a step; a replay needs a recording that can be heard).</param>
public sealed record StripAction(StripActionKind Kind, string Label, GameKey Key, bool Enabled);

/// <summary>
/// The instruction strip (W07, design 10.8): the row being taught, its Spanish when the rung shows it, its English when the rung or a help shows it, the step dots and the
/// Repetir, Más despacio and Ayuda actions. It is a model of its own, built from a <see cref="ProcedureView"/> and nothing else of the station, so that a page of a carried
/// recipe book (the owner's later plan, W30) can show the same strip beside the station.
/// </summary>
/// <param name="Row">The does row's id, or <see langword="null"/> when no row is taught (no run, or the bake is done).</param>
/// <param name="Unit">Whether the row is a step, a chunk or the goal.</param>
/// <param name="Rung">The row's rung, 1 to 4 (0 with no run).</param>
/// <param name="Spanish">The row's Spanish when it is shown, else <see langword="null"/>.</param>
/// <param name="English">The row's English when it is shown (rung 1, or after help), else <see langword="null"/>.</param>
/// <param name="CardForced">Whether the Spanish shows only because the row cannot be heard (no device, muted, no recording): the audio-only rung shows its card (decision 19).</param>
/// <param name="Dots">One dot per step of the procedure.</param>
/// <param name="Actions">Repetir, Más despacio and Ayuda, in that order.</param>
public sealed record InstructionStrip(
    string? Row,
    UnitKind Unit,
    int Rung,
    string? Spanish,
    string? English,
    bool CardForced,
    IReadOnlyList<StripDot> Dots,
    IReadOnlyList<StripAction> Actions)
{
    /// <summary>Whether the Spanish text is on screen.</summary>
    public bool ShowsText => Spanish is not null;

    /// <summary>Whether the English is on screen.</summary>
    public bool ShowsEnglish => English is not null;

    /// <summary>The strip with nothing to teach.</summary>
    public static InstructionStrip Empty { get; } = new(null, UnitKind.Step, 0, null, null, false, [], []);

    /// <summary>The strip of a view of the run.</summary>
    /// <param name="view">What the runner shows now.</param>
    /// <param name="row">The does row of <see cref="ProcedureView.Row"/> in the phrase sheet, or <see langword="null"/>.</param>
    /// <param name="buttons">The three actions' labels.</param>
    /// <param name="audible">Whether the row can be heard (<see cref="IInstructionAudio.CanPlay"/>); when it cannot, the Spanish card is shown whatever the rung (decision 19).</param>
    public static InstructionStrip From(ProcedureView view, PhraseRow? row, ButtonWords buttons, bool audible)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(buttons);
        if (view.State == ProcedureState.NotStarted)
        {
            return Empty;
        }

        var running = view.State == ProcedureState.InRun && row is not null;
        var forced = running && !view.ShowText && !audible;
        var text = running && (view.ShowText || forced) ? row!.Spanish : null;
        var english = running && view.ShowEnglish ? row!.English : null;
        var dots = new List<StripDot>(view.StepsTotal);
        for (var i = 0; i < view.StepsTotal; i++)
        {
            dots.Add(new StripDot(i < view.StepsDone ? StripDotState.Done : i == view.StepsDone && running ? StripDotState.Current : StripDotState.Todo));
        }

        IReadOnlyList<StripAction> actions =
        [
            new(StripActionKind.Repeat, buttons.Repeat, GameKey.R, running && audible),
            new(StripActionKind.Slower, buttons.Slower, GameKey.S, running && audible),
            new(StripActionKind.Help, buttons.Help, GameKey.H, running),
        ];
        return new InstructionStrip(running ? view.Row : null, view.Unit, view.Rung, text, english, forced, dots, actions);
    }
}

/// <summary>One dot of the strip.</summary>
/// <param name="State">Where the step stands.</param>
public sealed record StripDot(StripDotState State);
