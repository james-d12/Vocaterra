using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Sound;
using Vocaterra.Game.Diagnostics;

namespace Vocaterra.Game.Flow;

/// <summary>
/// The session's ending (S17, X04; Y07): when it comes (the camp done, a room at the inn, and the talk that gave it closed), the closing pool's NPC (the innkeeper) says the closing line, the card says "Session complete" in English and Spanish, and
/// the session's summary goes to the log and the console. A game loaded after its ending does not play it again.
/// </summary>
public sealed class SessionEndingFlow(SessionEnding ending, VoxelSession session, NpcVoice voice, SessionLog log, Action<string> console)
{
    /// <summary>Once a frame: plays the lines the world had an NPC say (Luna sending the player to the inn), then the ending when it comes.</summary>
    public void Update()
    {
        foreach (var line in session.TakeSpoken())
        {
            voice.Append(line.Speaker, line.Spanish, slow: false); // after what plays: the line must not cut a conversation's
            console($"{line.Speaker} says: {line.Spanish}{(line.English is null ? string.Empty : $" ({line.English})")}");
        }

        if (!ending.Update(session, session.Simulation.Tick))
        {
            return;
        }

        voice.Append(ending.Speaker, ending.Spanish, slow: false); // after the room line, which may still be playing (Y07)
        log.SessionSummary(ending.Summary, session.Simulation.Tick, session.NeedsRate); // the session's summary card, in the log (X04, R01)
        console($"session complete: {ending.Speaker} says: {ending.Spanish} ({ending.English}); {session.Objective.Summary}");
        foreach (var line in ending.Summary)
        {
            console($"  {line}");
        }

        if (session.NeedsRate != 1)
        {
            console($"  drive rate {Vocaterra.Game.Core.Menu.PlayerSettings.RateText(session.NeedsRate)}"); // N09: the playtest's numbers say the rate they ran at
        }
    }
}
