using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Sound;

namespace Vocaterra.Game.Core.Dialogue;

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
                if (session.Lessons.HandlesHelp)
                {
                    // A lesson step has its own help path (S16): the English hint, the model line again and, for a phrase, the card and the English.
                    session.LogVoice(VoiceEventKind.Hint);
                    return session.Lessons.Help();
                }

                _ = session.SubmitHelp(cursor());
                dialogue.BeginLine(SpeechButtons.HelpLabel);
                return true;
            case SpeechAction.DontKnow:
                if (session.Lessons.HandlesHelp)
                {
                    return session.Lessons.DontKnow(); // "no sé" in a lesson: the help path and a tracked "no sé", never a failure
                }

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
            player.Append(cue.Npc, cue.Text, slow); // a phrase's help is on offer (S8, the audio-only level's replay hook): the model answer follows the question
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
