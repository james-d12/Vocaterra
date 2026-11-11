namespace Vocaterra.Game.Core.Atmosphere;

/// <summary>
/// The checks of <c>game/day.json</c> that need more than its shape (E01): the cycle's length is positive, every hour is on the dial, the four phases begin in order
/// (dawn, day, dusk, night) and the start and wake hours are hours of the day. <see cref="Content.GameDataValidator"/> runs it with the other files' checks.
/// </summary>
internal static class DayValidator
{
    private const string File = DayData.RelativePath;

    /// <summary>Every problem of the cycle, in a fixed order; empty when the values hold together.</summary>
    /// <param name="day">The cycle.</param>
    public static IReadOnlyList<string> Validate(DayData day)
    {
        ArgumentNullException.ThrowIfNull(day);
        var problems = new List<string>();
        if (!double.IsFinite(day.CycleSeconds) || day.CycleSeconds <= 0)
        {
            problems.Add($"{File}: cycleSeconds must be positive (it is {day.CycleSeconds}).");
        }

        var onDial = true;
        onDial &= OnDial(problems, "dawnHour", day.DawnHour, includeEnd: true);
        onDial &= OnDial(problems, "dayHour", day.DayHour, includeEnd: true);
        onDial &= OnDial(problems, "duskHour", day.DuskHour, includeEnd: true);
        onDial &= OnDial(problems, "nightHour", day.NightHour, includeEnd: true);
        _ = OnDial(problems, "startHour", day.StartHour, includeEnd: false);
        _ = OnDial(problems, "wakeHour", day.WakeHour, includeEnd: false);
        if (onDial && !(day.DawnHour < day.DayHour && day.DayHour < day.DuskHour && day.DuskHour < day.NightHour))
        {
            problems.Add($"{File}: the phases must begin in order, dawnHour < dayHour < duskHour < nightHour ({day.DawnHour}, {day.DayHour}, {day.DuskHour}, {day.NightHour}).");
        }

        return problems;
    }

    // An hour is on the dial from 0 up to 24; the start and wake hours stop short of 24, which is 0 again.
    private static bool OnDial(List<string> problems, string name, double hour, bool includeEnd)
    {
        var ok = double.IsFinite(hour) && hour >= 0 && (includeEnd ? hour <= DayData.HoursPerDay : hour < DayData.HoursPerDay);
        if (!ok)
        {
            problems.Add($"{File}: {name} must be {(includeEnd ? "from 0 to 24" : "from 0 up to 24, not 24 itself")} (it is {hour}).");
        }

        return ok;
    }
}
