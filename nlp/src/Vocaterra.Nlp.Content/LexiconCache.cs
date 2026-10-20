using System.Collections.Concurrent;
using Vocaterra.Nlp.Language.Lexicon;
using NlpLexicon = Vocaterra.Nlp.Language.Lexicon.Lexicon;

namespace Vocaterra.Nlp.Content;

/// <summary>What the loader keeps of one lexicon file once it is parsed and checked.</summary>
/// <param name="GeneratedBy">The file's <c>generated_by</c> marker, or null.</param>
/// <param name="Entries">Number of entries in the file.</param>
/// <param name="Lemmas">Number of distinct lemmas in the file (NFC, lower case).</param>
internal readonly record struct LexiconFileSummary(string? GeneratedBy, int Entries, int Lemmas);

/// <summary>The merged lexicon of a bundle (null when a lexicon file was damaged, because there is nothing sound to merge) and the summary of each file that parsed.</summary>
internal sealed record LoadedLexicons(NlpLexicon? Lexicon, IReadOnlyDictionary<string, LexiconFileSummary> Files);

/// <summary>
/// An opt-in cache for callers that load many copies of one bundle which differ in files other than the lexicon, such as the fuzz oracle that loads
/// hundreds of damaged copies. The key is the paths and the SHA-256 of every lexicon file, so a copy whose lexicon files are byte for byte the
/// same as an earlier, valid set reuses the merged lexicon (an immutable object) instead of parsing and indexing tens of thousands of entries again.
/// The parsed entries of each valid lexicon file are kept by the SHA-256 of the file as well, so a copy in which only one small lexicon file differs
/// parses that file and merges, instead of parsing all of them again. A lexicon file that differs, or fails to parse, is never served from the cache. The loader uses no cache unless
/// <see cref="ContentLoadOptions.LexiconCache"/> is set.
/// </summary>
public sealed class LexiconCache
{
    private readonly ConcurrentDictionary<string, LoadedLexicons> _loaded = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, LexiconFile> _files = new(StringComparer.Ordinal);
    private readonly int _capacity;

    /// <summary>Creates a cache that holds at most <paramref name="capacity"/> merged lexicons; when it is full it forgets them all before keeping a new one.</summary>
    public LexiconCache(int capacity = 4)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
    }

    internal LoadedLexicons? Find(string key)
    {
        return _loaded.GetValueOrDefault(key);
    }

    internal LexiconFile? FindFile(string sha256)
    {
        return _files.GetValueOrDefault(sha256);
    }

    internal void AddFile(string sha256, LexiconFile file)
    {
        if (_files.Count >= _capacity * 4)
        {
            _files.Clear();
        }

        _files.TryAdd(sha256, file);
    }

    internal void Add(string key, LoadedLexicons loaded)
    {
        if (_loaded.Count >= _capacity)
        {
            _loaded.Clear();
        }

        _loaded.TryAdd(key, loaded);
    }
}
