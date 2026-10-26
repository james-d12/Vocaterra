using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Sound;
using Vocaterra.Game.UI;

namespace Vocaterra.Game.Utilities;

/// <summary>
/// The front-end's part of the phrase support (S8), as plain code the game calls once per frame and the tests call the same way: it plays the sounds
/// the session queued (the model audio when a card appears or when a prompt appears at audio-only, and the stall's key words) through the line player,
/// hands the shopkeeper's opening that followed the key-word cards to the panel, and words the console lines of the phrase progress.
/// </summary>
/// <param name="session">The running game.</param>
/// <param name="dialogue">The panel.</param>
/// <param name="actions">The speech actions, whose line player the sounds go through (S9 makes it audible).</param>
/// <param name="log">Where the console lines go.</param>
public sealed class SupportFrontend(GuideSession session, DialogueModel dialogue, SpeechActionRunner actions, Action<string> log)
{
    /// <summary>
    /// One frame: plays the sounds waiting (the hook: <see cref="ILinePlayer.Play"/> with the NPC, the text of the line and not slow), and, when the
    /// key-word cards have stood alone long enough, puts the shopkeeper's opening on the panel. Returns the opening that was added, or
    /// <see langword="null"/>.
    /// </summary>
    /// <param name="onOpening">Called with that opening before the sounds are queued (the game says its lines, which replaces what plays), or <see langword="null"/>.</param>
    public NpcOpening? Pump(Action<NpcOpening>? onOpening = null)
    {
        var opening = session.TakeOpening();
        if (opening is not null)
        {
            onOpening?.Invoke(opening);
        }

        string? previous = null;
        foreach (var cue in session.Support.TakeCues())
        {
            if (!string.Equals(cue.Text, previous, StringComparison.Ordinal))
            {
                actions.Player.Append(cue.Npc, cue.Text, false); // after the NPC's own line; two cards with one model say it once
            }

            previous = cue.Text;
        }

        if (opening is null)
        {
            return null;
        }

        if (dialogue.IsOpen)
        {
            dialogue.AddOpening(opening);
        }

        log($"talk: {opening.Npc} ({(opening.HostTurn ? "host turn" : "prompt")}, {opening.Prompt}): {string.Join(" / ", opening.Lines)}");
        return opening;
    }

    /// <summary>
    /// The console lines that follow a game event: after a phrase use, one <c>phrase: pedir-agua, supported 1, unaided 0, ...</c> line for the phrase and
    /// for each phrase it was a "no sé" for; after the key words, a line that says so. Empty for the other events.
    /// </summary>
    /// <param name="gameEvent">An event of <see cref="GuideSession.Events"/>.</param>
    public IReadOnlyList<string> LinesFor(GameEvent gameEvent)
    {
        ArgumentNullException.ThrowIfNull(gameEvent);
        if (gameEvent is not PhraseUse use)
        {
            return [];
        }

        var lines = new List<string> { session.PhraseProgress.ConsoleLine(use.PhraseId) };
        if (use.InPlay is { } inPlay)
        {
            lines.AddRange(inPlay.Where(id => !string.Equals(id, use.PhraseId, StringComparison.Ordinal)).Select(session.PhraseProgress.ConsoleLine));
        }

        return lines;
    }

    /// <summary>The summary at exit: the heading and one line per phrase seen (see <see cref="PhraseProgress.SummaryLines"/>), then the key words and the session.</summary>
    public IReadOnlyList<string> Summary()
    {
        var lines = new List<string> { $"phrases: summary at exit (session {session.PhraseProgress.Session}, stored time {session.StoredTime.UnixSeconds})" };
        lines.AddRange(session.PhraseProgress.SummaryLines());
        if (lines.Count == 1)
        {
            lines.Add("  no phrase was used or shown");
        }

        return lines;
    }
}
