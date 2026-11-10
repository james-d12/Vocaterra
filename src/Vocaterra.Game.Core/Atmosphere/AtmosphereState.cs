using System.Text.Json.Serialization;

namespace Vocaterra.Game.Core.Atmosphere;

/// <summary>
/// What the simulation keeps of the day and the weather (E01, E04), and what <c>systems.atmosphere</c> of a save holds: the clock's offset and held hour, then the weather's
/// state. A new game has <see cref="Default"/> (nothing moved, the weather in its first spell), the digest adds nothing for it and a save does not write it.
/// </summary>
/// <param name="Offset">The ticks added to the position in the cycle, 0 up to one cycle.</param>
/// <param name="FrozenHour">The hour the clock is held at (0 up to 24), or <see langword="null"/> when it runs.</param>
/// <param name="Weather">The weather's state, or <see langword="null"/> while it is still in its first spell.</param>
public sealed record AtmosphereState(
    long Offset = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? FrozenHour = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] WeatherState? Weather = null)
{
    /// <summary>The state of a new game.</summary>
    public static AtmosphereState Default { get; } = new();

    /// <summary>Whether this is the state of a new game.</summary>
    [JsonIgnore]
    public bool IsDefault => Offset == 0 && FrozenHour is null && Weather is null;

    /// <summary>The clock's part.</summary>
    [JsonIgnore]
    public DayClockState Clock => new(Offset, FrozenHour);
}
