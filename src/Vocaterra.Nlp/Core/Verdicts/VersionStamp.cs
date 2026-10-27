using System.Collections.Immutable;

namespace Vocaterra.Nlp.Core.Verdicts;

/// <summary>Content hash and model hashes that pin a verdict for replay; model hashes are kept ordinal-sorted.</summary>
public sealed record VersionStamp(string ContentHash, IReadOnlyDictionary<string, string> ModelHashes)
{
    /// <summary>A stamp with an empty content hash and no models.</summary>
    public static VersionStamp Empty { get; } = new(string.Empty, ImmutableSortedDictionary.Create<string, string>(StringComparer.Ordinal));

    /// <summary>Hash of the content bundle the verdict was produced with.</summary>
    public string ContentHash { get; init; } = ContentHash ?? throw new ArgumentNullException(nameof(ContentHash));

    /// <summary>Model name to model hash, normalized to an immutable ordinal-sorted dictionary.</summary>
    public IReadOnlyDictionary<string, string> ModelHashes { get; init; } = Normalize(ModelHashes);

    /// <summary>Structural equality including dictionary contents.</summary>
    public bool Equals(VersionStamp? other)
    {
        if (other is null || !string.Equals(ContentHash, other.ContentHash, StringComparison.Ordinal) || ModelHashes.Count != other.ModelHashes.Count)
        {
            return false;
        }

        foreach (var pair in ModelHashes)
        {
            if (!other.ModelHashes.TryGetValue(pair.Key, out var value) || !string.Equals(pair.Value, value, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Hash over the content hash and the sorted model hashes.</summary>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(ContentHash, StringComparer.Ordinal);
        foreach (var pair in ModelHashes)
        {
            hash.Add(pair.Key, StringComparer.Ordinal);
            hash.Add(pair.Value, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }

    private static ImmutableSortedDictionary<string, string> Normalize(IReadOnlyDictionary<string, string> source)
    {
        ArgumentNullException.ThrowIfNull(source, nameof(ModelHashes));
        if (source is ImmutableSortedDictionary<string, string> sorted && ReferenceEquals(sorted.KeyComparer, StringComparer.Ordinal))
        {
            return sorted;
        }

        return ImmutableSortedDictionary.CreateRange(StringComparer.Ordinal, source);
    }
}
