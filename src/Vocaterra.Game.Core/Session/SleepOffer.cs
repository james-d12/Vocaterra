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
    /// Whether the fire's prompt shows, and E sleeps: the play session's work is done (<see cref="VoxelSession.SessionWorkDone"/>: the objective says to sleep, or that every
    /// lesson is done; a session whose lesson is left offers no sleep that would skip it), the player may sleep (<see cref="VoxelSession.CanSleep"/>: a lit fire, the session
    /// at rest, no lesson step waiting), stands at their own fire, and nothing else has their attention (<paramref name="free"/>).
    /// </summary>
    /// <param name="session">The 3D game.</param>
    /// <param name="free">Whether the player is free: no panel, screen, check or report is open, no voice is recorded, and the crosshair is on no NPC and no workbench (their keys come first).</param>
    public static bool AtFire(VoxelSession session, bool free)
    {
        ArgumentNullException.ThrowIfNull(session);
        return free && session.SessionWorkDone && session.CanSleep && session.AtOwnFire;
    }

    /// <summary>Whether the session's closing card offers the sleep, and Enter sleeps: the card is up, the player is free and may sleep.</summary>
    /// <param name="session">The 3D game.</param>
    /// <param name="cardUp">Whether the closing card is on screen.</param>
    /// <param name="free">Whether the player is free (see <see cref="AtFire"/>; the crosshair does not matter).</param>
    public static bool OnCard(VoxelSession session, bool cardUp, bool free)
    {
        ArgumentNullException.ThrowIfNull(session);
        return cardUp && free && session.CanSleep;
    }

    /// <summary>The fire's prompt: <c>E: sleep until morning (session 3)</c> (<c>ui.sleepPrompt</c>).</summary>
    /// <param name="session">The 3D game.</param>
    public static string PromptText(VoxelSession session) => Text(session, "sleepPrompt", 1);

    /// <summary>The closing card's line: <c>Enter: sleep until morning and start session 2</c> (<c>ui.sleepCard</c>).</summary>
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
