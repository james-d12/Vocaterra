namespace Vocaterra.Game.Core.Atmosphere;

/// <summary>The part of the day the clock is in (E01). The numbers are saved nowhere; the world event's detail is the lowercase name (<see cref="DayPhaseNames.Of"/>).</summary>
public enum DayPhase
{
    /// <summary>The sky brightens: the daylight rises from 0 to 1.</summary>
    Dawn = 0,

    /// <summary>Full daylight.</summary>
    Day = 1,

    /// <summary>The sky darkens: the daylight falls from 1 to 0.</summary>
    Dusk = 2,

    /// <summary>No daylight.</summary>
    Night = 3,
}
