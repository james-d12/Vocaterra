using Vocaterra.Game;


namespace Vocaterra.MonoGame.Frontend;

/// <summary>
/// The two hand-overs between the dialogue panel and the turn worker, as plain code the game calls once per frame and the tests call the same
/// way: <see cref="Submit"/> sends what the panel asked for (the typed line, a scripted spoken line) and <see cref="Apply"/> takes the turns
/// the worker has finished, in the order they were submitted, applies them to the session (the arrow, the words) and shows them in the panel (a cancelled
/// turn is counted down and shows nothing; a mic-check outcome is left to the mic-check screen).
/// Neither waits for the worker.
/// </summary>
/// <param name="session">The game (the 3D scene), with its worker started.</param>
/// <param name="dialogue">The panel.</param>
/// <param name="cursor">The cell under the mouse, the scripted cursor or the crosshair at the moment of the call, for "¿qué es esto?".</param>
public sealed class TurnPump(GuideSession session, DialogueModel dialogue, Func<GuideCell?> cursor)
{
    /// <summary>
    /// Hands the worker the typed line, and the scripted spoken line (<c>--wav</c>, <c>--speak</c>), that the panel has asked to send. Call it
    /// between ticks, never inside a step.
    /// </summary>
    public void Submit()
    {
        if (dialogue.SubmitRequested)
        {
            var at = cursor();
            if (dialogue.TakeLine() is { } line)
            {
                _ = session.SubmitTyped(line, at);
            }
        }

        if (dialogue.TakeSpeechRequest() is { } speech)
        {
            var at = cursor();
            dialogue.BeginSpoken();
            _ = speech.Source == TurnSource.Wav ? session.SubmitWav(speech.Value, at) : session.SubmitSpeak(speech.Value, at);
        }
    }

    /// <summary>Applies the finished turns in submission order (see <see cref="GuideSession.Collect"/>), shows each in the panel and returns them.</summary>
    public IReadOnlyList<AppliedTurn> Apply()
    {
        var applied = session.Collect();
        foreach (var turn in applied)
        {
            if (!turn.Outcome.Request.IsMicCheck && turn.Outcome.Request.Check is null)
            {
                _ = dialogue.Complete(turn); // a mic check is no turn: the mic-check screen takes its outcome
            }
        }

        return applied;
    }
}
