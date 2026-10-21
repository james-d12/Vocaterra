using System.Text.Json;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Language.Morphology;
using Vocaterra.Simulation.Items;
using Vocaterra.Simulation.Tiles;

namespace Vocaterra.Game;

/// <summary>
/// One catalogue noun: its lemma, and from the core's lexicon and morphology its gender, its articles and its plural. The tiles and items it
/// names are the objects that turn from grey to colour as the word is learned (D-game-6).
/// </summary>
/// <param name="Lemma">The dictionary form (<c>árbol</c>).</param>
/// <param name="Gender">The lexicon's gender of the noun (<c>m</c> or <c>f</c>).</param>
/// <param name="Tiles">The tile kinds it names.</param>
/// <param name="Items">The item kinds it names.</param>
/// <param name="Definite">The definite article in the singular (<c>el</c>, <c>la</c>).</param>
/// <param name="Indefinite">The indefinite article in the singular (<c>un</c>, <c>una</c>).</param>
/// <param name="Plural">The plural form (<c>árboles</c>).</param>
/// <param name="Mass">Whether the noun is a mass noun, counted as <c>bloques de</c> (<c>madera</c>, <c>arena</c>).</param>
/// <param name="Sources">The catalogue nouns this one comes from (<c>madera</c> from <c>árbol</c>), for the guide's "where is it" answers.</param>
public sealed record NounEntry(
    string Lemma,
    string Gender,
    IReadOnlyList<TileId> Tiles,
    IReadOnlyList<ItemId> Items,
    string Definite,
    string Indefinite,
    string Plural,
    bool Mass,
    IReadOnlyList<string> Sources)
{
    /// <summary>The noun with its definite article: <c>el árbol</c>.</summary>
    public string WithDefinite => $"{Definite} {Lemma}";

    /// <summary>The noun with its indefinite article: <c>un árbol</c>.</summary>
    public string WithIndefinite => $"{Indefinite} {Lemma}";
}

/// <summary>
/// The noun catalogue: which tile kinds and item ids name which Spanish noun, which tiles are colourless (always drawn in colour), and each
/// noun's gender and articles from the bundle's lexicon and morphology. The catalogue is the host-owned file <c>game/nouns.json</c> in the
/// bundle; it is the only place where a tile or an item gets a word.
/// </summary>
/// <remarks>
/// <para>
/// <b>CR-007 workaround.</b> The 0.1.0 loader does not parse <c>game/nouns.json</c>; it is hashed by the bundle's manifest (the content tool
/// lists it), but the load's hash check does not cover it, and the bytes are read from the bundle folder after <c>ContentLoader.Load</c>.
/// </para>
/// <para>
/// <b>CR-002 workaround.</b> The verdict does not carry the normalized tokens with their readings, so
/// <see cref="CatalogueNounsOf"/> looks a surface form up in the bundle's lexicon itself (accent-insensitively, as the core does) and keeps the
/// readings that are catalogue nouns. The counts of <see cref="WordProgress"/> depend on it until the core exposes the analysis.
/// </para>
/// </remarks>
public sealed class NounCatalog
{
    /// <summary>The catalogue's path inside the bundle folder.</summary>
    public const string RelativePath = "game/nouns.json";

    private readonly ContentBundle _bundle;
    private readonly Dictionary<string, NounEntry> _byLemma;
    private readonly Dictionary<TileId, NounEntry> _byTile;
    private readonly Dictionary<ItemId, NounEntry> _byItem;
    private readonly HashSet<TileId> _colourless;

    private NounCatalog(
        ContentBundle bundle,
        IReadOnlyList<NounEntry> nouns,
        IReadOnlyList<TileId> colourless)
    {
        _bundle = bundle;
        _byLemma = new Dictionary<string, NounEntry>(StringComparer.Ordinal);
        _byTile = [];
        _byItem = [];
        foreach (var noun in nouns)
        {
            _byLemma.Add(noun.Lemma, noun);
            foreach (var tile in noun.Tiles)
            {
                _byTile.Add(tile, noun);
            }

            foreach (var item in noun.Items)
            {
                _byItem.Add(item, noun);
            }
        }

        _colourless = [.. colourless];
        Nouns = nouns.OrderBy(static n => n.Lemma, StringComparer.Ordinal).ToList();
        Colourless = colourless.OrderBy(static t => (int)t).ToList();
    }

    /// <summary>The catalogue's nouns, ordinal by lemma.</summary>
    public IReadOnlyList<NounEntry> Nouns { get; }

    /// <summary>The tile kinds that have no noun and are always drawn in colour (air, and anything else the file lists).</summary>
    public IReadOnlyList<TileId> Colourless { get; }

    /// <summary>Reads <see cref="RelativePath"/> from the bundle folder and builds the catalogue over the loaded bundle.</summary>
    /// <param name="bundle">The bundle loaded from <paramref name="bundleDirectory"/>.</param>
    /// <param name="bundleDirectory">The bundle folder (<c>content/game-v1</c>).</param>
    /// <exception cref="InvalidDataException">The file is missing, malformed, names an unknown tile, or names a lemma with no noun reading.</exception>
    public static NounCatalog Load(ContentBundle bundle, string bundleDirectory)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(bundleDirectory);
        var path = Path.Combine(bundleDirectory, RelativePath);
        if (!File.Exists(path))
        {
            throw new InvalidDataException($"The noun catalogue {RelativePath} is missing from {bundleDirectory}.");
        }

