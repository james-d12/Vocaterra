using System.Globalization;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Tip;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Session;

// Sleeping (V12c, Y01): when the player may sleep, what the objective says once the play session's work is done, the arrow to the nearest place to sleep (SleepPlaces),
// and what a sleep clears before SaveGame.Sleep puts the session's own save back into it as the next play session.
public sealed partial class VoxelSession
{
    private bool _restDue;

    /// <summary>The places the player can sleep at (Y01): the mountain fire today; a bed at an inn and a camp made in play later.</summary>
    public SleepPlaces SleepPlaces { get; }

    /// <summary>Whether the player has been given lodging, which meets a sleep place whose access is <c>lodging</c>. False until a later milestone sets it; the save keeps it (Y02).</summary>
    public bool LodgingGiven { get; set; }

    /// <summary>Gives the player lodging (Y05, <c>grant_lodging</c>): the inn's bed becomes usable for good. The first time raises <c>lodging_granted</c>; a second grant changes nothing.</summary>
    public void GrantLodging()
    {
        if (LodgingGiven)
        {
            return;
        }

        LodgingGiven = true;
        Conversation.FollowLodging(); // Y08: the innkeeper greets with the goodnight line from now on, whoever gave the room
        LogWorld(WorldEvents.LodgingGranted, null, null);
    }

    /// <summary>The place the player last slept at (Y02), or <see langword="null"/> for none: a death wakes them there when it is still in <see cref="SleepPlaces"/>. The save keeps it.</summary>
    public LastSleepSave? LastSleepPlace { get; internal set; }

    /// <summary>Records a sleep at the usable place the player stands at; a sleep at none leaves the last place as it was (<c>SaveGame.Sleep</c> calls it after the save is restored).</summary>
    internal void NoteSleptAt()
    {
        if (CurrentSleepPlace is { } place)
        {
            LastSleepPlace = place.Point is null ? new LastSleepSave(place.Id, place.Cell.X, place.Cell.Y, place.Cell.Z) : new LastSleepSave(place.Id);
        }
    }

    /// <summary>The sleep entry of a save (Y02), or <see langword="null"/> when there is no lodging, no last place and no place made in play.</summary>
    internal SleepSave? CaptureSleep()
    {
        var made = SleepPlaces.MadeInPlay;
        if (!LodgingGiven && LastSleepPlace is null && made.Count == 0)
        {
            return null;
        }

        return new SleepSave(
            LodgingGiven,
            LastSleepPlace,
            made.Count == 0 ? null : [.. made.Select(static p => new SleepPlaceSave(p.Id, p.Cell.X, p.Cell.Y, p.Cell.Z, p.Kind, p.RadiusBlocks, p.HeightBlocks, p.Access, p.Point))]);
    }

    /// <summary>Puts the sleep entry back (Y02); no entry is no lodging, no last place and no places made in play.</summary>
    /// <param name="save">The entry, or <see langword="null"/>.</param>
    internal void RestoreSleep(SleepSave? save)
    {
        LodgingGiven = save?.LodgingGiven ?? false;
        LastSleepPlace = save?.Last;
        SleepPlaces.RestoreMadeInPlay((save?.Places ?? []).Select(static p => new SleepPlace(p.Id, p.Point, new BlockPos(p.X, p.Y, p.Z), p.Kind, p.RadiusBlocks, p.HeightBlocks, p.Access)));
    }

    /// <summary>
    /// Whether the player can sleep in this run (V12c): the front-end turns it off for a playtest, which plays one session per run (its post-check follows
    /// the ending). Off, the objective never sends the player to sleep and <see cref="CanSleep"/> is false.
    /// </summary>
    public bool SleepOffered { get; set; } = true;

    /// <summary>
    /// The play session a test run was skipped to (<c>--skip-to-session</c>, <see cref="SessionSkip"/>), or <see langword="null"/> for a learner's own game: the sessions
    /// before it were counted as done, not played. The save keeps it, so no save made from such a run passes for a learner's.
    /// </summary>
    public int? SkippedTo { get; internal set; }

    /// <summary>The usable sleep place the player stands at (within its reach across and in height), or <see langword="null"/>.</summary>
    public SleepPlace? CurrentSleepPlace => SleepPlaces.UsableAt(Simulation.Player.FeetBlock);

    /// <summary>Whether the player stands at a bed that is not theirs yet (Y05: the inn's bed, access <c>lodging</c>, before lodging is given).</summary>
    public bool AtLockedBed => SleepPlaces.LockedBedAt(Simulation.Player.FeetBlock) is not null;

    /// <summary>Whether the player stands at a usable sleep place (<see cref="CurrentSleepPlace"/>).</summary>
    public bool AtSleepPlace => CurrentSleepPlace is not null;

    /// <summary>
    /// Whether the player wants to sleep now (L01, D-game-25, adaptation design 9.5): on day 1 once its ending is reached (the introduction, the camp and the fire,
    /// <see cref="SessionComplete"/>); from day 2 on whenever the sleep drive is below 75 (stage 1 or worse). No lesson, objective or order holds sleep back. With the needs off
    /// the drive stays full, so only the scripts' aids sleep (<see cref="CanSleep"/>).
    /// </summary>
    public bool SleepWanted => Day <= 1 ? SessionComplete : NeedsEnabled && Needs.StageOf(Drive.Sleep) >= 1;

    /// <summary>
    /// Whether the objective sends the player to sleep: day 1 only, once its ending is reached (the arrow goes to the nearest usable sleep place when this begins away from one). From day 2 the
    /// needs say when to sleep.
    /// </summary>
    public bool RestDue => SleepOffered && SleepPlaces.AnyUsable && Day <= 1 && SessionComplete;

    /// <summary>
    /// Whether the player may sleep now: sleep is offered, a sleep place is usable (<see cref="SleepPlaces.AnyUsable"/>) and the session is at rest (<see cref="GuideSession.CanSave"/>). A lesson never holds it back: a
    /// class paused or under way pauses (L01). Whether they want to is <see cref="SleepWanted"/>; the front-end adds that no panel is open.
    /// </summary>
    public bool CanSleep => SleepOffered && SleepPlaces.AnyUsable && CanSave;

    // What the objective says once day 1's work is done: sleep at the fire. Null otherwise (from day 2 the needs and the tracked objective speak, L01).
    private ObjectivePiece? RestLine() =>
        RestDue ? new ObjectivePiece(Locale.Ui("sleepHud", ("session", (Day + 1).ToString(CultureInfo.InvariantCulture))), ObjectivePieceKind.Support, Done: true) : null;

    // Once a tick: when the objective begins to send the player to sleep, the arrow goes to the fire, unless the player is there.
    private void NoteRestDue()
    {
        var due = RestDue;
        if (due && !_restDue && !AtSleepPlace)
        {
            PointAtSleepPlace();
        }

        _restDue = due;
    }

    /// <summary>Takes the objective's state as it is, so the next tick points nowhere for what was already due (a restored session that is not a new play session).</summary>
    internal void SettleRest() => _restDue = RestDue;

    // The arrow goes to the nearest usable sleep place (V12c, Y01).
    private void PointAtSleepPlace()
    {
        if (SleepPlaces.NearestUsable(Simulation.Player.FeetBlock) is not { } place)
        {
            return;
        }

        var label = place.Point is { } point ? Scene.LabelOf(point) : place.Id;
        var cell = place.Cell;
        Pointer = new PointerMarker(label, new GuideCell(cell.X, cell.Y, cell.Z), Point: place.Point);
        LogWorld(WorldEvents.BeaconSet, null, $"{label} at {cell.X},{cell.Y},{cell.Z}");
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
