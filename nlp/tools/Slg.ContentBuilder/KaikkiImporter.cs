using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Slg.Core.Determinism;
using Slg.Nlp.Lexicon;

namespace Slg.ContentBuilder;

/// <summary>The outcome of one import: the lexicon entries plus counts for the report.</summary>
/// <param name="Entries">The lexicon entries, validated, de-duplicated and ordinal-sorted.</param>
/// <param name="LinesRead">Non-blank input lines read.</param>
/// <param name="SpanishEntries">Lines whose <c>lang_code</c> is <c>es</c>.</param>
/// <param name="ImportedEntries">Spanish entries that produced at least one lexicon entry.</param>
/// <param name="SkippedByReason">Spanish entries that produced nothing, counted by reason (ordinal key order).</param>
public sealed record KaikkiImportResult(
    IReadOnlyList<LexiconEntry> Entries,
    int LinesRead,
    int SpanishEntries,
    int ImportedEntries,
    IReadOnlyDictionary<string, int> SkippedByReason)
{
    /// <summary>Number of Spanish entries that produced nothing.</summary>
    public int SkippedEntries => SkippedByReason.Values.Sum();

    /// <summary>A short multi-line summary of the counts.</summary>
    public string Summary()
    {
        var builder = new StringBuilder();
        builder.Append(CultureInfo.InvariantCulture, $"read {LinesRead} lines, {SpanishEntries} Spanish entries: ")
            .Append(CultureInfo.InvariantCulture, $"{ImportedEntries} imported into {Entries.Count} lexicon entries, {SkippedEntries} skipped\n");
        foreach (var pair in SkippedByReason)
        {
            builder.Append(CultureInfo.InvariantCulture, $"  skipped {pair.Value}: {pair.Key}\n");
        }

        return builder.ToString();
    }
}

/// <summary>
/// Converts wiktextract Spanish JSONL (as published on kaikki.org) into lexicon entries marked <c>wiktionary-import</c>.
/// </summary>
/// <remarks>
/// Only lines with <c>lang_code</c> <c>es</c> are read. The head word becomes an entry with the lexicon part of speech
/// (<c>noun, verb, adj, adv, prep, conj, pron, article, det, intj</c>; <c>det</c> maps to <c>adj</c> as in the seed lexicon). Gender comes from the
/// <c>senses[].tags</c> (<c>masculine</c>, <c>feminine</c>), else from the <c>es-noun</c> head template argument or its expansion text.
/// Every <c>forms[]</c> entry is mapped by its tags: person (<c>first-person</c> and so on), number (<c>singular</c>, <c>plural</c>),
/// tense (<c>present, preterite, imperfect, future, conditional</c>), mood (<c>indicative, subjunctive, imperative</c>) and gender
/// (<c>masculine, feminine</c>). Forms of several words, voseo forms, non-finite forms and forms tagged as obsolete, rare, diminutive and
/// similar are skipped. Proper names, phrases, affixes, contractions and numerals (whose lemma is a digit string in the lexicon schema) are skipped too.
/// </remarks>
public static partial class KaikkiImporter
{
    /// <summary>The <c>generated_by</c> value written to the lexicon file.</summary>
    public const string GeneratedBy = "wiktionary-import";

