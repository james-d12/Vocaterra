namespace Vocaterra.Game.Core.Event;

/// <summary>
/// Something the game recorded for the learning measurement: a phrase use, a voice event or a world event. Plain values with no clock: the
/// <see cref="Tick"/> is the simulation's tick and <see cref="Time"/> the stored time (Unix seconds) when the line was submitted, so the same
/// run gives the same events. S8 reads the phrase uses into the phrase progress; S11 writes all of them into the session log.
/// </summary>
/// <param name="Tick">The simulation tick when the thing happened (a turn: when it was submitted).</param>
/// <param name="Time">The stored time in Unix seconds.</param>
public abstract record GameEvent(long Tick, long Time)
{
    /// <summary>A short wire name of the event's family: <c>phrase</c>, <c>voice</c> or <c>world</c>.</summary>
    public abstract string Family { get; }

    /// <summary>The event as one console line, for example <c>phrase pedir-agua supported at shopkeeper</c>.</summary>
    public abstract string Text { get; }
}
