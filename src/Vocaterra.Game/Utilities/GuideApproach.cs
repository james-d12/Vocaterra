using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;

namespace Vocaterra.Game.Utilities;

/// <summary>
/// The guide greets the player who walks up to her (S17, D-game-15): when the player comes within <see cref="Blocks"/> blocks of the guide and her lesson is waiting
/// (it starts by itself, and has not been completed), the panel opens on her and the lesson begins, the same call the E key makes. Once per approach: it is armed again
/// when the player has gone <see cref="LeaveBlocks"/> away. Why a proximity trigger and not only the E key: a player who knows no Spanish and may not have found the
/// controls yet should not have to hunt for the right key to meet the one person who teaches them. Nothing is gated: Esc closes the panel, and the lesson waits.
/// Plain functions over values, so the rule is tested without a window.
/// </summary>
public static class GuideApproach
{
    /// <summary>How close the player must come, in blocks (horizontally), for the guide to greet them.</summary>
    public const int Blocks = 5;

    /// <summary>How far the player must go before the greeting is armed again, in blocks.</summary>
    public const int LeaveBlocks = 10;

    /// <summary>Whether the player is within <see cref="Blocks"/> of the guide (and not on another storey of the cliff: within six blocks in height).</summary>
    /// <param name="session">The 3D game.</param>
    public static bool Reached(VoxelSession session) => Distance(session) is { } d && d.Horizontal2 <= Blocks * Blocks && d.Vertical <= 6;

    /// <summary>
    /// Whether the guide greets the player now: she has a waiting lesson that starts by itself, the player is close, the greeting is armed and nothing else has the
    /// player's attention (<paramref name="free"/>). Updates <paramref name="armed"/>: it is cleared when the greeting fires and set again when the player leaves.
    /// </summary>
    /// <param name="session">The 3D game.</param>
    /// <param name="armed">Whether the greeting is armed (true at the start).</param>
    /// <param name="free">Whether the player is free to be greeted: no panel, journal, inventory or check is open and no script is driving.</param>
    public static bool ShouldGreet(VoxelSession session, ref bool armed, bool free)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (Distance(session) is not { } d || d.Horizontal2 > LeaveBlocks * LeaveBlocks)
        {
            armed = true;
            return false;
        }

        if (!armed || !free || !Reached(session) || !(LessonWaiting(session) || TransferWaiting(session)))
        {
            return false;
        }

        armed = false;
        return true;
    }

    /// <summary>
    /// Whether the camp is done and the guide has not made her transfer offer yet (<c>¿Quieres agua?</c>, S11): the way back up the trail to the player's own camp
    /// passes through hers, and she greets the player there with it, so the transfer moment happens on the way and no one has to remember to look for her.
    /// </summary>
    /// <param name="session">The 3D game.</param>
    public static bool TransferWaiting(VoxelSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.Objective.Complete && !PlaytestEnd.TransferOffered(session.Events);
    }

    /// <summary>Whether the guide has a lesson that starts by itself and is not completed (not begun, or begun and left).</summary>
    /// <param name="session">The 3D game.</param>
    public static bool LessonWaiting(VoxelSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.Lessons.OfferFor(NpcService.GuideAgent) is { } offer && session.Lessons.Book.Find(offer.LessonId) is { AutoStart: true };
    }

    private static (long Horizontal2, int Vertical)? Distance(VoxelSession session)
    {
        var guide = session.Scene.Npc(NpcService.GuideAgent).Feet;
        var me = session.Simulation.Player.FeetBlock;
        long dx = guide.X - me.X;
        long dz = guide.Z - me.Z;
        return ((dx * dx) + (dz * dz), Math.Abs(guide.Y - me.Y));
    }
}
