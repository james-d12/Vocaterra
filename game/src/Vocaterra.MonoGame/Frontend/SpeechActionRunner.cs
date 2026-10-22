using Vocaterra.Game;

namespace Vocaterra.MonoGame.Frontend;

/// <summary>
/// The hook through which an NPC's line is played again (S7 leaves it, S9 fills it): <c>Repetir</c> and <c>Más despacio</c> call it with the line and
/// whether it should be slow. Until S9 pre-renders the NPCs' voices nothing plays; the panel only highlights the line.
/// </summary>
public interface ILinePlayer
{
    /// <summary>Plays the NPC's line again.</summary>
    /// <param name="npc">The NPC's agent id.</param>
    /// <param name="line">The Spanish text of the line.</param>
    /// <param name="slow">Whether to play it slowly (the <c>.slow</c> file of S9).</param>
    void Play(string npc, string line, bool slow);
}

/// <summary>The line player of S7: it plays nothing (the panel highlights the line instead).</summary>
public sealed class SilentLinePlayer : ILinePlayer
{
    /// <summary>The one instance.</summary>
    public static SilentLinePlayer Instance { get; } = new();

    /// <inheritdoc />
    public void Play(string npc, string line, bool slow)
    {
    }
}

/// <summary>
/// Carries out the presses of the dialogue panel's speech buttons and choices (S7), as plain code the game calls for a key or a mouse click and the
/// tests call the same way. Every press raises its voice event through the session; a press that sends a line submits it to the worker marked as a
/// button or a choice (no word credit), and shows what the player picked.
/// </summary>
/// <param name="session">The running game.</param>
/// <param name="dialogue">The panel.</param>
/// <param name="player">The hook that plays an NPC's line again (S9).</param>
/// <param name="cursor">The cell under the crosshair, for the turns the buttons send.</param>
public sealed class SpeechActionRunner(GuideSession session, DialogueModel dialogue, ILinePlayer player, Func<GuideCell?> cursor)
{
    /// <summary>How long a re-shown line stays highlighted, in ticks of the simulation (two seconds).</summary>
    public const long HighlightTicks = 120;

    /// <summary>The hook that plays an NPC's line (S9); the support cards' sounds go through it too (S8).</summary>
    public ILinePlayer Player => player;

    /// <summary>Presses a button or a choice. Does nothing (and returns <see langword="false"/>) when the panel is closed or a choice is not on offer.</summary>
    /// <param name="action">What was pressed.</param>
    /// <returns>Whether the press did something.</returns>
    public bool Perform(SpeechAction action)
    {
        if (!dialogue.IsOpen)
        {
            return false;
        }

        switch (action)
        {
            case SpeechAction.Repeat:
            case SpeechAction.Slower:
                return Replay(action == SpeechAction.Slower);
            case SpeechAction.Help:
                _ = session.SubmitHelp(cursor());
                dialogue.BeginLine(SpeechButtons.HelpLabel);
                return true;
            case SpeechAction.DontKnow:
                _ = session.SubmitDontKnow(cursor());
                dialogue.BeginLine(SpeechButtons.DontKnowLabel);
                return true;
            case SpeechAction.Type:
                // The player chose to type: a turn still thinking is dropped (they will say it another way), then the text box opens.
                if (dialogue.IsThinking)
                {
                    _ = CancelThinking();
                }

                session.LogVoice(VoiceEventKind.TypeFallback);
                dialogue.BeginTyping();
                return true;
            default:
                return Choose(SpeechButtons.ChoiceIndex(action));
        }
    }

    /// <summary>
    /// Esc while "Pensando…": cancels the turns on the worker. They are rolled back there and their outcomes dropped by the session, and the panel
    /// stops thinking at once. Returns whether there was a turn to cancel.
    /// </summary>
    public bool CancelThinking()
    {
        if (!dialogue.CancelPending())
        {
            return false;
        }

        _ = session.CancelPending();
        return true;
    }

    private bool Replay(bool slow)
    {
        var line = dialogue.LastNpcLine;
        if (line is null)
        {
            return false;
        }

        session.LogVoice(slow ? VoiceEventKind.Slower : VoiceEventKind.Replay, line);
        _ = dialogue.HighlightLastNpcLine(session.CurrentTick + HighlightTicks);
        player.Play(session.CurrentNpc, line, slow);
        if (session.Support.ReplayCue is { } cue)
        {
            player.Play(cue.Npc, cue.Text, slow); // a phrase's help is on offer (S8, the audio-only level's replay hook): the model answer follows the question
        }

        return true;
    }

    private bool Choose(int? index)
    {
        if (index is not { } i || i >= dialogue.Choices.Count)
        {
            return false;
        }

        var option = dialogue.Choices[i];
        _ = session.SubmitChoice(option.Line, cursor());
        dialogue.BeginLine(option.Label);
        return true;
    }
}
