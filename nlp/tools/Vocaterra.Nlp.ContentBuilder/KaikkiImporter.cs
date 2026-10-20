using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Language.Lexicon;

namespace Vocaterra.Nlp.ContentBuilder;

/// <summary>What the frequency filter did: the size of the list, how many lemmas were considered, kept and dropped, and where the cut fell.</summary>
/// <param name="FrequencyLines">Non-blank lines read from the frequency list.</param>
/// <param name="FrequencyWords">Distinct case-folded words of the list (accents are kept).</param>
/// <param name="FrequencyTotal">The sum of all counts of the list.</param>
/// <param name="Top">The number of lemmas to keep, or null when every lemma was kept.</param>
/// <param name="LemmasConsidered">Distinct lemmas the importer produced before filtering.</param>
/// <param name="LemmasWithFrequency">Lemmas with at least one form in the list.</param>
/// <param name="LemmasKept">Lemmas kept.</param>
/// <param name="LemmasDroppedNoFrequency">Lemmas dropped because none of their forms is in the list (only when <paramref name="Top"/> is set).</param>
/// <param name="LemmasDroppedUnattestedHeadWord">Lemmas with a frequency dropped because their own head word is not in the list (only when <paramref name="Top"/> is set).</param>
/// <param name="LemmasDroppedBelowCutoff">Lemmas with a frequency that did not make the top N.</param>
/// <param name="CutoffFrequency">With a top N, the summed frequency of the last (least frequent) kept lemma; otherwise 0.</param>
public sealed record FrequencyFilterReport(
    int FrequencyLines,
    int FrequencyWords,
    long FrequencyTotal,
    int? Top,
    int LemmasConsidered,
    int LemmasWithFrequency,
    int LemmasKept,
    int LemmasDroppedNoFrequency,
    int LemmasDroppedUnattestedHeadWord,
    int LemmasDroppedBelowCutoff,
    long CutoffFrequency);

/// <summary>The outcome of one import: the lexicon entries plus counts for the report.</summary>
/// <param name="Entries">The lexicon entries, validated, de-duplicated and ordinal-sorted.</param>
/// <param name="LinesRead">Non-blank input lines read.</param>
/// <param name="SpanishEntries">Lines whose <c>lang_code</c> is <c>es</c>.</param>
/// <param name="ImportedEntries">Spanish entries that produced at least one lexicon entry.</param>
/// <param name="SkippedByReason">Spanish entries that produced nothing, counted by reason (ordinal key order).</param>
/// <param name="Filter">What the frequency filter did, or null when no frequency list was given.</param>
public sealed record KaikkiImportResult(
    IReadOnlyList<LexiconEntry> Entries,
    int LinesRead,
    int SpanishEntries,
    int ImportedEntries,
    IReadOnlyDictionary<string, int> SkippedByReason,
    FrequencyFilterReport? Filter = null)
{
    /// <summary>Number of Spanish entries that produced nothing.</summary>
    public int SkippedEntries => SkippedByReason.Values.Sum();

    /// <summary>Noun lemmas dropped after the read because the lemma is the plural of another noun (see <c>KaikkiImporter</c>).</summary>
    public int PluralLookalikeLemmas { get; init; }

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

        if (PluralLookalikeLemmas > 0)
        {
            builder.Append(CultureInfo.InvariantCulture, $"  dropped {PluralLookalikeLemmas} noun lemmas that are only the plural of another noun\n");
        }

        if (Filter is { } filter)
        {
            builder.Append(CultureInfo.InvariantCulture, $"frequency list: {filter.FrequencyLines} lines, {filter.FrequencyWords} distinct words, {filter.FrequencyTotal} occurrences\n");
            builder.Append(CultureInfo.InvariantCulture, $"lemmas: {filter.LemmasConsidered} considered, {filter.LemmasWithFrequency} with a frequency, {filter.LemmasKept} kept");
            if (filter.Top is { } top)
            {
                builder.Append(CultureInfo.InvariantCulture, $" (top {top}: {filter.LemmasDroppedBelowCutoff} below the cutoff of {filter.CutoffFrequency}, {filter.LemmasDroppedUnattestedHeadWord} whose head word is not in the list, {filter.LemmasDroppedNoFrequency} without a frequency)");
            }

            builder.Append('\n');
        }

        return builder.ToString();
    }
}

