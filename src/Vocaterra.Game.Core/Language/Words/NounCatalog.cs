using System.Text;
using System.Text.Json;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Language.Morphology;

namespace Vocaterra.Game.Core.Language.Words;

/// <summary>
/// One catalogue noun: its lemma, and from the core's lexicon and morphology its gender, its articles and its plural. The items and blocks it
/// names are the objects that turn from grey to colour as the word is learned (D-game-6).
/// </summary>
/// <param name="Lemma">The dictionary form (<c>árbol</c>).</param>
/// <param name="Gender">The lexicon's gender of the noun (<c>m</c> or <c>f</c>).</param>
/// <param name="Items">The item kinds it names.</param>
/// <param name="Definite">The definite article in the singular (<c>el</c>, <c>la</c>).</param>
/// <param name="Indefinite">The indefinite article in the singular (<c>un</c>, <c>una</c>).</param>
/// <param name="Plural">The plural form (<c>árboles</c>).</param>
/// <param name="Mass">Whether the noun is a mass noun, counted as <c>bloques de</c> (<c>madera</c>, <c>arena</c>).</param>
/// <param name="Sources">The catalogue nouns this one comes from (<c>madera</c> from <c>árbol</c>), for the guide's "where is it" answers.</param>
/// <param name="Blocks">The 3D block kinds it names (<c>agua</c> and <c>río</c> both name the water). Several nouns may name one block; the first one by lemma is its noun.</param>
public sealed record NounEntry(
    string Lemma,
    string Gender,
    IReadOnlyList<ItemId> Items,
    string Definite,
    string Indefinite,
    string Plural,
    bool Mass,
    IReadOnlyList<string> Sources,
    IReadOnlyList<BlockId> Blocks)
{
    /// <summary>The noun with its definite article: <c>el árbol</c>.</summary>
    public string WithDefinite => $"{Definite} {Lemma}";

    /// <summary>The noun with its indefinite article: <c>un árbol</c>.</summary>
    public string WithIndefinite => $"{Indefinite} {Lemma}";
}

/// <summary>
/// The noun catalogue: which block kinds and item kinds name which Spanish noun, and each noun's gender and articles from the bundle's lexicon
/// and morphology. The catalogue is the host-owned file <c>game/nouns.json</c> in the
/// bundle; it is the only place where a block or an item gets a word.
/// </summary>
/// <remarks>
/// The file is a host-owned bundle file (CR-007, core M26c): the loader does not parse it, but the manifest hashes it, so the catalogue is part
/// of the content hash and <see cref="Load"/> reads its verified bytes with <see cref="ContentBundle.TryReadHostFile"/>.
/// </remarks>
public sealed class NounCatalog
{
    /// <summary>The catalogue's path inside the bundle folder.</summary>
    public const string RelativePath = "game/nouns.json";

    private readonly Dictionary<string, NounEntry> _byLemma;
    private readonly Dictionary<ItemId, NounEntry> _byItem;
    private readonly Dictionary<BlockId, List<NounEntry>> _byBlock;

    private NounCatalog(IReadOnlyList<NounEntry> nouns)
    {
        _byLemma = new Dictionary<string, NounEntry>(StringComparer.Ordinal);
        _byItem = [];
        _byBlock = [];
        foreach (var noun in nouns)
        {
            _byLemma.Add(noun.Lemma, noun);
            foreach (var item in noun.Items)
            {
                _byItem.Add(item, noun);
            }
        }

        // Ordinal by lemma, so the first noun of a block is the same on every machine (agua before río, casa before tienda).
        foreach (var noun in nouns.OrderBy(static n => n.Lemma, StringComparer.Ordinal))
        {
            foreach (var block in noun.Blocks)
            {
                if (!_byBlock.TryGetValue(block, out var named))
                {
                    _byBlock[block] = named = [];
                }

                named.Add(noun);
            }
        }

        Nouns = nouns.OrderBy(static n => n.Lemma, StringComparer.Ordinal).ToList();
    }

    /// <summary>The catalogue's nouns, ordinal by lemma.</summary>
    public IReadOnlyList<NounEntry> Nouns { get; }

