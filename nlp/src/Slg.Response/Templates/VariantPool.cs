namespace Slg.Response.Templates;

/// <summary>A named set of interchangeable dialogue variants; one is chosen per interaction with a seeded generator.</summary>
public sealed record VariantPool(string PoolId, IReadOnlyList<DialogueTemplate> Variants)
{
    /// <summary>Stable identifier of the pool; never empty.</summary>
    public string PoolId { get; init; } = !string.IsNullOrWhiteSpace(PoolId)
        ? PoolId
        : throw new ArgumentException("A pool id is required.", nameof(PoolId));

    /// <summary>The variants in declaration order; never empty and with distinct line ids.</summary>
    public IReadOnlyList<DialogueTemplate> Variants { get; init; } = Validate(Variants);

    /// <summary>Structural equality including the variant list.</summary>
    public bool Equals(VariantPool? other)
    {
        return other is not null
            && string.Equals(PoolId, other.PoolId, StringComparison.Ordinal)
            && ListEquality.Equal(Variants, other.Variants);
    }

    /// <summary>Hash consistent with <see cref="Equals(VariantPool)"/>.</summary>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(PoolId, StringComparer.Ordinal);
        ListEquality.Add(ref hash, Variants);
        return hash.ToHashCode();
    }

    private static IReadOnlyList<DialogueTemplate> Validate(IReadOnlyList<DialogueTemplate> variants)
    {
        ArgumentNullException.ThrowIfNull(variants, nameof(Variants));
        if (variants.Count == 0)
        {
            throw new ArgumentException("A pool needs at least one variant.", nameof(Variants));
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var variant in variants)
        {
            ArgumentNullException.ThrowIfNull(variant, nameof(Variants));
            if (!seen.Add(variant.LineId))
            {
                throw new ArgumentException($"Duplicate line id '{variant.LineId}' in pool.", nameof(Variants));
            }
        }

        return variants;
    }
}