/// <summary>The parameters of one import: an early stop, and the frequency list with the number of lemmas to keep.</summary>
public sealed record KaikkiImportOptions
{
    /// <summary>Stop after this many Spanish entries have been imported; all when null.</summary>
    public int? MaxEntries { get; init; }

    /// <summary>
    /// The words that hand-authored lexicons define. The import emits nothing for a lemma they define except forms of verbs: a hand-authored noun,
    /// adjective or function word keeps the gender, number and part of speech its author gave it, so a second Wiktionary reading (the matador
    /// <c>el espada</c> next to <c>la espada</c>) cannot hide a gender mistake on a word the lessons teach. The import also emits nothing under the
    /// spelling of a function word they define, neither a head word (the musical note <c>la</c>, the noun <c>si</c>) nor an inflected form (<c>una</c>
    /// and <c>uno</c> of <c>unir</c>, <c>para</c> of <c>parar</c>): the analyzer reads every reading of a word, so a rare noun or verb form hiding behind
    /// an article, a pronoun, a numeral, a preposition, a conjunction or an adverb would turn common sentences into false mistakes.
    /// </summary>
    public HandAuthoredLemmas? HandAuthored { get; init; }

    /// <summary>The frequency list that ranks the lemmas; without one every lemma is kept and no ranking is done.</summary>
    public FrequencyList? Frequency { get; init; }

    /// <summary>
    /// Keep this many lemmas, the most frequent first (ties broken by ordinal lemma order); lemmas with no frequency, and lemmas whose own head word is not in the list, are then dropped.
    /// Needs <see cref="Frequency"/>; all lemmas are kept when null.
    /// </summary>
    public int? Top { get; init; }
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
/// <para>
/// Pages that only inflect another word (<c>form_of</c> senses, or a head template that says "form"), alternative spellings, entries whose every
/// sense is obsolete, archaic, dated, nonstandard, gender-neutral, a neologism or a misspelling, names of letters, articles and verbs that are
/// not infinitives are skipped as well; each skip is counted by reason in the result. With <see cref="KaikkiImportOptions.Frequency"/> the
/// lemmas are ranked by the summed counts of their distinct forms and only the top <see cref="KaikkiImportOptions.Top"/> are kept; a lemma
/// whose own head word is not in the list is not eligible (so a rare verb cannot ride on a frequent word's spelling). With
/// <see cref="KaikkiImportOptions.HandAuthored"/> the importer defers to hand-authored lexicons: it adds no non-verb entries for their lemmas and
/// reserves the spellings of their function words. The rules are versioned by <see cref="ImporterVersion"/>, which the sidecar records.
/// </para>
/// </remarks>
public static partial class KaikkiImporter
{
    /// <summary>The <c>generated_by</c> value written to the lexicon file.</summary>
    public const string GeneratedBy = "wiktionary-import";

    /// <summary>
    /// The importer version, written to the sidecar of every generated file. Bump it whenever the importer can write different output for the same
    /// inputs and parameters (a new skip rule, a changed mapping, a changed ranking), so a stale generated file can be told from a current one.
    /// 1.0.0 was the M12 importer; 1.1.0 (M17a) skips inflected-form and alternative-form entries, ignores voseo and similar form tags, drops the future subjunctive and
    /// the -se imperfect subjunctive, and adds the frequency filter and the hand-authored deference.
    /// </summary>
    public const string ImporterVersion = "1.1.0";

    /// <summary>The skip reason for entries that only say "inflection of another word"; the lemma entry lists those forms itself.</summary>
    public const string InflectedFormReason = "inflected form of another entry (the lemma entry lists it)";

