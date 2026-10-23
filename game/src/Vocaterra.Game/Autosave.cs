using Vocaterra.Simulation.Voxel;

namespace Vocaterra.Game;

/// <summary>The file names of the saves the game writes beside the player's own (X05, R04).</summary>
public static class AutosaveFiles
{
    /// <summary>The autosave of an ordinary run, in its session folder (a playtest writes its <c>playtest.save</c> instead, the file F6 writes).</summary>
    public const string FileName = "autosave.save";

    /// <summary>The last-chance save a crash writes, in the session folder. It never replaces the autosave or a player's save.</summary>
    public const string CrashFileName = "crash.save";
}

/// <summary>What the autosave policy decided for one frame: whether to save now, and why (the reason is the console line's words).</summary>
/// <param name="Save">Whether the game saves now.</param>
/// <param name="Reason">The reason of the save, or <see langword="null"/> when there is no save.</param>
public sealed record AutosaveDecision(bool Save, string? Reason)
{
    /// <summary>No save this frame.</summary>
    public static AutosaveDecision None { get; } = new(false, null);
}

/// <summary>
/// When the game autosaves (X05, R04). It is a function of the game's own events and ticks, never of the wall clock, so the same run asks for the
/// same saves at the same ticks; and a save changes nothing in the simulation, because it only reads the session.
/// </summary>
/// <remarks>
/// A save is <em>due</em> when one of these happens, and it is taken at the first frame the game is at rest (<c>atRest</c>: no turn on the worker,
/// no key words standing, no dialogue open, no voice being recorded; the caller decides that): a lesson step ends
/// (<see cref="LessonEventKind.StepDone"/>), a lesson completes, a camp objective step is done, the session completes, a dialogue closes
/// (<see cref="WorldEvents.TalkClosed"/>), or <see cref="IntervalSeconds"/> of play have passed since the last save. A due save names the newest of
/// those events until it is taken (the throttle names only a save nothing else made due). The policy reads each event once: it counts the events it has seen.
/// </remarks>
public sealed class AutosavePolicy
{
    /// <summary>The longest play, in seconds of stored ticks, between two saves: five minutes. A crash loses at most that much. A save is a few
    /// kilobytes, so this is rare enough to cost nothing and often enough that a playtest's crash loses one short stretch, not a session.</summary>
    public const int IntervalSeconds = 5 * 60;

    /// <summary>The same interval in simulation ticks.</summary>
    public const long IntervalTicks = (long)IntervalSeconds * VoxelSimulation.TicksPerSecond;

    /// <summary>The reason of the throttle's save.</summary>
    public const string IntervalReason = "five minutes of play";

    private int _seen;
    private long _lastSavedTick;
    private string? _due;

    /// <summary>Starts a policy at a tick, having seen the events the session already holds (a loaded save's events are not new).</summary>
    /// <param name="startTick">The simulation tick at the start.</param>
    /// <param name="eventsSeen">How many events the session already holds.</param>
    public AutosavePolicy(long startTick, int eventsSeen)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(eventsSeen);
        _lastSavedTick = startTick;
        _seen = eventsSeen;
    }

    /// <summary>The reason of the save that is due and waiting for a safe frame, or <see langword="null"/>.</summary>
    public string? Due => _due;

    /// <summary>The tick of the last save (the start, until the first).</summary>
    public long LastSavedTick => _lastSavedTick;

    /// <summary>
    /// Decides one frame. Call it once per frame with the session's events (oldest first) and whether the game is at rest. A save returned is taken:
    /// the policy records it, so the caller must write it.
    /// </summary>
    /// <param name="tick">The simulation tick now.</param>
    /// <param name="events">The session's events, oldest first; the policy reads only the ones it has not seen.</param>
    /// <param name="atRest">Whether the game may be saved now.</param>
    public AutosaveDecision Decide(long tick, IReadOnlyList<GameEvent> events, bool atRest)
    {
        ArgumentNullException.ThrowIfNull(events);
        for (; _seen < events.Count; _seen++)
        {
            _due = ReasonOf(events[_seen]) ?? _due; // the newest event's reason: a lesson's steps read "dialogue closed" once the panel shuts
        }

        if (_due is null && tick - _lastSavedTick >= IntervalTicks)
        {
            _due = IntervalReason;
        }

        if (_due is null || !atRest)
        {
            return AutosaveDecision.None;
        }

        var reason = _due;
        _due = null;
        _lastSavedTick = tick;
        return new AutosaveDecision(true, reason);
    }

    /// <summary>The reason an event makes a save due, or <see langword="null"/> when it does not.</summary>
    /// <param name="gameEvent">The event.</param>
    public static string? ReasonOf(GameEvent gameEvent)
    {
        ArgumentNullException.ThrowIfNull(gameEvent);
        return gameEvent switch
        {
            LessonEvent { Kind: LessonEventKind.StepDone } lesson => $"lesson {lesson.Lesson} step {lesson.Step} done",
            LessonEvent { Kind: LessonEventKind.LessonCompleted } lesson => $"lesson {lesson.Lesson} completed",
            WorldEvent { Name: WorldEvents.ObjectiveStep } => "objective step done",
            WorldEvent { Name: WorldEvents.SessionComplete } => "session complete",
            WorldEvent { Name: WorldEvents.TalkClosed } => "dialogue closed",
            _ => null,
        };
    }
}