    private static readonly ImmutableDictionary<string, string> PartsOfSpeech = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["noun"] = LexiconSchema.Noun,
        ["verb"] = LexiconSchema.Verb,
        ["adj"] = LexiconSchema.Adjective,
        ["det"] = LexiconSchema.Adjective,
        ["adv"] = LexiconSchema.Adverb,
        ["prep"] = LexiconSchema.Preposition,
        ["conj"] = LexiconSchema.Conjunction,
        ["pron"] = LexiconSchema.Pronoun,
        ["article"] = LexiconSchema.Article,
        ["intj"] = LexiconSchema.Interjection,
    }.ToImmutableDictionary(StringComparer.Ordinal);

    private static readonly ImmutableHashSet<string> IgnoredFormTags = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "table-tags",
        "inflection-template",
        "class",
        "romanization",
        "multiword-construction",
        "error-misspelling",
        "obsolete",
        "archaic",
        "dated",
        "rare",
        "nonstandard",
        "superlative",
        "comparative",
        "diminutive",
        "augmentative",
        "voseo",
        "participle",
        "gerund",
        "infinitive",
        "combined-form");

    // An ordered table, not a dictionary: when a form carries several tense tags the first one in this order wins.
    private static readonly (string Tag, string Tense)[] Tenses =
    [
        ("present", LexiconSchema.Present),
        ("preterite", LexiconSchema.Preterite),
        ("imperfect", LexiconSchema.Imperfect),
        ("future", LexiconSchema.Future),
        ("conditional", LexiconSchema.Conditional),
    ];

    /// <summary>Reads the JSONL file and imports at most <paramref name="maxEntries"/> Spanish entries (all when null).</summary>
    public static KaikkiImportResult ImportFile(string path, int? maxEntries = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        using var reader = new StreamReader(path, Encoding.UTF8);
        return Import(reader, maxEntries);
    }

    /// <summary>Imports from JSONL text; stops after <paramref name="maxEntries"/> Spanish entries have been imported (all when null).</summary>
    /// <exception cref="FormatException">A line is not valid JSON or not a JSON object.</exception>
    public static KaikkiImportResult Import(TextReader jsonl, int? maxEntries = null)
    {
        ArgumentNullException.ThrowIfNull(jsonl);
        if (maxEntries is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxEntries), maxEntries, "The maximum must not be negative.");
        }

        var entries = new List<LexiconEntry>();
        var skipped = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var lines = 0;
        var spanish = 0;
        var imported = 0;
        var lineNumber = 0;
        while (maxEntries is null || imported < maxEntries)
        {
            var line = jsonl.ReadLine();
            if (line is null)
            {
                break;
            }

            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            lines++;
            using var document = Parse(line, lineNumber);
            var root = document.RootElement;
            if (!string.Equals(GetString(root, "lang_code"), "es", StringComparison.Ordinal))
            {
                continue;
            }

            spanish++;
            var before = entries.Count;
            var reason = ImportEntry(root, entries);
            if (entries.Count > before)
            {
                imported++;
            }
            else
            {
                skipped[reason ?? "no usable data"] = skipped.GetValueOrDefault(reason ?? "no usable data") + 1;
            }
        }

        var lexicon = Lexicon.FromEntries(entries);
        return new KaikkiImportResult(lexicon.Entries, lines, spanish, imported, skipped);
    }

    /// <summary>Formats entries as a lexicon JSON document (one entry per line, features without nulls, schema version 1, <c>generated_by</c> wiktionary-import).</summary>
    public static string ToJson(IReadOnlyList<LexiconEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var builder = new StringBuilder();
        builder.Append("{\n  \"schemaVersion\": ").Append(LexiconSchema.CurrentVersion.ToString(CultureInfo.InvariantCulture)).Append(",\n");
        builder.Append("  \"generated_by\": ").Append(CanonicalJson.Serialize(GeneratedBy)).Append(",\n");
        builder.Append("  \"entries\": [");
        for (var i = 0; i < entries.Count; i++)
        {
            builder.Append(i == 0 ? "\n    " : ",\n    ").Append(EntryJson(entries[i]));
        }

        builder.Append(entries.Count == 0 ? "]\n}\n" : "\n  ]\n}\n");
        return builder.ToString();
    }

    private static string EntryJson(LexiconEntry entry)
    {
        var features = new List<string>(5);
        AddFeature(features, "person", entry.Features.Person);
        AddFeature(features, "number", entry.Features.Number);
        AddFeature(features, "tense", entry.Features.Tense);
        AddFeature(features, "mood", entry.Features.Mood);
        AddFeature(features, "gender", entry.Features.Gender);
        return "{\"form\":" + CanonicalJson.Serialize(entry.Form)
            + ",\"lemma\":" + CanonicalJson.Serialize(entry.Lemma)
            + ",\"pos\":" + CanonicalJson.Serialize(entry.Pos)
            + ",\"features\":{" + string.Join(',', features) + "}}";
    }

    private static void AddFeature(List<string> features, string name, string? value)
    {
        if (value is not null)
        {
            features.Add("\"" + name + "\":" + CanonicalJson.Serialize(value));
        }
    }

    private static JsonDocument Parse(string line, int lineNumber)
    {
        try
        {
            var document = JsonDocument.Parse(line);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                document.Dispose();
                throw new FormatException($"Line {lineNumber}: expected a JSON object.");
            }

            return document;
        }
        catch (JsonException ex)
        {
            throw new FormatException($"Line {lineNumber}: invalid JSON ({ex.Message}).", ex);
        }
    }

    // Returns the reason nothing was imported, or null when entries were added.
    private static string? ImportEntry(JsonElement root, List<LexiconEntry> entries)
    {
        var word = GetString(root, "word");
        var rawPos = GetString(root, "pos");
        if (string.IsNullOrWhiteSpace(word))
        {
            return "no head word";
        }

        if (rawPos is null)
        {
            return "no part of speech";
        }

        if (string.Equals(rawPos, "num", StringComparison.Ordinal))
        {
            return "numeral (lemma is a digit string in the lexicon schema)";
        }

        if (!PartsOfSpeech.TryGetValue(rawPos, out var pos))
        {
            return $"unsupported part of speech '{rawPos}'";
        }

        var lemma = word.Trim().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        if (lemma.Any(char.IsWhiteSpace) || lemma.StartsWith('-') || lemma.EndsWith('-'))
        {
            return "multi-word or affix head word";
        }

        var made = new List<LexiconEntry>();
        switch (pos)
        {
            case LexiconSchema.Verb:
                made.Add(new LexiconEntry(lemma, lemma, pos, EntryFeatures.None));
                AddVerbForms(root, lemma, made);
                break;
            case LexiconSchema.Noun:
            case LexiconSchema.Adjective:
            case LexiconSchema.Article:
                AddAgreeingEntries(root, lemma, pos, made);
                break;
            default:
                made.Add(new LexiconEntry(lemma, lemma, pos, EntryFeatures.None));
                break;
        }

        entries.AddRange(made);
        return null;
    }

    private static void AddVerbForms(JsonElement root, string lemma, List<LexiconEntry> made)
    {
        foreach (var form in Forms(root))
        {
            var tags = Tags(form.Element);
            if (!IsUsableForm(form.Text, tags))
            {
                continue;
            }

            var person = tags.Contains("first-person") ? "1" : tags.Contains("second-person") ? "2" : tags.Contains("third-person") ? "3" : null;
            var number = tags.Contains("singular") ? LexiconSchema.Singular : tags.Contains("plural") ? LexiconSchema.Plural : null;
            string? tense = null;
            foreach (var (tag, value) in Tenses)
            {
                if (tags.Contains(tag))
                {
                    tense = value;
                    break;
                }
            }

            var mood = tags.Contains("subjunctive") ? LexiconSchema.Subjunctive
                : tags.Contains("imperative") ? LexiconSchema.Imperative
                : tags.Contains("indicative") ? LexiconSchema.Indicative
                : null;
            if (mood is null && tense is not null)
            {
                mood = LexiconSchema.Indicative;
            }

            if (person is null || number is null || mood is null || (tense is null && mood != LexiconSchema.Imperative))
            {
                continue;
            }

            if (mood == LexiconSchema.Imperative && tags.Contains("negative"))
            {
                continue;
            }

            made.Add(new LexiconEntry(form.Text, lemma, LexiconSchema.Verb, new EntryFeatures(person, number, tense, mood, null)));
        }
    }

    private static void AddAgreeingEntries(JsonElement root, string lemma, string pos, List<LexiconEntry> made)
    {
        var formList = Forms(root).ToList();
        var hasFeminineForm = formList.Any(form => Tags(form.Element).Contains("feminine"));
        IReadOnlyList<string> headGenders = pos == LexiconSchema.Noun
            ? HeadGenders(root)
            : hasFeminineForm
                ? [LexiconSchema.Masculine]
                : [LexiconSchema.Masculine, LexiconSchema.Feminine]; // An adjective or article with no listed feminine form serves both genders.

        AddHeadEntries(lemma, pos, headGenders, made);
        foreach (var form in formList)
        {
            var tags = Tags(form.Element);
            if (!IsUsableForm(form.Text, tags))
            {
                continue;
            }

            var plural = tags.Contains("plural");
            var gender = tags.Contains("feminine") ? LexiconSchema.Feminine : tags.Contains("masculine") ? LexiconSchema.Masculine : null;
            if (!plural && gender is null)
            {
                continue;
            }

            var number = plural ? LexiconSchema.Plural : LexiconSchema.Singular;
            var genders = gender is null ? headGenders : [gender];
            AddHeadEntries(form.Text, lemma, pos, number, genders, made);
        }
    }

    private static void AddHeadEntries(string lemma, string pos, IReadOnlyList<string> genders, List<LexiconEntry> made)
    {
        AddHeadEntries(lemma, lemma, pos, LexiconSchema.Singular, genders, made);
    }

    private static void AddHeadEntries(string form, string lemma, string pos, string number, IReadOnlyList<string> genders, List<LexiconEntry> made)
    {
        if (genders.Count == 0)
        {
            made.Add(new LexiconEntry(form, lemma, pos, new EntryFeatures(null, number, null, null, null)));
            return;
        }

        foreach (var gender in genders)
        {
            made.Add(new LexiconEntry(form, lemma, pos, new EntryFeatures(null, number, null, null, gender)));
        }
    }

    // Gender of a noun: sense tags first, then the es-noun template argument, then the template's expansion text.
    private static IReadOnlyList<string> HeadGenders(JsonElement root)
    {
        var genders = new SortedSet<string>(StringComparer.Ordinal);
        if (root.TryGetProperty("senses", out var senses) && senses.ValueKind == JsonValueKind.Array)
        {
            foreach (var sense in senses.EnumerateArray())
            {
                var tags = Tags(sense);
                AddIf(genders, tags.Contains("masculine"), LexiconSchema.Masculine);
                AddIf(genders, tags.Contains("feminine"), LexiconSchema.Feminine);
            }
        }

        if (genders.Count == 0 && root.TryGetProperty("head_templates", out var templates) && templates.ValueKind == JsonValueKind.Array)
        {
            foreach (var template in templates.EnumerateArray())
            {
                if (template.TryGetProperty("args", out var args) && args.ValueKind == JsonValueKind.Object)
                {
                    var first = GetString(args, "1");
                    AddIf(genders, first is not null && first.StartsWith('m'), LexiconSchema.Masculine);
                    AddIf(genders, first is not null && (first.StartsWith('f') || first.StartsWith("mf", StringComparison.Ordinal) || first.StartsWith("m-f", StringComparison.Ordinal)), LexiconSchema.Feminine);
                }

                if (genders.Count == 0 && GetString(template, "expansion") is { } expansion)
                {
                    // Only the text before the first parenthesis names the gender: "gato m (plural gatos, feminine gata)".
                    foreach (Match match in GenderToken().Matches(expansion.Split('(')[0]))
                    {
                        AddIf(genders, match.Value.Contains('m', StringComparison.Ordinal), LexiconSchema.Masculine);
                        AddIf(genders, match.Value.Contains('f', StringComparison.Ordinal), LexiconSchema.Feminine);
                    }
                }
            }
        }

        return [.. genders];
    }

    private static void AddIf(SortedSet<string> set, bool condition, string value)
    {
        if (condition)
        {
            set.Add(value);
        }
    }

    [GeneratedRegex(@"(?<![\p{L}-])(mf|m|f)(?![\p{L}-])", RegexOptions.CultureInvariant)]
    private static partial Regex GenderToken();

    private static bool IsUsableForm(string text, ImmutableHashSet<string> tags)
    {
        if (text.Length == 0 || text.Any(char.IsWhiteSpace) || text.Contains('-', StringComparison.Ordinal))
        {
            return false;
        }

        return !tags.Overlaps(IgnoredFormTags);
    }

    private static IEnumerable<(string Text, JsonElement Element)> Forms(JsonElement root)
    {
        if (!root.TryGetProperty("forms", out var forms) || forms.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var form in forms.EnumerateArray())
        {
            if (form.ValueKind == JsonValueKind.Object && GetString(form, "form") is { } text)
            {
                yield return (text.Trim().Normalize(NormalizationForm.FormC).ToLowerInvariant(), form);
            }
        }
    }

    private static ImmutableHashSet<string> Tags(JsonElement element)
    {
        var tags = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
        if (element.TryGetProperty("tags", out var array) && array.ValueKind == JsonValueKind.Array)
        {
            foreach (var tag in array.EnumerateArray())
            {
                if (tag.ValueKind == JsonValueKind.String)
                {
                    tags.Add(tag.GetString()!);
                }
            }
        }

        return tags.ToImmutable();
    }

    private static string? GetString(JsonElement element, string name)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
    }
}