    /// <summary>
    /// Builds the catalogue from the bundle's host file <see cref="RelativePath"/>, whose bytes are the ones the load hashed against the
    /// manifest.
    /// </summary>
    /// <param name="bundle">The bundle, loaded with hash verification (the file must be a host file of it).</param>
    /// <exception cref="InvalidDataException">The bundle has no such host file, or the file is malformed, names an unknown item or block, or names a lemma with no noun reading.</exception>
    public static NounCatalog Load(ContentBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        if (!bundle.TryReadHostFile(RelativePath, out var bytes))
        {
            throw new InvalidDataException(
                $"The bundle has no host file {RelativePath}: it is missing from the manifest, or the bundle was loaded without hash verification.");
        }

        return Parse(bundle, Encoding.UTF8.GetString(bytes.Span));
    }

    /// <summary>Builds the catalogue from the file's text (the same format as <see cref="RelativePath"/>).</summary>
    /// <exception cref="InvalidDataException">The text is malformed, names an unknown item or block, or names a lemma with no noun reading.</exception>
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
        var claimedItems = new HashSet<ItemId>();
        foreach (var item in root.GetProperty("nouns").EnumerateArray())
        {
            var lemma = item.GetProperty("lemma").GetString() ?? throw new InvalidDataException("A catalogue noun has no lemma.");
            var items = item.GetProperty("items").EnumerateArray().Select(i => ItemNamed(i.GetString())).ToList();
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
            var blocks = item.TryGetProperty("blocks", out var blockValues)
                ? blockValues.EnumerateArray().Select(b => BlockNamed(b.GetString())).ToList()
                : new List<BlockId>();
            nouns.Add(Describe(bundle, lemma, items, blocks, mass, sources));
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

        return new NounCatalog(nouns);
    }

    /// <summary>The catalogue entry for a lemma, or <see langword="null"/> when the lemma is not a catalogue noun.</summary>
    public NounEntry? ForLemma(string lemma)
    {
        ArgumentNullException.ThrowIfNull(lemma);
        return _byLemma.TryGetValue(lemma, out var noun) ? noun : null;
    }

    /// <summary>The catalogue entry that names an item kind, or <see langword="null"/> when no noun names it.</summary>
    public NounEntry? ForItem(ItemId item) => _byItem.TryGetValue(item, out var noun) ? noun : null;

    /// <summary>The first catalogue noun that names a 3D block kind (by lemma order), or <see langword="null"/> when none does.</summary>
    public NounEntry? ForBlock(BlockId block) => _byBlock.TryGetValue(block, out var named) ? named[0] : null;

    /// <summary>Every catalogue noun that names a 3D block kind, in lemma order (empty when none does).</summary>
    public IReadOnlyList<NounEntry> NounsOfBlock(BlockId block) => _byBlock.TryGetValue(block, out var named) ? named : [];

    /// <summary>The 3D block kinds a catalogue noun names (empty for a noun with no block, or an unknown lemma).</summary>
    public IReadOnlyList<BlockId> BlocksOf(string lemma) => ForLemma(lemma)?.Blocks ?? [];

    /// <summary>
    /// The catalogue noun a word reading names, or <see langword="null"/> when the reading is not a noun of the catalogue. The reading is the
    /// one the core's resolver chose (<see cref="GuideWord"/>), so the word counts follow the verdict's analysis (CR-002).
    /// </summary>
    /// <param name="lemma">The reading's lemma.</param>
    /// <param name="pos">The reading's part of speech.</param>
    public NounEntry? ForReading(string? lemma, string? pos) =>
        lemma is not null && pos == "noun" ? ForLemma(lemma) : null;

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

    private static BlockId BlockNamed(string? name) =>
        BlockDefinition.FromName(name)?.Id ?? throw new InvalidDataException($"{RelativePath} names an unknown block '{name}'.");

    private static NounEntry Describe(
        ContentBundle bundle,
        string lemma,
        IReadOnlyList<ItemId> items,
        IReadOnlyList<BlockId> blocks,
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
            items,
            morphology.Article(Definiteness.Definite, gender, "sg", lemma),
            morphology.Article(Definiteness.Indefinite, gender, "sg", lemma),
            morphology.Pluralize(lemma),
            mass,
            sources,
            blocks);
    }
}