    /// <summary>The skip reason for entries that are only an alternative spelling, abbreviation or clipping of another word.</summary>
    public const string AlternativeFormReason = "alternative form or abbreviation of another entry";

    /// <summary>The skip reason for articles, which stay hand-authored: the analyzer reads the lemma <c>el</c> or <c>un</c> and the gender and number of each form.</summary>
    public const string ArticleReason = "article (hand-authored: the analyzer relies on the lemmas el and un)";

    /// <summary>The skip reason for entries whose every sense is obsolete, archaic, dated, nonstandard, gender-neutral, a neologism or a misspelling.</summary>
    public const string UnsuitableSensesReason = "every sense is obsolete, archaic, dated, nonstandard, gender-neutral, a neologism or a misspelling";

    /// <summary>The skip reason for nouns that only name a letter (<c>e</c> with the plural <c>es</c>, <c>te</c>, <c>ese</c>): they share their spelling with frequent function words and verb forms.</summary>
    public const string LetterNameReason = "name of a letter (shares its spelling with frequent function words)";

    /// <summary>The skip reason for a lemma that a hand-authored lexicon already defines; its readings stand (only verbs still add forms).</summary>
    public const string HandAuthoredReason = "lemma is defined by a hand-authored lexicon (its readings stand; verbs still add forms)";

    /// <summary>The skip reason for a word spelled like a function word (article, pronoun, numeral, preposition, conjunction, adverb) that a hand-authored lexicon defines.</summary>
    public const string FunctionWordReason = "spelled like a function word the hand-authored lexicon defines (la, si, mi, no ...)";

    /// <summary>The skip reason for verb entries whose head word does not end like an infinitive (Wiktionary files some "only used in se ..." forms as verbs).</summary>
    public const string NotAnInfinitiveReason = "verb head word is not an infinitive";

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

    // A word whose every sense carries one of these tags is not a lemma to teach; the senses of a lemma with some other, current sense do not matter.
    private static readonly ImmutableHashSet<string> UnsuitableSenseTags = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "obsolete",
        "archaic",
        "dated",
        "nonstandard",
        "neologism",
        "gender-neutral",
        "proscribed",
        "misspelling",
        "error-misspelling");

    private static readonly ImmutableHashSet<string> PluralOnlyTags = ImmutableHashSet.Create(StringComparer.Ordinal, "plural-only");

