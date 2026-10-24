namespace Vocaterra.MonoGame.Utilities;

/// <summary>
/// The one place of the front-end that reads the wall clock: the time stamps of the session log and the name of its folder (S11, D-game-10).
/// The simulation, the game layer and the evaluation path read no clock; a scripted run's evaluation is the same on every machine whatever this
/// returns. Everything else takes its clock as a parameter.
/// </summary>
public static class WallClock
{
    /// <summary>The current time, with its offset, for the time stamp of a log line.</summary>
    public static DateTimeOffset Now => DateTimeOffset.Now;

    /// <summary>The current local date and time, for the name of a session folder.</summary>
    public static DateTime LocalNow => DateTime.Now;
}
