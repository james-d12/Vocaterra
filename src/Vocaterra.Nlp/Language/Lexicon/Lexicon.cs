using System.Collections.Concurrent;
using System.Text;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Language.Packs;

namespace Vocaterra.Nlp.Language.Lexicon;

/// <summary>An immutable word list with an ordinal-sorted index from surface form to readings.</summary>
public sealed class Lexicon
{
    /// <summary>Manifest resource name under which the seed lexicon is embedded in this assembly.</summary>
    public const string SeedResourceName = "Vocaterra.Nlp.Language.Lexicon.seed.json";

    private static readonly LexicalFeatures[] NoReadings = [];

    private readonly LexiconEntry[] _entries;
    private readonly string[] _lemmas;
    private readonly Dictionary<string, LexicalFeatures[]> _byForm;
    private readonly Dictionary<string, LexicalFeatures[]> _byFoldedForm;
    private readonly Dictionary<string, AccentVariant[]> _variantsByPlainForm;
    private readonly Dictionary<string, LexiconEntry[]> _byLemma;
    private readonly ConcurrentDictionary<string, Lazy<object>> _derived = new(StringComparer.Ordinal);

    private readonly ILexiconFolding _folding;

    private Lexicon(LexiconEntry[] sortedEntries, string? generatedBy, ILexiconFolding folding)
    {
        _entries = sortedEntries;
        GeneratedBy = generatedBy;
        _folding = folding;
        var count = sortedEntries.Length;
        var features = new LexicalFeatures[count];
        for (var i = 0; i < count; i++)
        {
            features[i] = sortedEntries[i].ToFeatures();
        }

        // The entries are sorted by form, so the readings of one form are one run. The accent-insensitive index takes the runs in the same order and
        // joins the runs whose forms fold to the same key (esta, está); a form without accents shares its array with the exact index.
        _byForm = new Dictionary<string, LexicalFeatures[]>(count, StringComparer.Ordinal);
        _byFoldedForm = new Dictionary<string, LexicalFeatures[]>(count, StringComparer.Ordinal);
        var variants = new Dictionary<string, List<AccentVariant>>(StringComparer.Ordinal);
        var start = 0;
        while (start < count)
        {
            var form = sortedEntries[start].Form;
            var end = start + 1;
            while (end < count && string.Equals(sortedEntries[end].Form, form, StringComparison.Ordinal))
            {
                end++;
            }

            var run = features[start..end];
            _byForm.Add(form, run);
            var key = folding.LenientKey(form);
            _byFoldedForm[key] = _byFoldedForm.TryGetValue(key, out var earlier) ? [.. earlier, .. run] : run;
            if (!string.Equals(key, form, StringComparison.Ordinal))
            {
                // An accented form: its readings are the accent variants of the plain spelling, in the order of the sorted entries.
                if (!variants.TryGetValue(key, out var forKey))
                {
                    forKey = [];
                    variants.Add(key, forKey);
                }

                foreach (var reading in run)
                {
                    forKey.Add(new AccentVariant(form, reading));
                }
            }

            start = end;
        }

        _variantsByPlainForm = new Dictionary<string, AccentVariant[]>(variants.Count, StringComparer.Ordinal);
        foreach (var pair in variants)
        {
            _variantsByPlainForm.Add(pair.Key, [.. pair.Value]);
        }

        var byLemma = new Dictionary<string, List<LexiconEntry>>(StringComparer.Ordinal);
        foreach (var entry in sortedEntries)
        {
            if (!byLemma.TryGetValue(entry.Lemma, out var forLemma))
            {
                forLemma = [];
                byLemma.Add(entry.Lemma, forLemma);
            }

            forLemma.Add(entry);
        }

        _byLemma = new Dictionary<string, LexiconEntry[]>(byLemma.Count, StringComparer.Ordinal);
        foreach (var pair in byLemma)
        {
            _byLemma.Add(pair.Key, [.. pair.Value]);
        }

        var lemmas = _byLemma.Keys.ToArray();
        Array.Sort(lemmas, StringComparer.Ordinal);
        _lemmas = lemmas;
    }

    /// <summary>The lexicon with no entries.</summary>
    public static Lexicon Empty { get; } = new([], null, DefaultFolding);

    // The lookup fold of the compatibility overloads and of the embedded seed: Spanish, because the seed lexicon is Spanish. A bundle's lexicon is built with
    // the folding of the bundle's pack instead (FromEntries(entries, folding, profile)); see ADR 0011.
    private static ILexiconFolding DefaultFolding => SpanishLanguagePack.Instance.Folding;

