namespace Vocaterra.Nlp.Core.Taxonomy;

/// <summary>
/// The error tags of one language, in rank order: the order in which errors at the same position sort. A language pack lists its own set
/// (<see cref="Language.Packs.ILanguagePack.ErrorTags"/>); <see cref="V1"/> is the frozen set of v1, which is the Spanish one and is what
/// <see cref="ErrorTags.All"/> lists. M34, additive.
/// </summary>
public sealed class ErrorTagSet
{
    private const string PronunciationPrefix = "pron.";

    private readonly string[] _tags;

    /// <summary>Creates a set from the tags in rank order.</summary>
    /// <param name="tags">The tags, most important first; the order is the rank. No tag may repeat or be empty.</param>
    /// <exception cref="ArgumentException">A tag is empty or repeated.</exception>
    public ErrorTagSet(IEnumerable<string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        _tags = [.. tags];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tag in _tags)
        {
            if (string.IsNullOrWhiteSpace(tag) || !seen.Add(tag))
            {
                throw new ArgumentException($"The error tag '{tag}' is empty or repeated.", nameof(tags));
            }
        }

        All = Array.AsReadOnly(_tags);
    }

    /// <summary>The frozen v1 set (the same tags in the same order as <see cref="ErrorTags.All"/>), which the Spanish pack uses.</summary>
    public static ErrorTagSet V1 { get; } = new(ErrorTags.All);

    /// <summary>The tags in rank order.</summary>
    public IReadOnlyList<string> All { get; }

    /// <summary>The rank of a tag: its position in <see cref="All"/>, or <see cref="Count"/> for a tag the set does not list, so unknown tags sort after all known ones.</summary>
    /// <param name="tag">The tag.</param>
    public int Rank(string tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        for (var i = 0; i < _tags.Length; i++)
        {
            if (string.Equals(_tags[i], tag, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return _tags.Length;
    }

    /// <summary>The number of tags.</summary>
    public int Count => _tags.Length;

    /// <summary>True when the set lists the tag (ordinal comparison).</summary>
    /// <param name="tag">The tag.</param>
    public bool IsKnown(string tag)
    {
        return Rank(tag) < _tags.Length;
    }

    /// <summary>True for a tag in the pronunciation family (<c>pron.*</c>).</summary>
    /// <param name="tag">The tag.</param>
    public bool IsPronunciation(string tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        return tag.StartsWith(PronunciationPrefix, StringComparison.Ordinal);
    }
}
