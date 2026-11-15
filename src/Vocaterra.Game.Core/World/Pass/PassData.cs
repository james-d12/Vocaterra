using System.Text.Json;
using Vocaterra.Game.Core.Content;

namespace Vocaterra.Game.Core.World.Pass;

/// <summary>
/// The lock of the exit through the east ring (<c>pass</c> of <c>game/world.json</c>, B08b, D-game-34 item 6): the lessons that must all be complete before the pass opens, the
/// scene prop whose blocks close the road until then, and the site where the road meets the valley's edge. No lesson id is in code: the list is the file's, and
/// <see cref="PassValidator"/> checks each id exists.
/// </summary>
/// <param name="RequiredLessons">The ids of the lessons that must all be complete (all of the game's lessons today).</param>
/// <param name="Rockfall">The name of the scene prop (<c>game/scene.json</c>) whose blocks are removed when the pass opens.</param>
/// <param name="Edge">The scene site where the road meets the valley's edge (the place the streamed world lifts its wall at, B07).</param>
public sealed record PassData(IReadOnlyList<string> RequiredLessons, string Rockfall, string Edge)
{
    /// <summary>The section's name in <c>game/world.json</c>.</summary>
    public const string SectionName = "pass";

    /// <summary>The lock of a world file that has no <c>pass</c> section: nothing is required, so the pass is open from the start, and there is no rockfall to remove.</summary>
    public static PassData None { get; } = new([], string.Empty, string.Empty);

    /// <summary>Reads the section from the bundle's <c>game/world.json</c>.</summary>
    /// <param name="bundle">The bundle, loaded with hash verification.</param>
    /// <exception cref="InvalidDataException">The file is malformed or the section is incomplete.</exception>
    public static PassData Load(Vocaterra.Nlp.Content.ContentBundle bundle) => Parse(GameData.HostText(bundle, WorldGenData.RelativePath));

    /// <summary>Reads the section from the text of <c>game/world.json</c>; <see cref="None"/> when it has none.</summary>
    /// <param name="json">The file's text.</param>
    /// <exception cref="InvalidDataException">The text is malformed or the section is incomplete.</exception>
    public static PassData Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty(SectionName, out var section))
            {
                return None;
            }

            return new PassData(
                section.GetProperty("requiredLessons").EnumerateArray().Select(static item => item.GetString() ?? string.Empty).ToArray(),
                section.GetProperty("rockfall").GetString() ?? string.Empty,
                section.GetProperty("edge").GetString() ?? string.Empty);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException($"{WorldGenData.RelativePath} is malformed: {ex.Message}", ex);
        }
    }
}
