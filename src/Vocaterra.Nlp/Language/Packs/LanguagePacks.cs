using System.Collections.Immutable;

namespace Vocaterra.Nlp.Language.Packs;

/// <summary>
/// A registry of language packs by primary language subtag. <see cref="Default"/> holds the packs the engine ships with; <see cref="With"/> and
/// <see cref="Of"/> build a custom set, which a host passes through <see cref="Vocaterra.Nlp.Content.ContentLoadOptions.LanguagePacks"/>.
/// Immutable and thread-safe.
/// </summary>
public sealed class LanguagePacks
{
    private readonly ImmutableSortedDictionary<string, ILanguagePack> _packs;

    private LanguagePacks(ImmutableSortedDictionary<string, ILanguagePack> packs)
    {
        _packs = packs;
    }

    /// <summary>A registry with no pack.</summary>
    public static LanguagePacks Empty { get; } = new(ImmutableSortedDictionary.Create<string, ILanguagePack>(StringComparer.Ordinal));

    /// <summary>The packs the engine ships with: Spanish (<c>es</c>).</summary>
    public static LanguagePacks Default { get; } = Of(SpanishLanguagePack.Instance);

    /// <summary>The language ids registered, in ordinal order.</summary>
    public IReadOnlyList<string> Languages => [.. _packs.Keys];

    /// <summary>Builds a registry of exactly the given packs; a later pack replaces an earlier one with the same <see cref="ILanguagePack.LanguageId"/>.</summary>
    public static LanguagePacks Of(params ILanguagePack[] packs)
    {
        return Empty.With(packs);
    }

    /// <summary>Returns a new registry with the given packs added; one with the id of an existing pack replaces it.</summary>
    /// <exception cref="ArgumentException">A pack's <see cref="ILanguagePack.LanguageId"/> is not a lower case primary language subtag (2 to 8 ASCII letters).</exception>
    public LanguagePacks With(params ILanguagePack[] packs)
    {
        ArgumentNullException.ThrowIfNull(packs);
        var builder = _packs.ToBuilder();
        foreach (var pack in packs)
        {
            ArgumentNullException.ThrowIfNull(pack, nameof(packs));
            var id = pack.LanguageId;
            if (id is null || id.Length < 2 || id.Length > 8 || !id.All(static c => c is >= 'a' and <= 'z'))
            {
                throw new ArgumentException($"The language id '{id}' is not a lower case primary language subtag.", nameof(packs));
            }

            builder[id] = pack;
        }

        return new LanguagePacks(builder.ToImmutable());
    }

    /// <summary>Finds the pack of a BCP-47 tag by its primary subtag, ordinal and case-insensitive on the subtag (<c>es-MX</c>, <c>ES</c> and <c>es</c> find the same pack).</summary>
    /// <param name="tag">The tag.</param>
    /// <param name="pack">The pack when the method returns true.</param>
    /// <returns>False when the tag is malformed or no pack is registered for its primary subtag.</returns>
    public bool TryResolve(string? tag, out ILanguagePack pack)
    {
        if (TryGetPrimarySubtag(tag, out var primary) && _packs.TryGetValue(primary, out var found))
        {
            pack = found;
            return true;
        }

        pack = null!;
        return false;
    }

    /// <summary>
    /// Reads the primary language subtag of a well-formed BCP-47 tag as lower case. Well-formed here is the syntax of RFC 5646: subtags of 1 to 8 ASCII
    /// letters or digits separated by single hyphens, the first of 2 to 8 letters. The tag is not checked against the IANA registry.
    /// </summary>
    /// <param name="tag">The tag, for example <c>es-MX</c>.</param>
    /// <param name="primary">The primary subtag in lower case when the method returns true; otherwise empty.</param>
    /// <returns>True when the tag is well-formed.</returns>
    public static bool TryGetPrimarySubtag(string? tag, out string primary)
    {
        primary = string.Empty;
        if (string.IsNullOrEmpty(tag))
        {
            return false;
        }

        var subtags = tag.Split('-');
        for (var i = 0; i < subtags.Length; i++)
        {
            var subtag = subtags[i];
            if (subtag.Length is 0 or > 8 || !subtag.All(static c => IsAsciiLetter(c) || c is >= '0' and <= '9'))
            {
                return false;
            }

            if (i == 0 && (subtag.Length < 2 || !subtag.All(IsAsciiLetter)))
            {
                return false;
            }
        }

        primary = subtags[0].ToLowerInvariant();
        return true;
    }

    private static bool IsAsciiLetter(char c)
    {
        return c is >= 'a' and <= 'z' or >= 'A' and <= 'Z';
    }
}