    private static readonly string[] InfinitiveEndings = ["ar", "er", "ir", "ír", "arse", "erse", "irse", "írse"];

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
        "vos-form",
        "alternative",
        "dialectal",
        "misspelling",
        "proscribed",
        "uncommon",
        "pronunciation-spelling",
        "error-unrecognized-form",
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
        return ImportFile(path, new KaikkiImportOptions { MaxEntries = maxEntries });
    }

    /// <summary>Reads the JSONL file and imports it with the options (an early stop, and a frequency list with the number of lemmas to keep).</summary>
    public static KaikkiImportResult ImportFile(string path, KaikkiImportOptions options)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        using var reader = new StreamReader(path, Encoding.UTF8);
        return Import(reader, options);
    }

    /// <summary>Imports from JSONL text; stops after <paramref name="maxEntries"/> Spanish entries have been imported (all when null).</summary>
    /// <exception cref="FormatException">A line is not valid JSON or not a JSON object.</exception>
    public static KaikkiImportResult Import(TextReader jsonl, int? maxEntries = null)
    {
        return Import(jsonl, new KaikkiImportOptions { MaxEntries = maxEntries });
    }

    /// <summary>
    /// Imports from JSONL text. With a frequency list the importer ranks the lemmas it produced by the summed count of their distinct forms in the
    /// list and, when <see cref="KaikkiImportOptions.Top"/> is set, keeps only the first N (see <see cref="KaikkiImportOptions"/>).
    /// </summary>
    /// <exception cref="FormatException">A line is not valid JSON or not a JSON object.</exception>
    public static KaikkiImportResult Import(TextReader jsonl, KaikkiImportOptions options)
    {
        ArgumentNullException.ThrowIfNull(jsonl);
        ArgumentNullException.ThrowIfNull(options);
        if (options.MaxEntries is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.MaxEntries, "The maximum must not be negative.");
        }

        if (options.Top is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.Top, "The number of lemmas to keep must be positive.");
        }

        if (options.Top is not null && options.Frequency is null)
        {
            throw new ArgumentException("Keeping the top N lemmas needs a frequency list.", nameof(options));
        }

        var maxEntries = options.MaxEntries;
        var entries = new List<LexiconEntry>();
        var buckets = options.Frequency is null ? null : new Dictionary<string, LemmaBucket>(StringComparer.Ordinal);
        var made = new List<LexiconEntry>();
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
            made.Clear();
            var reason = ImportEntry(root, made, options.HandAuthored);
            if (made.Count == 0)
            {
                skipped[reason ?? "no usable data"] = skipped.GetValueOrDefault(reason ?? "no usable data") + 1;
                continue;
            }

            imported++;
            if (buckets is null)
            {
                entries.AddRange(made);
            }
            else
            {
                AddToBucket(buckets, made, options.Frequency!);
            }
        }

        var lookalikes = PluralLookalikes(buckets is null ? entries : buckets.Values.SelectMany(static bucket => bucket.Entries));
        FrequencyFilterReport? report = null;
        if (buckets is not null)
        {
            foreach (var bucket in buckets.Values)
            {
                if (bucket.Entries.RemoveAll(entry => entry.Pos == LexiconSchema.Noun && lookalikes.Contains(entry.Lemma)) > 0)
                {
                    bucket.Recount(options.Frequency!);
                }
            }

            report = LemmaFrequencyFilter.Apply(buckets.Values, options.Frequency!, options.Top, entries);
        }
        else
        {
            entries.RemoveAll(entry => entry.Pos == LexiconSchema.Noun && lookalikes.Contains(entry.Lemma));
        }

        var lexicon = Lexicon.FromEntries(entries);
        return new KaikkiImportResult(lexicon.Entries, lines, spanish, imported, skipped, report) { PluralLookalikeLemmas = lookalikes.Count };
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
    // Wiktionary files some plural forms as nouns of their own: the suit "corazones" (hearts) and "diamantes" (diamonds) next to the plurals of corazón and
    // diamante, and the plural-only "pantalones" next to the plural of pantalón. As a second lemma, the plural form would also read as a singular noun, and
    // "1 diamantes" would no longer be a number mistake. The forms themselves stay: they are in the table of the real lemma.
    private static HashSet<string> PluralLookalikes(IEnumerable<LexiconEntry> entries)
    {
        var pluralsOfOtherLemmas = new HashSet<string>(StringComparer.Ordinal);
        var nounHeads = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (entry.Pos != LexiconSchema.Noun)
            {
                continue;
            }

            if (entry.Features.Number == LexiconSchema.Plural && !string.Equals(entry.Form, entry.Lemma, StringComparison.Ordinal))
            {
                pluralsOfOtherLemmas.Add(entry.Form);
            }

            if (string.Equals(entry.Form, entry.Lemma, StringComparison.Ordinal))
            {
                nounHeads.Add(entry.Lemma);
            }
        }

        nounHeads.IntersectWith(pluralsOfOtherLemmas);
        return nounHeads;
    }

    private static string? ImportEntry(JsonElement root, List<LexiconEntry> entries, HandAuthoredLemmas? handAuthored)
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

        if (pos == LexiconSchema.Article)
        {
            return ArticleReason;
        }

        if (AllSenses(root, "form_of") || IsInflectedFormPage(root))
        {
            return InflectedFormReason;
        }

        if (AllSenses(root, "alt_of"))
        {
            return AlternativeFormReason;
        }

        if (AllSensesTagged(root, UnsuitableSenseTags))
        {
            return UnsuitableSensesReason;
        }

        if (pos == LexiconSchema.Noun && AllSensesInCategory(root, " letter names"))
        {
            return LetterNameReason;
        }

        var lemma = word.Trim().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        if (lemma.Any(char.IsWhiteSpace) || lemma.StartsWith('-') || lemma.EndsWith('-'))
        {
            return "multi-word or affix head word";
        }

        if (!lemma.All(char.IsLetter))
        {
            return "head word has characters other than letters";
        }

        if (handAuthored is not null)
        {
            if (handAuthored.FunctionWordForms.Contains(lemma))
            {
                return FunctionWordReason;
            }

            if (pos != LexiconSchema.Verb && handAuthored.Lemmas.Contains(lemma))
            {
                return HandAuthoredReason;
            }
        }

        if (pos == LexiconSchema.Verb && !InfinitiveEndings.Any(ending => lemma.EndsWith(ending, StringComparison.Ordinal)))
        {
            return NotAnInfinitiveReason;
        }

        var made = new List<LexiconEntry>();
        switch (pos)
        {
            case LexiconSchema.Verb:
                made.Add(new LexiconEntry(lemma, lemma, pos, EntryFeatures.None));
                AddVerbForms(root, lemma, made, handAuthored);
                break;
            case LexiconSchema.Noun:
            case LexiconSchema.Adjective:
            case LexiconSchema.Article:
                AddAgreeingEntries(root, lemma, pos, made, handAuthored);
                break;
            default:
                made.Add(new LexiconEntry(lemma, lemma, pos, EntryFeatures.None));
                break;
        }

        entries.AddRange(made);
        return null;
    }

    private static void AddVerbForms(JsonElement root, string lemma, List<LexiconEntry> made, HandAuthoredLemmas? handAuthored)
    {
        foreach (var form in Forms(root))
        {
            var tags = Tags(form.Element);
            if (!IsUsableForm(form.Text, tags) || IsFunctionWordSpelling(form.Text, handAuthored))
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

            // The future subjunctive (hablare, hubiere) is obsolete in modern Spanish, and the imperfect subjunctive in -se (hablase, comiese) is a literary
            // variant of the one in -ra that fills the same cell; neither is worth the rare homographs and the size they add (a sixth of all verb forms).
            if ((tense == LexiconSchema.Future && mood == LexiconSchema.Subjunctive) || tags.Contains("imperfect-se"))
            {
                continue;
            }

            made.Add(new LexiconEntry(form.Text, lemma, LexiconSchema.Verb, new EntryFeatures(person, number, tense, mood, null)));
        }
    }

    private static void AddAgreeingEntries(JsonElement root, string lemma, string pos, List<LexiconEntry> made, HandAuthoredLemmas? handAuthored)
    {
        var formList = Forms(root).ToList();
        var hasFeminineForm = formList.Any(form => Tags(form.Element).Contains("feminine"));
        IReadOnlyList<string> headGenders = pos == LexiconSchema.Noun
            ? HeadGenders(root)
            : hasFeminineForm
                ? [LexiconSchema.Masculine]
                : [LexiconSchema.Masculine, LexiconSchema.Feminine]; // An adjective or article with no listed feminine form serves both genders.

        // A noun that only exists in the plural (pantalones, gafas) has the plural as its head word.
        var headNumber = pos == LexiconSchema.Noun && AllSensesTagged(root, PluralOnlyTags) ? LexiconSchema.Plural : LexiconSchema.Singular;
        AddHeadEntries(lemma, lemma, pos, headNumber, headGenders, made);
        foreach (var form in formList)
        {
            var tags = Tags(form.Element);
            if (!IsUsableForm(form.Text, tags) || IsFunctionWordSpelling(form.Text, handAuthored))
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

    // The spelling of a hand-authored function word is reserved for it: the form una of unir, uno of unir, para of parar, sobre of sobrar would make
    // the analyzer read every article, numeral, preposition and conjunction as a finite verb as well (and flag "ayer compré una espada" as a tense mistake).
    private static bool IsFunctionWordSpelling(string form, HandAuthoredLemmas? handAuthored)
    {
        return handAuthored is not null && handAuthored.FunctionWordForms.Contains(form);
    }

    private static bool IsUsableForm(string text, ImmutableHashSet<string> tags)
    {
        if (text.Length == 0 || !text.All(char.IsLetter))
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

    // Wiktionary's pages for inflected forms use the head template {{head|es|noun form}} (or "verb form", "adjective form"), even when the page also has
    // a sense of its own, such as the plural huevos that also means "guts". Those words are listed by their lemma's forms table.
    private static bool IsInflectedFormPage(JsonElement root)
    {
        if (!root.TryGetProperty("head_templates", out var templates) || templates.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var template in templates.EnumerateArray())
        {
            if (template.ValueKind == JsonValueKind.Object
                && template.TryGetProperty("args", out var args)
                && GetString(args, "2") is { } kind
                && kind.EndsWith(" form", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    // True when the entry has senses and every one of them lists a category whose name ends with the suffix ("Latin letter names", "Greek letter names").
    private static bool AllSensesInCategory(JsonElement root, string suffix)
    {
        if (!root.TryGetProperty("senses", out var senses) || senses.ValueKind != JsonValueKind.Array || senses.GetArrayLength() == 0)
        {
            return false;
        }

        foreach (var sense in senses.EnumerateArray())
        {
            if (sense.ValueKind != JsonValueKind.Object
                || !sense.TryGetProperty("categories", out var categories)
                || categories.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var found = false;
            foreach (var category in categories.EnumerateArray())
            {
                if (GetString(category, "name") is { } name && name.EndsWith(suffix, StringComparison.Ordinal))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                return false;
            }
        }

        return true;
    }

    // True when the entry has senses and every one of them has at least one of the tags.
    private static bool AllSensesTagged(JsonElement root, ImmutableHashSet<string> tags)
    {
        if (!root.TryGetProperty("senses", out var senses) || senses.ValueKind != JsonValueKind.Array || senses.GetArrayLength() == 0)
        {
            return false;
        }

        foreach (var sense in senses.EnumerateArray())
        {
            if (sense.ValueKind != JsonValueKind.Object || !Tags(sense).Overlaps(tags))
            {
                return false;
            }
        }

        return true;
    }

    // True when the entry has senses and every one of them carries a non-empty list under the property ("form_of", "alt_of").
    private static bool AllSenses(JsonElement root, string property)
    {
        if (!root.TryGetProperty("senses", out var senses) || senses.ValueKind != JsonValueKind.Array || senses.GetArrayLength() == 0)
        {
            return false;
        }

        foreach (var sense in senses.EnumerateArray())
        {
            if (sense.ValueKind != JsonValueKind.Object
                || !sense.TryGetProperty(property, out var target)
                || target.ValueKind != JsonValueKind.Array
                || target.GetArrayLength() == 0)
            {
                return false;
            }
        }

        return true;
    }

    // All entries made from one input line share the lemma; the bucket sums the list counts of the distinct forms found so far.
    private static void AddToBucket(Dictionary<string, LemmaBucket> buckets, List<LexiconEntry> made, FrequencyList frequency)
    {
        var lemma = made[0].Lemma;
        if (!buckets.TryGetValue(lemma, out var bucket))
        {
            bucket = new LemmaBucket(lemma);
            buckets.Add(lemma, bucket);
        }

        foreach (var entry in made)
        {
            bucket.Entries.Add(entry);
            if (frequency.TryGetCount(entry.Form, out var count))
            {
                if (bucket.CountedForms.Add(entry.Form))
                {
                    bucket.Frequency += count;
                }

                bucket.HeadWordInList |= string.Equals(entry.Form, lemma, StringComparison.Ordinal);
            }
        }
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
