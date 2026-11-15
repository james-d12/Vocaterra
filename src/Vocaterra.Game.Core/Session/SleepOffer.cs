using System.Globalization;

namespace Vocaterra.Game.Core.Session;

/// <summary>
/// Where the game offers the sleep (V12c, Y01): the prompt at a usable sleep place (today the lit fire), in the HUD's hint style (<c>E: sleep until morning (session 3)</c>), and the line
/// of the session's closing card (<c>Enter: sleep until morning and start session 2</c>). A sleep is a load in the same run (<see cref="Save.SaveGame.Sleep"/>). Plain functions over
/// the session and what the front-end knows, so the rules are tested without a window.
/// </summary>
public static class SleepOffer
{
    /// <summary>
    /// Whether the fire's prompt shows, and E sleeps: the player wants to sleep (<see cref="VoxelSession.SleepWanted"/>: day 1's ending, or from day 2 the sleep drive below 75), may sleep
    /// (<see cref="VoxelSession.CanSleep"/>: a usable sleep place, the session at rest; no lesson holds it, L01), stands at a usable sleep place, and nothing else has their attention (<paramref name="free"/>).
    /// </summary>
    /// <param name="session">The 3D game.</param>
    /// <param name="free">Whether the player is free: no panel, screen, check or report is open, no voice is recorded, and the crosshair is on no NPC and no workbench (their keys come first).</param>
    public static bool AtFire(VoxelSession session, bool free)
    {
        ArgumentNullException.ThrowIfNull(session);
        return free && session.SleepWanted && session.CanSleep && session.AtSleepPlace;
    }

    /// <summary>
    /// Whether E at the fire says «No tengo sueño.» (L01, design 9.5): the player is at a usable sleep place, free, and the sleep drive is not below 75 (on day 1, before the ending).
    /// The game then shows <see cref="NotSleepyText"/> and sleeps nothing.
    /// </summary>
    /// <param name="session">The 3D game.</param>
    /// <param name="free">Whether the player is free (see <see cref="AtFire"/>).</param>
    public static bool AtFireNotTired(VoxelSession session, bool free)
    {
        ArgumentNullException.ThrowIfNull(session);
        return free && !session.SleepWanted && session.CanSleep && session.AtSleepPlace;
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

    /// <summary>Whether the session's closing card offers the sleep, and Enter sleeps: the card is up, the player is free, may sleep and stands at a usable sleep place (Y01: away from one the card offers none; the objective's line and the arrow say where to go).</summary>
    /// <param name="session">The 3D game.</param>
    /// <param name="cardUp">Whether the closing card is on screen.</param>
    /// <param name="free">Whether the player is free (see <see cref="AtFire"/>; the crosshair does not matter).</param>
    public static bool OnCard(VoxelSession session, bool cardUp, bool free)
    {
        ArgumentNullException.ThrowIfNull(session);
        return cardUp && free && session.CanSleep && session.SleepWanted && session.AtSleepPlace;
    }

    /// <summary>
    /// Where a night ended, for the console's line (Y08, the locale's text): <c>at the inn</c> for a bed, <c>at the fire</c> for a fire, <c>at your camp</c> for the camp's bedroll or ring (Y12), <c>where the player fell</c> for a collapse (which sleeps at no place) and <c>wherever the player stood</c> for the scripts' aid away from any place.
    /// </summary>
    /// <param name="session">The 3D game.</param>
    /// <param name="place">The sleep place slept at, or <see langword="null"/> for none.</param>
    /// <param name="collapse">Whether the night was a collapse.</param>
    public static string EndedWhere(VoxelSession session, SleepPlace? place, bool collapse)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.Locale.Ui(collapse ? "endedWhereFell" : place is null ? "endedAnywhere" : VoxelSession.IsCampPlace(place.Id) ? "endedAtCamp" : place.Kind == Needs.SleepPlaceSpec.KindBed ? "endedAtInn" : "endedAtFire");
    }

    /// <summary>The fire's prompt: <c>E: sleep until morning (session 3)</c> (<c>ui.sleepPrompt</c>; <c>ui.sleepPromptDone</c> when no later session has a lesson).</summary>
    /// <param name="session">The 3D game.</param>
    public static string PromptText(VoxelSession session) => Text(session, "sleepPrompt", 1);

    /// <summary>
    /// Whether the player is free at a bed that is not theirs yet (Y05: the inn's bed before lodging): the game shows <see cref="LockedBedText"/> and E does nothing. A bed whose access is met
    /// offers the sleep as the fire does; away from every sleep place nothing shows.
    /// </summary>
    /// <param name="session">The 3D game.</param>
    /// <param name="free">Whether the player is free (see <see cref="AtFire"/>).</param>
    public static bool AtLockedBed(VoxelSession session, bool free)
    {
        ArgumentNullException.ThrowIfNull(session);
        return free && session.SleepOffered && session.AtLockedBed;
    }

    /// <summary>The line at a bed the player has no room at yet: the bed is the inn's and a room must be asked for (<c>ui.sleepBedLocked</c>).</summary>
    /// <param name="session">The 3D game.</param>
    public static string LockedBedText(VoxelSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.Locale.Ui("sleepBedLocked");
    }

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
