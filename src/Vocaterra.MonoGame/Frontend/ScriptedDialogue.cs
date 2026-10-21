using Vocaterra.Game;

namespace Vocaterra.MonoGame.Frontend;

/// <summary>What one step of the scripted run does.</summary>
public enum ScriptStepKind
{
    /// <summary>Type a line and send it (<c>--say</c>).</summary>
    Say,

    /// <summary>Move the stored time on without stepping the world (<c>--skip</c>).</summary>
    Skip,

    /// <summary>Close the dialogue and talk to the guide again, so that she may ask a review question (<c>--talk</c>).</summary>
    Talk,
}

/// <summary>One step of the scripted run, in the order the command line gave it.</summary>
/// <param name="Kind">What the step does.</param>
/// <param name="Line">For <see cref="ScriptStepKind.Say"/>, the line.</param>
/// <param name="Minutes">For <see cref="ScriptStepKind.Skip"/>, the minutes of stored time to skip.</param>
public sealed record ScriptStep(ScriptStepKind Kind, string Line = "", int Minutes = 0)
{
    /// <summary>A step that says a line.</summary>
    public static ScriptStep Say(string line) => new(ScriptStepKind.Say, line);

    /// <summary>A step that skips stored time.</summary>
    public static ScriptStep Skip(int minutes) => new(ScriptStepKind.Skip, Minutes: minutes);

    /// <summary>A step that talks to the guide again.</summary>
    public static ScriptStep Talk() => new(ScriptStepKind.Talk);
}

/// <summary>
/// The scripted run for <c>--say</c>: it walks the player to the guide, waits for the body to settle, opens the dialogue panel, types each
/// line into the text box and asks the game to send it, one line at a time, then does nothing. It is an <see cref="IInputSource"/> because
/// it is polled once per tick like the keyboard, but it only returns movement while it is walking; the typing goes through the same
/// <see cref="DialogueModel"/> calls a person's keys make, and the send is done by the game between ticks (the model's
/// <see cref="DialogueModel.SubmitRequested"/>), never from inside a step. Besides lines it runs <see cref="ScriptStepKind.Skip"/> (stored time
/// moves on, the world does not step) and <see cref="ScriptStepKind.Talk"/> (close and open the dialogue again, which is when the guide may ask a
/// review question), in the order given.
/// </summary>
public sealed class ScriptedDialogue : IInputSource
{
    /// <summary>The walk gives up after this many ticks (the panel then opens wherever the player stands), so a blocked path cannot hang a run.</summary>
    public const int MaxWalkTicks = 900;

    private readonly GameSession _session;
    private readonly DialogueModel _dialogue;
    private readonly IReadOnlyList<ScriptStep> _steps;
    private readonly int _settleTicks;
    private readonly int _gapTicks;
    private Phase _phase = Phase.Walking;
    private int _wait;
    private int _walked;
    private int _next;

    private enum Phase
    {
        Walking,
        Settling,
        Opening,
        Running,
        Sending,
        Done,
    }

    /// <summary>Creates the script.</summary>
    /// <param name="session">The running game (for the guide's place and the player's body).</param>
    /// <param name="dialogue">The panel to drive.</param>
    /// <param name="steps">What to do after opening the panel, in order.</param>
    /// <param name="settleTicks">Ticks to wait after arriving, so the body has stopped.</param>
    /// <param name="gapTicks">Ticks between opening, typing and sending, so a screenshot taken mid-script shows each step.</param>
    public ScriptedDialogue(GameSession session, DialogueModel dialogue, IReadOnlyList<ScriptStep> steps, int settleTicks = 20, int gapTicks = 6)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(dialogue);
        ArgumentNullException.ThrowIfNull(steps);
        _session = session;
        _dialogue = dialogue;
        _steps = steps;
        _settleTicks = settleTicks;
        _gapTicks = gapTicks;
    }

    /// <summary>Creates the script for lines only: one <see cref="ScriptStepKind.Say"/> step per line.</summary>
    public ScriptedDialogue(GameSession session, DialogueModel dialogue, IReadOnlyList<string> lines, int settleTicks = 20, int gapTicks = 6)
        : this(session, dialogue, (lines ?? throw new ArgumentNullException(nameof(lines))).Select(ScriptStep.Say).ToList(), settleTicks, gapTicks)
    {
    }

    /// <summary>Whether every step has been done.</summary>
    public bool IsDone => _phase == Phase.Done;

    /// <inheritdoc />
    public InputActions Poll(long tick)
    {
        switch (_phase)
        {
            case Phase.Walking:
                return Walk();
            case Phase.Settling:
                if (--_wait <= 0)
                {
                    _phase = Phase.Opening;
                    _wait = _gapTicks;
                }

                break;
            case Phase.Opening:
                if (--_wait <= 0)
                {
                    _dialogue.Open(_session.BeginTalk()?.Question);
                    _phase = _steps.Count == 0 ? Phase.Done : Phase.Running;
                    _wait = _gapTicks;
                }

                break;
            case Phase.Running:
                if (--_wait <= 0)
                {
                    RunStep(_steps[_next]);
                }

                break;
            case Phase.Sending:
                if (!_dialogue.SubmitRequested)
                {
                    Advance();
                }

                break;
            default:
                break;
        }

        return InputActions.None;
    }

    private void RunStep(ScriptStep step)
    {
        switch (step.Kind)
        {
            case ScriptStepKind.Say:
                _dialogue.Entry.Insert(step.Line);
                _dialogue.RequestSubmit();
                _phase = Phase.Sending;
                break;
            case ScriptStepKind.Skip:
                _session.SkipTime(step.Minutes * 60L);
                Console.WriteLine($"skip: {step.Minutes} min, {_session.SkippedSeconds} s skipped in all");
                Advance();
                break;
            default:
                _dialogue.Close();
                _session.EndReview();
                var review = _session.BeginTalk();
                _dialogue.Open(review?.Question);
                Console.WriteLine(review is null
                    ? "talk: no review"
                    : $"review: \"{review.Question}\" pointing at {review.Target.X},{review.Target.Y}");
                Advance();
                break;
        }
    }

    private void Advance()
    {
        _next++;
        _phase = _next < _steps.Count ? Phase.Running : Phase.Done;
        _wait = _gapTicks;
    }

    private InputActions Walk()
    {
        if (_session.IsNearGuide || ++_walked > MaxWalkTicks)
        {
            _phase = Phase.Settling;
            _wait = _settleTicks;
            return InputActions.None;
        }

        var player = _session.PlayerTile;
        return player.X < _session.Guide.Feet.X ? InputActions.Right : InputActions.Left;
    }
}
