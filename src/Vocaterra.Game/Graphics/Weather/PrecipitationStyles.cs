namespace Vocaterra.Game.Graphics.Weather;

/// <summary>
/// The style table (E06): one row per kind of precipitation the weather data can name, keyed by the string of the snapshot's <c>PrecipitationKind</c>. It is the one place in the code a
/// precipitation's own id is written (the architecture scan <c>NoWeatherKindIdsInSourceTests</c> allows this file and no other); a test holds that every kind of the bundle's
/// <c>game/weather.json</c> has a row, so a kind added to the data without a style fails there. An id the table lacks draws nothing (<see cref="TryFor"/>).
/// </summary>
public static class PrecipitationStyles
{
    /// <summary>The rows: rain falls fast in long thin streaks, a little blue-grey, slanted by the wind.</summary>
    public static IReadOnlyList<PrecipitationStyle> Table { get; } =
    [
        new PrecipitationStyle(
            Id: "rain",
            Length: 1.4f,
            Width: 0.045f,
            FallSpeed: 15f,
            Colour: (196, 210, 230),
            Opacity: 0.8f,
            WindSlant: 0.6f,
            CountShare: 1f),
    ];

    /// <summary>The style of a precipitation kind.</summary>
    /// <param name="kind">The kind's id, as the channel carries it (empty for none).</param>
    /// <param name="style">The row, or <see langword="null"/>.</param>
    /// <returns>Whether the table has a row for the kind.</returns>
    public static bool TryFor(string kind, out PrecipitationStyle style)
    {
        foreach (var row in Table)
        {
            if (string.Equals(row.Id, kind, StringComparison.Ordinal))
            {
                style = row;
                return true;
            }
        }

        style = null!;
        return false;
    }
}
