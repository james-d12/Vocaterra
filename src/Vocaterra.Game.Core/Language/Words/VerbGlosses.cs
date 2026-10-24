using System.Text;
using System.Text.Json;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Core.Language.Words;

/// <summary>One verb's English (S20): the infinitive's gloss (<c>to want</c>), and the English third person singular and past where the regular rule does not give them.</summary>
/// <param name="Lemma">The infinitive (<c>querer</c>).</param>
/// <param name="English">The gloss, with its <c>to</c> (<c>to want</c>).</param>
/// <param name="Third">The third person singular (<c>has</c>), or <see langword="null"/> for the regular rule.</param>
/// <param name="Past">The past (<c>had</c>), or <see langword="null"/> for the regular rule.</param>
public sealed record VerbGloss(string Lemma, string English, string? Third, string? Past);

/// <summary>
/// The verbs' English, read from the bundle's host file <c>game/glosses.json</c> (S20, D-game-13: the English is on-screen text). The lexicon carries no
/// gloss, so the file gives one per verb the learner can meet; a verb not in it is shown by its infinitive alone.
/// </summary>
public sealed class VerbGlosses
{
    /// <summary>The file's path inside the bundle folder.</summary>
    public const string RelativePath = "game/glosses.json";

    private readonly Dictionary<string, VerbGloss> _verbs;

    private VerbGlosses(IEnumerable<VerbGloss> verbs)
    {
        _verbs = new Dictionary<string, VerbGloss>(StringComparer.Ordinal);
        foreach (var verb in verbs)
        {
            _verbs[verb.Lemma] = verb;
        }
    }

    /// <summary>No glosses at all.</summary>
    public static VerbGlosses Empty { get; } = new([]);

    /// <summary>Every gloss, by infinitive (ordinal).</summary>
    public IReadOnlyList<VerbGloss> All => _verbs.Values.OrderBy(static v => v.Lemma, StringComparer.Ordinal).ToList();

    /// <summary>Reads the bundle's file.</summary>
    /// <param name="bundle">The content bundle.</param>
    /// <exception cref="InvalidDataException">The file is missing from the bundle or is not a file this game reads.</exception>
    public static VerbGlosses Load(ContentBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        if (!bundle.TryReadHostFile(RelativePath, out var bytes))
        {
            throw new InvalidDataException($"The bundle has no host file {RelativePath}: it is missing from the manifest, or the bundle was loaded without hash verification.");
        }

        return Parse(Encoding.UTF8.GetString(bytes.Span));
    }

    /// <summary>Parses the file's text.</summary>
    /// <param name="json">The file.</param>
    /// <exception cref="InvalidDataException">A verb has no lemma or no English, or the schema is not version 1.</exception>
    public static VerbGlosses Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1)
        {
            throw new InvalidDataException($"{RelativePath}: the schema version is not 1.");
        }

        var verbs = new List<VerbGloss>();
        foreach (var element in root.GetProperty("verbs").EnumerateArray())
        {
            var lemma = Text(element, "lemma");
            var english = Text(element, "english");
            if (!english.StartsWith("to ", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"{RelativePath}: the gloss '{english}' of '{lemma}' does not start with 'to'.");
            }

            verbs.Add(new VerbGloss(lemma, english, Optional(element, "third"), Optional(element, "past")));
        }

        return new VerbGlosses(verbs);
    }

    /// <summary>The gloss of a verb, or <see langword="null"/> when the file has none.</summary>
    /// <param name="lemma">The infinitive.</param>
    public VerbGloss? ForLemma(string lemma) => _verbs.TryGetValue(lemma, out var verb) ? verb : null;

    private static string Text(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidDataException($"{RelativePath}: a verb has no '{name}'.");
        }

        return value.GetString()!;
    }

    private static string? Optional(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString() : null;
}
