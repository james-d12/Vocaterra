namespace Vocaterra.Game.Core.Atmosphere;

/// <summary>The names of the day phases as a trigger writes them (the detail of the <c>day_phase</c> world event).</summary>
public static class DayPhaseNames
{
    /// <summary>The detail for <see cref="DayPhase.Dawn"/>.</summary>
    public const string Dawn = "dawn";

    /// <summary>The detail for <see cref="DayPhase.Day"/>.</summary>
    public const string Day = "day";

    /// <summary>The detail for <see cref="DayPhase.Dusk"/>.</summary>
    public const string Dusk = "dusk";

    /// <summary>The detail for <see cref="DayPhase.Night"/>.</summary>
    public const string Night = "night";

    /// <summary>The name of a phase.</summary>
    /// <param name="phase">The phase.</param>
    public static string Of(DayPhase phase) => phase switch
    {
        DayPhase.Dawn => Dawn,
        DayPhase.Day => Day,
        DayPhase.Dusk => Dusk,
        DayPhase.Night => Night,
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, "Not a day phase."),
    };
}
