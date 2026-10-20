using System.Text;
using Slg.Core.Determinism;
using Slg.Core.Stages;

namespace Slg.Nlp.Lexicon;

/// <summary>An immutable word list with an ordinal-sorted index from surface form to readings.</summary>
public sealed class Lexicon
{
    /// <summary>Manifest resource name under which the seed lexicon is embedded in this assembly.</summary>
    public const string SeedResourceName = "Slg.Nlp.Lexicon.seed.json";

    private static readonly LexicalFeatures[] NoReadings = [];

    private readonly LexiconEntry[] _entries;
    private readonly string[] _lemmas;
    private readonly Dictionary<string, LexicalFeatures[]> _byForm;
    private readonly Dictionary<string, LexicalFeatures[]> _byFoldedForm;
    private readonly Dictionary<string, LexiconEntry[]> _byLemma;

    private Lexicon(LexiconEntry[] sortedEntries, string? generatedBy)
    {
        _entries = sortedEntries;
        GeneratedBy = generatedBy;
        _byForm = new Dictionary<string, LexicalFeatures[]>(StringComparer.Ordinal);
        _byFoldedForm = new Dictionary<string, LexicalFeatures[]>(StringComparer.Ordinal);
        _byLemma = new Dictionary<string, LexiconEntry[]>(StringComparer.Ordinal);

        var byForm = new Dictionary<string, List<LexicalFeatures>>(StringComparer.Ordinal);
        var byFolded = new Dictionary<string, List<LexicalFeatures>>(StringComparer.Ordinal);
        var byLemma = new Dictionary<string, List<LexiconEntry>>(StringComparer.Ordinal);
        foreach (var entry in sortedEntries)
        {
            Append(byForm, entry.Form, entry.ToFeatures());
            Append(byFolded, TextFolding.StripAccents(entry.Form), entry.ToFeatures());
            if (!byLemma.TryGetValue(entry.Lemma, out var forLemma))
            {
                forLemma = [];
                byLemma.Add(entry.Lemma, forLemma);
            }

            forLemma.Add(entry);
        }

        foreach (var pair in byForm)
        {
            _byForm.Add(pair.Key, [.. pair.Value]);
        }

        foreach (var pair in byFolded)
        {
            _byFoldedForm.Add(pair.Key, [.. pair.Value]);
        }

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
        ArgumentNullException.ThrowIfNull(json);
        var file = CanonicalJson.Deserialize<LexiconFile>(json);
        if (file.SchemaVersion != LexiconSchema.CurrentVersion)
        {
            throw new FormatException($"Unsupported lexicon schemaVersion {file.SchemaVersion}; expected {LexiconSchema.CurrentVersion}.");
        }

        // A missing "entries" property arrives as null although the record declares it non-nullable.
        if ((object?)file.Entries is null)
        {
            throw new FormatException("The lexicon needs an entries array.");
        }

        return Create(file.Entries, file.GeneratedBy);
    }

    /// <summary>Builds a lexicon from entries, normalizing forms and lemmas (NFC, lowercase), validating and sorting them.</summary>
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

    private static Lexicon Create(IEnumerable<LexiconEntry> entries, string? generatedBy)
    {
        var list = new List<LexiconEntry>();
        foreach (var raw in entries)
        {
            ArgumentNullException.ThrowIfNull(raw);
            LexiconSchema.Validate(raw);
            list.Add(raw with { Form = TextFolding.Normalize(raw.Form), Lemma = TextFolding.Normalize(raw.Lemma) });
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

    private static void Append(Dictionary<string, List<LexicalFeatures>> map, string key, LexicalFeatures value)
    {
        if (!map.TryGetValue(key, out var list))
        {
            list = [];
            map.Add(key, list);
        }

        list.Add(value);
    }

    private static class SeedHolder
    {
        internal static readonly Lexicon Instance = LoadSeed();
    }
}
