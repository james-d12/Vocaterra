using System.Text.Json;
using Vocaterra.Game.Core.Content;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Core.Atmosphere;

/// <summary>
/// The day and night cycle's numbers (<c>game/day.json</c>, E01, D-game-31): how long one day takes, the hours of a 24-hour dial at which each phase begins, the hour a new game
/// starts at and the hour a sleep wakes at. The hours may be fractions (6.5 is half past six). <see cref="DayClock"/> turns them into ticks; <see cref="DayValidator"/> checks the values.
/// A bundle with no <c>game/day.json</c> does not load, as one with no <c>game/needs.json</c> does not; a simulation built without game data uses <see cref="Default"/>.
/// </summary>
/// <param name="CycleSeconds">The real-time length of one full day and night at 60 ticks a second.</param>
/// <param name="DawnHour">The hour dawn begins.</param>
/// <param name="DayHour">The hour full day begins.</param>
/// <param name="DuskHour">The hour dusk begins.</param>
/// <param name="NightHour">The hour night begins.</param>
/// <param name="StartHour">The hour at tick 0 of a new game.</param>
/// <param name="WakeHour">The hour a sleep (or a collapse) wakes at.</param>
public sealed record DayData(double CycleSeconds, double DawnHour, double DayHour, double DuskHour, double NightHour, double StartHour, double WakeHour)
{
    /// <summary>The file's path inside the bundle folder.</summary>
    public const string RelativePath = "game/day.json";

    /// <summary>The file's format.</summary>
    public const int SchemaVersion = 1;

    /// <summary>The hours on the dial.</summary>
    public const double HoursPerDay = 24;

    /// <summary>The values of the first version (a 40-minute day, about 30 of them light), used where a simulation is built with no game data.</summary>
    public static DayData Default { get; } = new(2400, 4, 6, 20, 22, 8, 7);

    /// <summary>Reads the cycle from the bundle's verified host file.</summary>
    /// <param name="bundle">The bundle, loaded with hash verification.</param>
    /// <exception cref="InvalidDataException">The bundle has no such host file, or the file is malformed.</exception>
    public static DayData Load(ContentBundle bundle) => Parse(GameData.HostText(bundle, RelativePath));

    /// <summary>Builds the cycle from the file's text. The shape is checked here; the values are <see cref="DayValidator"/>'s.</summary>
    /// <param name="json">The contents of <c>game/day.json</c>.</param>
    /// <exception cref="InvalidDataException">The text is malformed or a part is missing.</exception>
    public static DayData Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (Number(root, "schemaVersion") != SchemaVersion)
            {
                throw new InvalidDataException($"{RelativePath} must have schemaVersion {SchemaVersion}.");
            }

            return new DayData(
                Number(root, "cycleSeconds"),
                Number(root, "dawnHour"),
                Number(root, "dayHour"),
                Number(root, "duskHour"),
                Number(root, "nightHour"),
                Number(root, "startHour"),
                Number(root, "wakeHour"));
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException($"{RelativePath} is malformed: {ex.Message}", ex);
        }
    }

    private static double Number(JsonElement element, string name) => element.GetProperty(name).GetDouble();
}
