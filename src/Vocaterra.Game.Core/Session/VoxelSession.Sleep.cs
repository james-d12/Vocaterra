using System.Globalization;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Needs;
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
    /// Whether the player wants to sleep now (L01, D-game-25, adaptation design 9.5): on day 1 once its ending is reached (the introduction, the camp and the fire,
    /// <see cref="SessionComplete"/>); from day 2 on whenever the sleep drive is below 75 (stage 1 or worse). No lesson, objective or order holds sleep back. With the needs off
    /// the drive stays full, so only the scripts' aids sleep (<see cref="CanSleep"/>).
    /// </summary>
    public bool SleepWanted => Day <= 1 ? SessionComplete : NeedsEnabled && Needs.StageOf(Drive.Sleep) >= 1;

    /// <summary>
    /// Whether the objective sends the player to their fire to sleep: day 1 only, once its ending is reached (the arrow goes to the fire when this begins away from it). From day 2 the
    /// needs say when to sleep.
    /// </summary>
    public bool RestDue => SleepOffered && FireLit && Day <= 1 && SessionComplete;

    /// <summary>
    /// Whether the player may sleep now: sleep is offered, their own fire is lit and the session is at rest (<see cref="GuideSession.CanSave"/>). A lesson never holds it back: a
    /// class paused or under way pauses (L01). Whether they want to is <see cref="SleepWanted"/>; the front-end adds that no panel is open.
    /// </summary>
    public bool CanSleep => SleepOffered && FireLit && CanSave;

    // What the objective says once day 1's work is done: sleep at the fire. Null otherwise (from day 2 the needs and the tracked objective speak, L01).
    private ObjectivePiece? RestLine() =>
        RestDue ? new ObjectivePiece(Locale.Ui("sleepHud", ("session", (Day + 1).ToString(CultureInfo.InvariantCulture))), ObjectivePieceKind.Support, Done: true) : null;

    // Once a tick: when the objective begins to send the player to sleep, the arrow goes to the fire, unless the player is there.
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