    /// <summary>The folding this lexicon builds its indexes with and looks words up with (the Spanish folding unless a pack's folding was given at load).</summary>
    public ILexiconFolding Folding => _folding;

    /// <summary>The seed lexicon embedded in this assembly; loaded once on first use.</summary>
    public static Lexicon Seed => SeedHolder.Instance;

    /// <summary>The provenance string of the source file (for example <c>hand-authored</c>), or null when built from entries.</summary>
    public string? GeneratedBy { get; }

    /// <summary>All entries, sorted ordinally by form, lemma, part of speech and then features; exact duplicates removed.</summary>
    public IReadOnlyList<LexiconEntry> Entries => _entries;

    /// <summary>The distinct lemmas in ordinal order.</summary>
    public IReadOnlyList<string> Lemmas => _lemmas;

    /// <summary>Parses and validates a lexicon JSON document (schema version 1).</summary>
    public static Lexicon Load(string json)
    {
        var file = ReadFile(json);
        return Create(file.Entries, file.GeneratedBy, DefaultFolding, LexiconProfile.V1);
    }

    /// <summary>Parses and validates a lexicon JSON document under the given language profile and builds its indexes with the given folding (M34, additive).</summary>
    /// <param name="json">The document.</param>
    /// <param name="folding">The pack's folding, which the indexes and every lookup use.</param>
    /// <param name="profile">The pack's closed vocabularies.</param>
    public static Lexicon Load(string json, ILexiconFolding folding, LexiconProfile profile)
    {
        ArgumentNullException.ThrowIfNull(folding);
        ArgumentNullException.ThrowIfNull(profile);
        var file = ReadFile(json, profile);
        return Create(file.Entries, file.GeneratedBy, folding, profile);
    }

    /// <summary>
    /// Parses a lexicon JSON document and checks its schema version, its <c>entries</c> array and every entry against the schema, without building the
    /// indexes. The content loader reads each <c>lexicon/*.json</c> file this way and builds one lexicon from all of them with <see cref="FromEntries(IEnumerable{LexiconEntry})"/>.
    /// </summary>
    /// <exception cref="FormatException">The schema version is not supported, <c>entries</c> is missing, or an entry breaks the schema.</exception>
    public static LexiconFile ReadFile(string json)
    {
        return ReadFile(json, LexiconProfile.V1);
    }

