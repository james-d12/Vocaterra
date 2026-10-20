using System.Collections.Concurrent;
using System.Text;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Stages;

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
    private readonly Dictionary<string, LexiconEntry[]> _byLemma;
    private readonly ConcurrentDictionary<string, Lazy<object>> _derived = new(StringComparer.Ordinal);

    private Lexicon(LexiconEntry[] sortedEntries, string? generatedBy)
    {
        _entries = sortedEntries;
        GeneratedBy = generatedBy;
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
            var key = TextFolding.StripAccents(form);
            _byFoldedForm[key] = _byFoldedForm.TryGetValue(key, out var earlier) ? [.. earlier, .. run] : run;
            start = end;
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
    public static Lexicon Empty { get; } = new([], null);

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
        return Create(file.Entries, file.GeneratedBy);
    }

    /// <summary>
    /// Parses a lexicon JSON document and checks its schema version, its <c>entries</c> array and every entry against the schema, without building the
    /// indexes. The content loader reads each <c>lexicon/*.json</c> file this way and builds one lexicon from all of them with <see cref="FromEntries"/>.
    /// </summary>
    /// <exception cref="FormatException">The schema version is not supported, <c>entries</c> is missing, or an entry breaks the schema.</exception>
    public static LexiconFile ReadFile(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return Validate(LexiconFileReader.Read(json));
    }

    /// <summary>The same as <see cref="ReadFile(string)"/> for UTF-8 bytes (a byte order mark is skipped); invalid UTF-8 is a <see cref="System.Text.Json.JsonException"/>.</summary>
    public static LexiconFile ReadFile(ReadOnlySpan<byte> utf8)
    {
        return Validate(LexiconFileReader.Read(utf8));
    }

    private static LexiconFile Validate(LexiconFile file)
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

            LexiconSchema.Validate(entry);
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
        return Create(entries, null);
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
        var key = TextFolding.Normalize(form);
        if (_byForm.TryGetValue(key, out var exact))
        {
            return new LexiconLookupResult(key, exact, accentInsensitive: false);
        }

        if (_byFoldedForm.TryGetValue(TextFolding.StripAccents(key), out var folded))
        {
            return new LexiconLookupResult(key, folded, accentInsensitive: true);
        }

        return new LexiconLookupResult(key, NoReadings, accentInsensitive: false);
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
        return index.TryGetValue(TextFolding.StripAccents(form), out var spellings) ? spellings : [];
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
        return _byLemma.TryGetValue(TextFolding.Normalize(lemma), out var entries) ? entries : [];
    }

    /// <summary>Returns true when at least one entry has this lemma.</summary>
    public bool ContainsLemma(string lemma)
    {
        ArgumentNullException.ThrowIfNull(lemma);
        return _byLemma.ContainsKey(TextFolding.Normalize(lemma));
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
            var key = TextFolding.StripAccents(entry.Form);
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

    private static Lexicon Create(IEnumerable<LexiconEntry> entries, string? generatedBy)
    {
        var list = new List<LexiconEntry>();
        foreach (var raw in entries)
        {
            ArgumentNullException.ThrowIfNull(raw);
            LexiconSchema.Validate(raw);
            var form = TextFolding.Normalize(raw.Form);
            var lemma = TextFolding.Normalize(raw.Lemma);
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

        return new Lexicon([.. unique], generatedBy);
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
        return cmp != 0 ? cmp : CompareNullable(a.Features.Gender, b.Features.Gender);
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
