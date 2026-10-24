using System.Text;
using System.Text.Json;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Core.Items;

/// <summary>One ingredient of a recipe with the noun that names it.</summary>
/// <param name="Noun">The catalogue noun of the ingredient (<c>madera</c>).</param>
/// <param name="Item">The item kind it stands for.</param>
/// <param name="Count">How many one craft uses.</param>
public sealed record RecipeIngredient(NounEntry Noun, ItemId Item, int Count);

/// <summary>
/// One recipe as the game knows it: the nouns for the guide and the panel, and the plain <see cref="Rule"/> the simulation crafts with.
/// </summary>
/// <param name="Output">The noun of the item made (<c>mesa</c>).</param>
/// <param name="OutputCount">How many one craft gives.</param>
/// <param name="Ingredients">What one craft uses up, in the file's order.</param>
/// <param name="Station">The noun of the station (<c>mesa</c>), or <see langword="null"/> when the hands do.</param>
/// <param name="Rule">The simulation's value for the same recipe.</param>
public sealed record RecipeEntry(
    NounEntry Output,
    int OutputCount,
    IReadOnlyList<RecipeIngredient> Ingredients,
    NounEntry? Station,
    Recipe Rule);

/// <summary>
/// The crafting recipes of the bundle: the host-owned file <c>game/recipes.json</c> (read with the bytes the load hashed against the
/// manifest, like <see cref="NounCatalog"/>). The numbers live in that file and nowhere in code; the simulation receives the recipes as plain
/// values (<see cref="Rules"/>) and has no JSON of its own.
/// </summary>
public sealed class RecipeBook
{
    /// <summary>The recipes' path inside the bundle folder.</summary>
    public const string RelativePath = "game/recipes.json";

    private readonly Dictionary<string, int> _indexByOutput;

    private RecipeBook(IReadOnlyList<RecipeEntry> entries)
    {
        Entries = entries;
        Rules = entries.Select(static e => e.Rule).ToList();
        _indexByOutput = [];
        for (var i = 0; i < entries.Count; i++)
        {
            _indexByOutput.Add(entries[i].Output.Lemma, i);
        }
    }

    /// <summary>The recipes in the file's order (the order the panel shows and the index <c>PlayerCommand.Craft</c> uses).</summary>
    public IReadOnlyList<RecipeEntry> Entries { get; }

    /// <summary>The same recipes as plain simulation values, in the same order.</summary>
    public IReadOnlyList<Recipe> Rules { get; }

    /// <summary>
    /// Builds the book from the bundle's host file <see cref="RelativePath"/>, whose bytes are the ones the load hashed against the manifest.
    /// </summary>
    /// <exception cref="InvalidDataException">The file is missing from the bundle, is malformed, or names something that is not a catalogue noun.</exception>
    public static RecipeBook Load(ContentBundle bundle, NounCatalog nouns)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        if (!bundle.TryReadHostFile(RelativePath, out var bytes))
        {
            throw new InvalidDataException($"The bundle has no host file {RelativePath}: it is missing from the manifest, or the bundle was loaded without hash verification.");
        }

        return Parse(Encoding.UTF8.GetString(bytes.Span), nouns);
    }

    /// <summary>Builds the book from the file's text.</summary>
    /// <exception cref="InvalidDataException">The text is malformed, a lemma is not a catalogue noun with an item, a station names no block, or a count is not positive.</exception>
    public static RecipeBook Parse(string json, NounCatalog nouns)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(nouns);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1)
        {
            throw new InvalidDataException($"{RelativePath} must have schemaVersion 1.");
        }

        var entries = new List<RecipeEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in root.GetProperty("recipes").EnumerateArray())
        {
            var outputLemma = element.GetProperty("output").GetString() ?? throw new InvalidDataException("A recipe has no output.");
            var output = nouns.ForLemma(outputLemma) ?? throw new InvalidDataException($"{RelativePath}: output '{outputLemma}' is not a catalogue noun.");
            var outputItem = ItemOf(output, RelativePath);
            if (!seen.Add(outputLemma))
            {
                throw new InvalidDataException($"{RelativePath}: '{outputLemma}' has more than one recipe.");
            }

            var count = PositiveCount(element.GetProperty("count"), outputLemma);
            var ingredients = new List<RecipeIngredient>();
            foreach (var part in element.GetProperty("ingredients").EnumerateArray())
            {
                var lemma = part.GetProperty("item").GetString() ?? throw new InvalidDataException($"A recipe for '{outputLemma}' has an ingredient with no item.");
                var noun = nouns.ForLemma(lemma) ?? throw new InvalidDataException($"{RelativePath}: ingredient '{lemma}' of '{outputLemma}' is not a catalogue noun.");
                ingredients.Add(new RecipeIngredient(noun, ItemOf(noun, RelativePath), PositiveCount(part.GetProperty("count"), outputLemma)));
            }

            if (ingredients.Count == 0)
            {
                throw new InvalidDataException($"{RelativePath}: the recipe for '{outputLemma}' has no ingredients.");
            }

            NounEntry? station = null;
            BlockId? stationBlock = null;
            if (element.TryGetProperty("station", out var stationValue) && stationValue.ValueKind == JsonValueKind.String)
            {
                var lemma = stationValue.GetString()!;
                station = nouns.ForLemma(lemma) ?? throw new InvalidDataException($"{RelativePath}: station '{lemma}' of '{outputLemma}' is not a catalogue noun.");
                stationBlock = station.Blocks.Count > 0
                    ? station.Blocks[0]
                    : throw new InvalidDataException($"{RelativePath}: station '{lemma}' of '{outputLemma}' names no block.");
            }

            var rule = new Recipe(outputItem, count, ingredients.Select(static i => new Ingredient(i.Item, i.Count)).ToList(), stationBlock);
            entries.Add(new RecipeEntry(output, count, ingredients, station, rule));
        }

        return new RecipeBook(entries);
    }

    /// <summary>The recipe that makes the noun, or <see langword="null"/> when the noun has none (the guide then says it cannot be made).</summary>
    public RecipeEntry? ForOutput(string lemma)
    {
        ArgumentNullException.ThrowIfNull(lemma);
        return _indexByOutput.TryGetValue(lemma, out var index) ? Entries[index] : null;
    }

    /// <summary>The index of the recipe that makes the noun (the argument of <c>PlayerCommand.Craft</c>), or -1.</summary>
    public int IndexOf(string lemma)
    {
        ArgumentNullException.ThrowIfNull(lemma);
        return _indexByOutput.TryGetValue(lemma, out var index) ? index : -1;
    }

    private static int PositiveCount(JsonElement value, string recipe)
    {
        var count = value.GetInt32();
        return count > 0 ? count : throw new InvalidDataException($"{RelativePath}: a count in the recipe for '{recipe}' is not positive.");
    }

    private static ItemId ItemOf(NounEntry noun, string file) =>
        noun.Items.Count > 0 ? noun.Items[0] : throw new InvalidDataException($"{file}: '{noun.Lemma}' names no item.");
}