    /// <summary>The same as <see cref="ReadFile(string)"/> with the closed vocabularies of the given language profile (M34, additive).</summary>
    public static LexiconFile ReadFile(string json, LexiconProfile profile)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(profile);
        return Validate(LexiconFileReader.Read(json), profile);
    }

    /// <summary>The same as <see cref="ReadFile(string)"/> for UTF-8 bytes (a byte order mark is skipped); invalid UTF-8 is a <see cref="System.Text.Json.JsonException"/>.</summary>
    public static LexiconFile ReadFile(ReadOnlySpan<byte> utf8)
    {
        return Validate(LexiconFileReader.Read(utf8), LexiconProfile.V1);
    }

    /// <summary>The same as <see cref="ReadFile(ReadOnlySpan{byte})"/> with the closed vocabularies of the given language profile (M34, additive).</summary>
    public static LexiconFile ReadFile(ReadOnlySpan<byte> utf8, LexiconProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return Validate(LexiconFileReader.Read(utf8), profile);
    }

    private static LexiconFile Validate(LexiconFile file, LexiconProfile profile)
    {
        if (file.SchemaVersion != LexiconSchema.CurrentVersion)
        {
            throw new FormatException($"Unsupported lexicon schemaVersion {file.SchemaVersion}; expected {LexiconSchema.CurrentVersion}.");
        }

        // A missing "entries" property arrives as null although the record declares it non-nullable.
        if ((object?)file.Entries is null)
        {
            throw new FormatException("The lexicon needs an entries array.");
        }

        foreach (var entry in file.Entries)
        {
            if ((object?)entry is null)
            {
                throw new FormatException("The lexicon contains a null entry.");
            }

            profile.Validate(entry);
        }

        return file;
    }

    /// <summary>
    /// Builds a lexicon from entries, normalizing forms and lemmas (NFC, lowercase), validating and sorting them.
    /// This is also the merge policy for several lexicon files: pass the entries of all of them. Entries are unioned; two entries with the same
    /// form, lemma, part of speech and features are one reading and appear once, and different readings of one form (another lemma, another
    /// part of speech, other features) all stay and are all returned by <see cref="Lookup"/>.
    /// </summary>
    public static Lexicon FromEntries(IEnumerable<LexiconEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return Create(entries, null, DefaultFolding, LexiconProfile.V1);
    }

    /// <summary>Builds a lexicon like <see cref="FromEntries(IEnumerable{LexiconEntry})"/> with a pack's folding and profile (M34, additive).</summary>
    /// <param name="entries">The entries of all lexicon files.</param>
    /// <param name="folding">The pack's folding: forms and lemmas are stored under <see cref="ILexiconFolding.Key"/>, and the accent-insensitive index under <see cref="ILexiconFolding.LenientKey"/>.</param>
    /// <param name="profile">The pack's closed vocabularies, which every entry is validated against.</param>
    public static Lexicon FromEntries(IEnumerable<LexiconEntry> entries, ILexiconFolding folding, LexiconProfile profile)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(folding);
        ArgumentNullException.ThrowIfNull(profile);
        return Create(entries, null, folding, profile);
    }

    /// <summary>Reads the embedded seed lexicon JSON text.</summary>
    public static string ReadSeedJson()
    {
        using var stream = typeof(Lexicon).Assembly.GetManifestResourceStream(SeedResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{SeedResourceName}' is missing.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>Parses the embedded seed lexicon into a fresh instance.</summary>
    public static Lexicon LoadSeed()
    {
        return Load(ReadSeedJson());
    }

    /// <summary>Returns the readings of a surface form: exact matches first, else accent-insensitive matches flagged on the result.</summary>
    public LexiconLookupResult Lookup(string form)
    {
        var key = _folding.Key(form);
        if (_byForm.TryGetValue(key, out var exact))
        {
            return new LexiconLookupResult(key, exact, accentInsensitive: false);
        }

        if (_byFoldedForm.TryGetValue(_folding.LenientKey(key), out var folded))
        {
            return new LexiconLookupResult(key, folded, accentInsensitive: true);
        }

        return new LexiconLookupResult(key, NoReadings, accentInsensitive: false);
    }

    /// <summary>
    /// Returns what <see cref="Lookup"/> returns plus the accent variants of the form: when the form has no accents and is itself a lexicon form (<c>esta</c>),
    /// the readings of the accented forms that fold to it (<c>está</c>), each with its spelling, so that a caller can choose between the readings of the exact
    /// form and the accented ones from context. The variants are empty for a form with an accent (the writer chose it), for a form the lexicon lacks (its
    /// accented readings are already what <see cref="Lookup"/> returns, flagged accent-insensitive) and when no accented form folds to it.
    /// </summary>
    public LexiconAccentLookup LookupWithAccentVariants(string form)
    {
        var exact = Lookup(form);
        if (!exact.IsAccentInsensitive && exact.Count > 0 && _variantsByPlainForm.TryGetValue(exact.Form, out var variants))
        {
            return new LexiconAccentLookup(exact, variants);
        }

        return new LexiconAccentLookup(exact, []);
    }

    /// <summary>
    /// Returns the distinct surface forms that are the same word as <paramref name="form"/> once accents are ignored, in ordinal order, so
    /// the accented spellings of an accent-less form (<c>dias</c> gives <c>días</c>). Empty when no form folds to the same key; the form
    /// itself is one of them when it is a lexicon form.
    /// </summary>
    public IReadOnlyList<string> SpellingsIgnoringAccents(string form)
    {
        ArgumentNullException.ThrowIfNull(form);
        var index = Derive("spellings-by-folded-key", static lexicon => lexicon.BuildSpellingsByFoldedKey());
        return index.TryGetValue(_folding.LenientKey(form), out var spellings) ? spellings : [];
    }

    /// <summary>
    /// Returns a read-only structure derived from this lexicon alone (for example the index of regularized irregular forms), built on first use and
    /// shared by everything that uses this lexicon. The lexicon is immutable, so the structure is a pure function of it and every caller would build
    /// the same one; building it once keeps a second morphology generator over the same lexicon from repeating the work.
    /// </summary>
    internal T Derive<T>(string key, Func<Lexicon, T> build)
        where T : class
    {
        return (T)_derived.GetOrAdd(key, _ => new Lazy<object>(() => build(this), LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    /// <summary>Returns the entries of a lemma in entry order; empty when the lemma is unknown.</summary>
    public IReadOnlyList<LexiconEntry> EntriesForLemma(string lemma)
    {
        ArgumentNullException.ThrowIfNull(lemma);
        return _byLemma.TryGetValue(_folding.Key(lemma), out var entries) ? entries : [];
    }

    /// <summary>Returns true when at least one entry has this lemma.</summary>
    public bool ContainsLemma(string lemma)
    {
        ArgumentNullException.ThrowIfNull(lemma);
        return _byLemma.ContainsKey(_folding.Key(lemma));
    }

    /// <summary>Serializes the lexicon as canonical JSON (sorted entries, schema version 1).</summary>
    public string ToJson()
    {
        return CanonicalJson.Serialize(new LexiconFile(LexiconSchema.CurrentVersion, GeneratedBy, _entries));
    }

    // The entries are sorted by form, so the forms are adjacent and each is seen once.
    private Dictionary<string, string[]> BuildSpellingsByFoldedKey()
    {
        var groups = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        string? previous = null;
        foreach (var entry in _entries)
        {
            if (string.Equals(entry.Form, previous, StringComparison.Ordinal))
            {
                continue;
            }

            previous = entry.Form;
            var key = _folding.LenientKey(entry.Form);
            if (!groups.TryGetValue(key, out var forms))
            {
                forms = [];
                groups.Add(key, forms);
            }

            forms.Add(entry.Form);
        }

        var index = new Dictionary<string, string[]>(groups.Count, StringComparer.Ordinal);
        foreach (var pair in groups)
        {
            var forms = pair.Value.ToArray();
            Array.Sort(forms, StringComparer.Ordinal);
            index.Add(pair.Key, forms);
        }

        return index;
    }

    private static Lexicon Create(IEnumerable<LexiconEntry> entries, string? generatedBy, ILexiconFolding folding, LexiconProfile profile)
    {
        var list = new List<LexiconEntry>();
        foreach (var raw in entries)
        {
            ArgumentNullException.ThrowIfNull(raw);
            profile.Validate(raw);
            var form = folding.Key(raw.Form);
            var lemma = folding.Key(raw.Lemma);
            list.Add(ReferenceEquals(form, raw.Form) && ReferenceEquals(lemma, raw.Lemma) ? raw : raw with { Form = form, Lemma = lemma });
        }

        list.Sort(CompareEntries);
        var unique = new List<LexiconEntry>(list.Count);
        foreach (var entry in list)
        {
            if (unique.Count == 0 || CompareEntries(unique[^1], entry) != 0)
            {
                unique.Add(entry);
            }
        }

        return new Lexicon([.. unique], generatedBy, folding);
    }

    private static int CompareEntries(LexiconEntry a, LexiconEntry b)
    {
        var cmp = string.CompareOrdinal(a.Form, b.Form);
        if (cmp != 0)
        {
            return cmp;
        }

        cmp = string.CompareOrdinal(a.Lemma, b.Lemma);
        if (cmp != 0)
        {
            return cmp;
        }

        cmp = string.CompareOrdinal(a.Pos, b.Pos);
        if (cmp != 0)
        {
            return cmp;
        }

        cmp = CompareNullable(a.Features.Person, b.Features.Person);
        if (cmp != 0)
        {
            return cmp;
        }

        cmp = CompareNullable(a.Features.Number, b.Features.Number);
        if (cmp != 0)
        {
            return cmp;
        }

        cmp = CompareNullable(a.Features.Tense, b.Features.Tense);
        if (cmp != 0)
        {
            return cmp;
        }

        cmp = CompareNullable(a.Features.Mood, b.Features.Mood);
        if (cmp != 0)
        {
            return cmp;
        }

        cmp = CompareNullable(a.Features.Gender, b.Features.Gender);
        if (cmp != 0)
        {
            return cmp;
        }

        // The M34 members come last, so entries that leave them null sort exactly as before.
        cmp = CompareNullable(a.Features.Case, b.Features.Case);
        if (cmp != 0)
        {
            return cmp;
        }

        cmp = CompareNullable(a.Features.VerbForm, b.Features.VerbForm);
        if (cmp != 0)
        {
            return cmp;
        }

        cmp = CompareNullable(a.Features.Declension, b.Features.Declension);
        if (cmp != 0)
        {
            return cmp;
        }

        cmp = CompareNullable(a.Features.Particle, b.Features.Particle);
        return cmp != 0 ? cmp : CompareNullable(a.Features.Register, b.Features.Register);
    }

    private static int CompareNullable(string? a, string? b)
    {
        if (a is null)
        {
            return b is null ? 0 : -1;
        }

        return b is null ? 1 : string.CompareOrdinal(a, b);
    }

    private static class SeedHolder
    {
        internal static readonly Lexicon Instance = LoadSeed();
    }
}
