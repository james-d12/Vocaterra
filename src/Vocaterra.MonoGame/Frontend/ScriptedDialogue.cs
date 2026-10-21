using Vocaterra.Game;

namespace Vocaterra.MonoGame.Frontend;

/// <summary>
/// The scripted run for <c>--say</c>: it walks the player to the guide, waits for the body to settle, opens the dialogue panel, types each
/// line into the text box and asks the game to send it, one line at a time, then does nothing. It is an <see cref="IInputSource"/> because
/// it is polled once per tick like the keyboard, but it only returns movement while it is walking; the typing goes through the same
/// <see cref="DialogueModel"/> calls a person's keys make, and the send is done by the game between ticks (the model's
/// <see cref="DialogueModel.SubmitRequested"/>), never from inside a step.
/// </summary>
public sealed class ScriptedDialogue : IInputSource
{
    /// <summary>The walk gives up after this many ticks (the panel then opens wherever the player stands), so a blocked path cannot hang a run.</summary>
    public const int MaxWalkTicks = 900;

    private readonly GameSession _session;
    private readonly DialogueModel _dialogue;
    private readonly IReadOnlyList<string> _lines;
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
        Typing,
        Sending,
        Done,
    }

    /// <summary>Creates the script.</summary>
    /// <param name="session">The running game (for the guide's place and the player's body).</param>
    /// <param name="dialogue">The panel to drive.</param>
    /// <param name="lines">The lines to say, in order.</param>
    /// <param name="settleTicks">Ticks to wait after arriving, so the body has stopped.</param>
    /// <param name="gapTicks">Ticks between opening, typing and sending, so a screenshot taken mid-script shows each step.</param>
    public ScriptedDialogue(GameSession session, DialogueModel dialogue, IReadOnlyList<string> lines, int settleTicks = 20, int gapTicks = 6)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(dialogue);
        ArgumentNullException.ThrowIfNull(lines);
        _session = session;
        _dialogue = dialogue;
        _lines = lines;
        _settleTicks = settleTicks;
        _gapTicks = gapTicks;
    }

    /// <summary>Whether every line has been sent.</summary>
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
                    _dialogue.Open();
                    _phase = _lines.Count == 0 ? Phase.Done : Phase.Typing;
                    _wait = _gapTicks;
                }

                break;
            case Phase.Typing:
                if (--_wait <= 0)
                {
                    _dialogue.Entry.Insert(_lines[_next]);
                    _dialogue.RequestSubmit();
                    _phase = Phase.Sending;
                }

                break;
            case Phase.Sending:
                if (!_dialogue.SubmitRequested)
                {
                    _next++;
                    _phase = _next < _lines.Count ? Phase.Typing : Phase.Done;
                    _wait = _gapTicks;
                }

                break;
            default:
                break;
        }

        return InputActions.None;
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
