using System.Text.Json.Serialization;

namespace Vocaterra.Game.Core.Atmosphere;

/// <summary>
/// What the day clock keeps besides the tick (E01): the offset a sleep or an unfreeze set, and the hour a test or a screenshot holds it at. A new game has
/// <see cref="Default"/> (no offset, not held); the digest and the save add nothing for it.
/// </summary>
/// <param name="Offset">The ticks added to the position in the cycle, 0 up to one cycle.</param>
/// <param name="FrozenHour">The hour the clock is held at (0 up to 24), or <see langword="null"/> when it runs.</param>
public sealed record DayClockState(
    long Offset = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? FrozenHour = null)
{
    /// <summary>The state of a new game.</summary>
    public static DayClockState Default { get; } = new();

    /// <summary>Whether this is the state of a new game.</summary>
    public bool IsDefault => Offset == 0 && FrozenHour is null;
}
