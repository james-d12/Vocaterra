using System.Globalization;
using System.Text.Json;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Core.Activities;

/// <summary>
/// One fixture of a station: a catalogue noun the station's steps name (the bowl, the spoon, the oven) and the icon that draws it. A fixture is not an
/// inventory item (design 10.7).
/// </summary>
/// <param name="Noun">The catalogue noun: its id in the file (<c>bowl</c>), the target language's lemma once the book is read through the catalogue (<c>bol</c>, <see cref="StationBook.InLanguage"/>).</param>
/// <param name="Icon">The icon's key (<c>bowl</c>).</param>
public sealed record StationFixture(string Noun, string Icon);

/// <summary>
/// A station (W05; design 10.7): the scene prop it stands on (<c>oven</c>, its name in the scene), the NPC who teaches the procedures done there and its fixtures.
/// </summary>
/// <param name="Id">The station's id (<c>oven</c>, a neutral noun id), which a procedure names.</param>
/// <param name="Prop">The scene prop's name.</param>
/// <param name="Npc">The NPC who teaches there.</param>
/// <param name="Fixtures">The fixtures, in the file's order.</param>
public sealed record StationDefinition(string Id, string Prop, string Npc, IReadOnlyList<StationFixture> Fixtures)
{
    /// <summary>Whether the station has a fixture with this noun.</summary>
    /// <param name="noun">The catalogue noun.</param>
    public bool HasFixture(string noun) => Fixtures.Any(f => string.Equals(f.Noun, noun, StringComparison.Ordinal));
}

/// <summary>
/// The stations of the game (W05), read from the bundle's host file <c>game/stations.json</c> (schema 1, a list of stations). The book holds plain values;
/// the checks against the scene, the NPCs and the nouns are <see cref="Content.GameDataValidator"/>'s.
/// </summary>
public sealed class StationBook
{
    /// <summary>The file's path inside the bundle folder.</summary>
    public const string RelativePath = "game/stations.json";

    /// <summary>The schema version of the file.</summary>
    public const int SchemaVersion = 1;

    private StationBook(IReadOnlyList<StationDefinition> stations) => Stations = stations;

    /// <summary>The stations, in the file's order.</summary>
    public IReadOnlyList<StationDefinition> Stations { get; }

    /// <summary>The book of a bundle with no station.</summary>
    public static StationBook Empty { get; } = new([]);

    /// <summary>The station with this id, or <see langword="null"/>.</summary>
    /// <param name="id">The station's id.</param>
    public StationDefinition? Find(string id) => Stations.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.Ordinal));

    /// <summary>
    /// The same book with every fixture's noun read through the noun catalogue (T03): the file names a fixture by neutral id (<c>bowl</c>) and the learner is matched by
    /// the target language's word (<see cref="NounCatalog.LemmaOfId"/>). The station's own id stays an id. An id the catalogue does not have stays, for the validator.
    /// </summary>
    /// <param name="nouns">The noun catalogue.</param>
    public StationBook InLanguage(NounCatalog nouns)
    {
        ArgumentNullException.ThrowIfNull(nouns);
        return new StationBook(Stations.Select(station => station with
        {
            Fixtures = station.Fixtures.Select(fixture => fixture with { Noun = nouns.LemmaOfId(fixture.Noun) }).ToList(),
        }).ToList());
    }

    /// <summary>Reads the stations from the bundle's verified host file.</summary>
    /// <param name="bundle">The bundle, loaded with hash verification.</param>
    /// <exception cref="InvalidDataException">The file is missing from the bundle or malformed.</exception>
    public static StationBook Load(ContentBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        return Parse(GameData.HostText(bundle, RelativePath));
    }

    /// <summary>Builds the book from the file's text.</summary>
    /// <param name="json">The file's text.</param>
    /// <exception cref="InvalidDataException">The text is malformed, a station id is used twice, or a fixture has no noun or icon.</exception>
    public static StationBook Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.GetProperty("schemaVersion").GetInt32() != SchemaVersion)
            {
                throw new InvalidDataException($"{RelativePath} must have schemaVersion {SchemaVersion}.");
            }

            var stations = new List<StationDefinition>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var element in root.GetProperty("stations").EnumerateArray())
            {
                var id = element.GetProperty("id").GetString()!;
                if (!ids.Add(id))
                {
                    throw new InvalidDataException($"{RelativePath}: the station '{id}' is listed twice.");
                }

                var fixtures = element.GetProperty("fixtures").EnumerateArray().Select(static f => new StationFixture(
                    f.GetProperty("noun").GetString()!,
                    f.GetProperty("icon").GetString()!)).ToList();
                var repeated = fixtures.GroupBy(static f => f.Noun, StringComparer.Ordinal).FirstOrDefault(static g => g.Count() > 1);
                if (repeated is not null)
                {
                    throw new InvalidDataException(string.Create(CultureInfo.InvariantCulture, $"{RelativePath}: the station '{id}' has the fixture '{repeated.Key}' twice."));
                }

                stations.Add(new StationDefinition(id, element.GetProperty("prop").GetString()!, element.GetProperty("npc").GetString()!, fixtures));
            }

            return new StationBook(stations);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new InvalidDataException($"{RelativePath} is malformed: {ex.Message}", ex);
        }
    }
}
