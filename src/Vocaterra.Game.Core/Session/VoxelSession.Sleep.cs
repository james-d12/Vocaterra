using System.Globalization;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Tip;

namespace Vocaterra.Game.Core.Session;

// The sleep at the player's own fire (V12c): when the player may sleep, what the objective says once the play session's work is done, the arrow back to the fire,
// and what a sleep clears before SaveGame.Sleep puts the session's own save back into it as the next play session.
public sealed partial class VoxelSession
{
    /// <summary>How near the player stands to their own fire to sleep there: within this many blocks across of <c>firering.player</c>, and six in height (as the guide's greeting).</summary>
    public const int FireBlocks = 5;

    private bool _restDue;

    /// <summary>
    /// Whether the player can sleep at their own fire in this run (V12c): the front-end turns it off for a playtest, which plays one session per run (its post-check follows
    /// the ending). Off, the objective never sends the player to sleep and <see cref="CanSleep"/> is false.
    /// </summary>
    public bool SleepOffered { get; set; } = true;

    /// <summary>
    /// The play session a test run was skipped to (<c>--skip-to-session</c>, <see cref="SessionSkip"/>), or <see langword="null"/> for a learner's own game: the sessions
    /// before it were counted as done, not played. The save keeps it, so no save made from such a run passes for a learner's.
    /// </summary>
    public int? SkippedTo { get; internal set; }

    /// <summary>Whether the player stands at their own fire ring (<c>firering.player</c>): within <see cref="FireBlocks"/> across and six blocks in height.</summary>
    public bool AtOwnFire
    {
        get
        {
            if (!Scene.Points.TryGetValue(PlayerFireRing, out var ring))
            {
                return false;
            }

            var feet = Simulation.Player.FeetBlock;
            long dx = ring.X - feet.X;
            long dz = ring.Z - feet.Z;
            return (dx * dx) + (dz * dz) <= FireBlocks * FireBlocks && Math.Abs(ring.Y - feet.Y) <= 6;
        }
    }

    /// <summary>
    /// Whether this play session's work is done (V12c): in session 1 its ending (the camp done and the player's own fire lit, <see cref="SessionComplete"/>); from session 2
    /// on, no lesson is under way and every unlocked lesson is done (the session's new lesson completed).
    /// </summary>
    public bool SessionWorkDone =>
        PlaySession <= 1 ? SessionComplete : Lessons.Active is null && !Lessons.Book.Lessons.Any(l => Lessons.IsUnlocked(l) && !Lessons.IsDone(l.Id));

    /// <summary>The play session of the first lesson a later session opens that is not done, or <see langword="null"/> when no later session has one (after session 6 today).</summary>
    public int? NextLessonSession =>
        Lessons.Book.Lessons.Where(l => l.Session > PlaySession && !Lessons.IsDone(l.Id)).Select(static l => (int?)l.Session).Min();

    /// <summary>
    /// Whether the objective sends the player to their fire to sleep (V12c): sleep is offered, the fire is lit, the session's work is done and a later session has a lesson.
    /// The arrow goes to the fire when this begins away from it.
    /// </summary>
    public bool RestDue => SleepOffered && FireLit && SessionWorkDone && NextLessonSession is not null;

    /// <summary>
    /// Whether the player may sleep now (V12c): sleep is offered, their own fire is lit, the session is at rest (<see cref="GuideSession.CanSave"/>) and no lesson step waits
    /// (an engaged lesson comes first: every lesson starts by itself, so one begun in this run stays engaged until it is done). The front-end adds that no panel is open.
    /// </summary>
    public bool CanSleep => SleepOffered && FireLit && CanSave && !Lessons.Engaged;

    // What the objective says once the play session's work is done (V12c): sleep to start the next session, or (no later session has a lesson) every lesson is done;
    // from session 2 on, while it is not done, the lesson to go on with. Null when none of these applies.
    private ObjectivePiece? RestLine()
    {
        if (SessionWorkDone)
        {
            if (NextLessonSession is null)
            {
                return new ObjectivePiece(Locale.Ui("allLessonsDoneHud"), ObjectivePieceKind.Support, Done: true);
            }

            return SleepOffered && FireLit
                ? new ObjectivePiece(Locale.Ui("sleepHud", ("session", (PlaySession + 1).ToString(CultureInfo.InvariantCulture))), ObjectivePieceKind.Support, Done: true)
                : null;
        }

        if (PlaySession > 1 && Lessons.NextUp is { } next)
        {
            var npc = Lessons.CurrentNpc ?? next.Npc; // a lesson begun goes on at its step's NPC (a do step's own line comes first while the lesson is engaged)
            return new ObjectivePiece(Locale.Ui("lessonNextHud", ("npc", Locale.NpcName(npc).ToLowerInvariant()), ("title", next.ShortTitle)), ObjectivePieceKind.Support);
        }

        return null;
    }

    // Once a tick: when the objective begins to send the player to sleep (a later session's lesson completed), the arrow goes to the fire, unless the player is there.
    private void NoteRestDue()
    {
        var due = RestDue;
        if (due && !_restDue && !AtOwnFire)
        {
            PointAtFire();
        }

        _restDue = due;
    }

    /// <summary>Takes the objective's state as it is, so the next tick points nowhere for what was already due (a restored session that is not a new play session).</summary>
    internal void SettleRest() => _restDue = RestDue;

    // The arrow goes to the player's own fire, where they sleep (V12c).
    private void PointAtFire()
    {
        if (!Scene.Points.TryGetValue(PlayerFireRing, out var ring))
        {
            return;
        }

        var label = Scene.LabelOf(PlayerFireRing);
        Pointer = new PointerMarker(label, new GuideCell(ring.X, ring.Y, ring.Z), Point: PlayerFireRing);
        LogWorld(WorldEvents.BeaconSet, null, $"{label} at {ring.X},{ring.Y},{ring.Z}");
    }

    /// <inheritdoc />
    internal override void ClearUnsaved()
    {
        base.ClearUnsaved();
        _newCards.Clear();
        _hints.Clear();
        _pendingSteps.Clear();
        _completed = [];
        _eventAmount = 1;
        Migration = null; // the save the sleep restores is this very bundle's
    }
}
