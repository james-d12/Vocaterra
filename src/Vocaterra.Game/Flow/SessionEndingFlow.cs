using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Sound;
using Vocaterra.Game.Utilities;

namespace Vocaterra.Game.Flow;

/// <summary>
/// The session's ending (S17, X04): when it comes, the guide says her closing line, the card says "Session complete" in English and Spanish, and
/// the session's summary goes to the log and the console. A game loaded after its ending does not play it again.
/// </summary>
public sealed class SessionEndingFlow(SessionEnding ending, VoxelSession session, NpcVoice voice, SessionLog log, Action<string> console)
{
    /// <summary>Once a frame: plays the ending when it comes.</summary>
    public void Update()
    {
        if (!ending.Update(session, session.Simulation.Tick))
        {
            return;
        }

        voice.Say(SpeechSpeakers.Guide, [ending.Spanish]);
        log.SessionSummary(ending.Summary, session.Simulation.Tick); // the session's summary card, in the log (X04, R01)
        console($"session complete: {ending.Spanish} ({ending.English}); {session.Objective.Summary}");
        foreach (var line in ending.Summary)
        {
            console($"  {line}");
        }
    }
}