        return Parse(bundle, File.ReadAllText(path));
    }

    /// <summary>Builds the catalogue from the file's text (the same format as <see cref="RelativePath"/>).</summary>
    /// <exception cref="InvalidDataException">The text is malformed, names an unknown tile, or names a lemma with no noun reading.</exception>
    public static NounCatalog Parse(ContentBundle bundle, string json)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(json);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1)
        {
            throw new InvalidDataException($"{RelativePath} must have schemaVersion 1.");
        }

        var nouns = new List<NounEntry>();
        var claimed = new HashSet<TileId>();
        var claimedItems = new HashSet<ItemId>();
        foreach (var item in root.GetProperty("nouns").EnumerateArray())
        {
            var lemma = item.GetProperty("lemma").GetString() ?? throw new InvalidDataException("A catalogue noun has no lemma.");
            var tiles = item.GetProperty("tiles").EnumerateArray().Select(t => TileNamed(t.GetString())).ToList();
            var items = item.GetProperty("items").EnumerateArray().Select(i => ItemNamed(i.GetString())).ToList();
            foreach (var tile in tiles)
            {
                if (!claimed.Add(tile))
                {
                    throw new InvalidDataException($"{RelativePath} names tile {tile} more than once.");
                }
            }

            foreach (var kind in items)
            {
                if (!claimedItems.Add(kind))
                {
                    throw new InvalidDataException($"{RelativePath} names item {kind} more than once.");
                }
            }

            var mass = item.TryGetProperty("mass", out var massValue) && massValue.GetBoolean();
            var sources = item.TryGetProperty("sources", out var sourceValues)
                ? sourceValues.EnumerateArray().Select(s => s.GetString() ?? string.Empty).ToList()
                : new List<string>();
            nouns.Add(Describe(bundle, lemma, tiles, items, mass, sources));
        }

        var colourless = root.GetProperty("colourless").EnumerateArray().Select(t => TileNamed(t.GetString())).ToList();
        foreach (var tile in colourless)
        {
            if (!claimed.Add(tile))
            {
                throw new InvalidDataException($"{RelativePath} lists tile {tile} both as a noun and as colourless.");
            }
        }

        var lemmas = nouns.Select(static n => n.Lemma).ToHashSet(StringComparer.Ordinal);
        foreach (var noun in nouns)
        {
            foreach (var source in noun.Sources)
            {
                if (!lemmas.Contains(source))
                {
                    throw new InvalidDataException($"{RelativePath}: '{noun.Lemma}' comes from '{source}', which is not a catalogue noun.");
                }
            }
        }

        return new NounCatalog(bundle, nouns, colourless);
    }

    /// <summary>The catalogue entry for a lemma, or <see langword="null"/> when the lemma is not a catalogue noun.</summary>
    public NounEntry? ForLemma(string lemma)
    {
        ArgumentNullException.ThrowIfNull(lemma);
        return _byLemma.TryGetValue(lemma, out var noun) ? noun : null;
    }

    /// <summary>The catalogue entry that names a tile kind, or <see langword="null"/> for a colourless or unnamed tile.</summary>
    public NounEntry? ForTile(TileId tile) => _byTile.TryGetValue(tile, out var noun) ? noun : null;

    /// <summary>The catalogue entry that names an item kind, or <see langword="null"/> when no noun names it.</summary>
    public NounEntry? ForItem(ItemId item) => _byItem.TryGetValue(item, out var noun) ? noun : null;

    /// <summary>Whether a tile kind is listed as colourless (always in colour).</summary>
    public bool IsColourless(TileId tile) => _colourless.Contains(tile);

    /// <summary>
    /// The catalogue lemmas that a surface form can be, by its lexicon readings (accent-insensitive, as the core looks words up), in ordinal
    /// order. Empty for a word that is not a catalogue noun. This is the CR-002 workaround: see the type's remarks.
    /// </summary>
    public IReadOnlyList<string> CatalogueNounsOf(string surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        if (surface.Length == 0)
        {
            return [];
        }

        var lemmas = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var reading in _bundle.Lexicon.Lookup(surface))
        {
            if (reading.Pos == "noun" && _byLemma.ContainsKey(reading.Lemma))
            {
                lemmas.Add(reading.Lemma);
            }
        }

        return [.. lemmas];
    }

    private static TileId TileNamed(string? name)
    {
        foreach (var definition in TileDefinition.All)
        {
            if (string.Equals(definition.Name, name, StringComparison.Ordinal))
            {
                return definition.Id;
            }
        }

        throw new InvalidDataException($"{RelativePath} names an unknown tile '{name}'.");
    }

    private static ItemId ItemNamed(string? name)
    {
        foreach (var definition in ItemDefinition.All)
        {
            if (string.Equals(definition.Name, name, StringComparison.Ordinal))
            {
                return definition.Id;
            }
        }

        throw new InvalidDataException($"{RelativePath} names an unknown item '{name}'.");
    }

    private static NounEntry Describe(
        ContentBundle bundle,
        string lemma,
        IReadOnlyList<TileId> tiles,
        IReadOnlyList<ItemId> items,
        bool mass,
        IReadOnlyList<string> sources)
    {
        var gender = bundle.Lexicon.EntriesForLemma(lemma)
            .Select(static e => e.ToFeatures())
            .FirstOrDefault(static f => f.Pos == "noun" && f.Gender is not null)?.Gender
            ?? throw new InvalidDataException($"{RelativePath}: '{lemma}' has no noun reading with a gender in the lexicon.");
        var morphology = bundle.Morphology;
        return new NounEntry(
            lemma,
            gender,
            tiles,
            items,
            morphology.Article(Definiteness.Definite, gender, "sg", lemma),
            morphology.Article(Definiteness.Indefinite, gender, "sg", lemma),
            morphology.Pluralize(lemma),
            mass,
            sources);
    }
}
