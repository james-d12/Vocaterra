using System.Globalization;

namespace Vocaterra.Game.Core.Session;

/// <summary>
/// Where the game offers the sleep at the player's own fire (V12c): the prompt at the lit fire, in the HUD's hint style (<c>E: sleep until morning (session 3)</c>), and the line
/// of the session's closing card (<c>Enter: sleep until morning and start session 2</c>). A sleep is a load in the same run (<see cref="Save.SaveGame.Sleep"/>). Plain functions over
/// the session and what the front-end knows, so the rules are tested without a window.
/// </summary>
public static class SleepOffer
{
    /// <summary>
    /// Whether the fire's prompt shows, and E sleeps: the player wants to sleep (<see cref="VoxelSession.SleepWanted"/>: day 1's ending, or from day 2 the sleep drive below 75), may sleep
    /// (<see cref="VoxelSession.CanSleep"/>: a lit fire, the session at rest; no lesson holds it, L01), stands at their own fire, and nothing else has their attention (<paramref name="free"/>).
    /// </summary>
    /// <param name="session">The 3D game.</param>
    /// <param name="free">Whether the player is free: no panel, screen, check or report is open, no voice is recorded, and the crosshair is on no NPC and no workbench (their keys come first).</param>
    public static bool AtFire(VoxelSession session, bool free)
    {
        ArgumentNullException.ThrowIfNull(session);
        return free && session.SleepWanted && session.CanSleep && session.AtOwnFire;
    }

    /// <summary>
    /// Whether E at the fire says «No tengo sueño.» (L01, design 9.5): the player is at their own lit fire, free, and the sleep drive is not below 75 (on day 1, before the ending).
    /// The game then shows <see cref="NotSleepyText"/> and sleeps nothing.
    /// </summary>
    /// <param name="session">The 3D game.</param>
    /// <param name="free">Whether the player is free (see <see cref="AtFire"/>).</param>
    public static bool AtFireNotTired(VoxelSession session, bool free)
    {
        ArgumentNullException.ThrowIfNull(session);
        return free && !session.SleepWanted && session.CanSleep && session.AtOwnFire;
    }

    /// <summary>The player's line at a fire they are not tired at: the Spanish with the English beside it, text only (the player's words are not voiced, D-game-13).</summary>
    /// <param name="session">The 3D game.</param>
    public static string NotSleepyText(VoxelSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        const string id = "guide.sleep.not_sleepy.1";
        var spanish = session.Data.Speech.First(l => string.Equals(l.LineId, id, StringComparison.Ordinal)).Text;
        return $"“{spanish}” ({session.Locale.Text("line." + id)})";
    }

    /// <summary>Whether the session's closing card offers the sleep, and Enter sleeps: the card is up, the player is free and may sleep.</summary>
    /// <param name="session">The 3D game.</param>
    /// <param name="cardUp">Whether the closing card is on screen.</param>
    /// <param name="free">Whether the player is free (see <see cref="AtFire"/>; the crosshair does not matter).</param>
    public static bool OnCard(VoxelSession session, bool cardUp, bool free)
    {
        ArgumentNullException.ThrowIfNull(session);
        return cardUp && free && session.CanSleep && session.SleepWanted;
    }

    /// <summary>The fire's prompt: <c>E: sleep until morning (session 3)</c> (<c>ui.sleepPrompt</c>; <c>ui.sleepPromptDone</c> when no later session has a lesson).</summary>
    /// <param name="session">The 3D game.</param>
    public static string PromptText(VoxelSession session) => Text(session, "sleepPrompt", 1);

    /// <summary>The closing card's line: <c>Enter: sleep until morning and start session 2</c> (<c>ui.sleepCard</c>; <c>ui.sleepCardDone</c> when no later session has a lesson).</summary>
    /// <param name="session">The 3D game.</param>
    public static string CardText(VoxelSession session) => Text(session, "sleepCard", 1);

    /// <summary>The toast after a sleep: <c>Good morning: session 2 begins</c> (<c>ui.sleptToast</c>).</summary>
    /// <param name="session">The 3D game, in its new session.</param>
    public static string MorningText(VoxelSession session) => Text(session, "sleptToast", 0);

    private static string Text(VoxelSession session, string key, int ahead)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.Locale.Ui(key, ("session", (session.Day + ahead).ToString(CultureInfo.InvariantCulture)));
    }
}
