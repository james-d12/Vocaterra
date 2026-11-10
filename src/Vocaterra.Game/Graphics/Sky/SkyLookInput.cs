using Vocaterra.Game.Core.Atmosphere;

namespace Vocaterra.Game.Graphics.Sky;

/// <summary>
/// What <see cref="SkyLook.At"/> needs to know about the moment (E02): the hour on the 24-hour dial, the daylight from 0 (night) to 1 (full day) and the phase. It is a record so it can
/// grow: the weather milestones add the clouds' cover, the sunlight and the fog to it, each with a default that leaves the look as it is without them.
/// </summary>
/// <param name="Hour">The hour on the 24-hour dial, 0 up to 24.</param>
/// <param name="Daylight">The daylight, 0 up to 1 (<see cref="DayClock.DaylightAt"/>).</param>
/// <param name="Phase">The phase of the day.</param>
public readonly record struct SkyLookInput(double Hour, double Daylight, DayPhase Phase)
{
    /// <summary>The input for a tick, read from the clock: its hour, its daylight and its phase.</summary>
    /// <param name="clock">The day clock of the simulation.</param>
    /// <param name="tick">The simulation's tick.</param>
    public static SkyLookInput At(DayClock clock, long tick)
    {
        ArgumentNullException.ThrowIfNull(clock);
        return new SkyLookInput(clock.HourAt(tick), clock.DaylightAt(tick), clock.PhaseAt(tick));
    }
}
