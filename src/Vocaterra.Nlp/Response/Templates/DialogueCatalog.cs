using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vocaterra.Nlp.Core.Determinism;

namespace Vocaterra.Nlp.Response.Templates;

/// <summary>Maps each dialogue intent to its <see cref="VariantPool"/>; serialized as canonical JSON <c>{"pools":{"intent":{...}}}</c> with sorted keys.</summary>
[JsonConverter(typeof(DialogueCatalogJsonConverter))]
public sealed class DialogueCatalog : IEquatable<DialogueCatalog>
{
    private readonly ImmutableSortedDictionary<string, VariantPool> _pools;

    /// <summary>Builds a catalog; intents must be non-empty and unique, and pool ids and line ids must be unique across the catalog.</summary>
    public DialogueCatalog(IEnumerable<KeyValuePair<string, VariantPool>> pools)
    {
        ArgumentNullException.ThrowIfNull(pools);
        var builder = ImmutableSortedDictionary.CreateBuilder<string, VariantPool>(StringComparer.Ordinal);
        var poolIds = new HashSet<string>(StringComparer.Ordinal);
        var lineIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pair in pools)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                throw new ArgumentException("A dialogue intent must not be empty.", nameof(pools));
            }

            ArgumentNullException.ThrowIfNull(pair.Value, nameof(pools));
            if (builder.ContainsKey(pair.Key))
            {
                throw new ArgumentException($"Duplicate dialogue intent '{pair.Key}'.", nameof(pools));
            }

            if (!poolIds.Add(pair.Value.PoolId))
            {
                throw new ArgumentException($"Duplicate pool id '{pair.Value.PoolId}'.", nameof(pools));
            }

            foreach (var variant in pair.Value.Variants)
            {
                if (!lineIds.Add(variant.LineId))
                {
                    throw new ArgumentException($"Duplicate line id '{variant.LineId}'.", nameof(pools));
                }
            }

            builder.Add(pair.Key, pair.Value);
        }

        _pools = builder.ToImmutable();
    }

    /// <summary>The catalog with no pools.</summary>
    public static DialogueCatalog Empty { get; } = new([]);

    /// <summary>Number of dialogue intents.</summary>
    public int Count => _pools.Count;

    /// <summary>The pools keyed by dialogue intent, enumerating in ordinal key order.</summary>
    public IReadOnlyDictionary<string, VariantPool> Pools => _pools;

    /// <summary>Parses canonical JSON; throws <see cref="JsonException"/> or <see cref="ArgumentException"/> for invalid content.</summary>
    public static DialogueCatalog FromJson(string json)
    {
        return CanonicalJson.Deserialize<DialogueCatalog>(json);
    }

    /// <summary>Serializes the catalog as canonical JSON.</summary>
    public string ToJson()
    {
        return CanonicalJson.Serialize(this);
    }

    /// <summary>Looks up the pool of a dialogue intent.</summary>
    public bool TryGetPool(string dialogueIntent, [MaybeNullWhen(false)] out VariantPool pool)
    {
        ArgumentNullException.ThrowIfNull(dialogueIntent);
        return _pools.TryGetValue(dialogueIntent, out pool);
    }

    /// <summary>Looks up a pool by its own id (the id an agent action names); linear in the number of pools.</summary>
    public bool TryGetPoolById(string poolId, [MaybeNullWhen(false)] out VariantPool pool)
    {
        ArgumentNullException.ThrowIfNull(poolId);
        foreach (var candidate in _pools.Values)
        {
            if (string.Equals(candidate.PoolId, poolId, StringComparison.Ordinal))
            {
                pool = candidate;
                return true;
            }
        }

        pool = null;
        return false;
    }

    /// <summary>Value equality over the ordinal-sorted intent to pool pairs.</summary>
    public bool Equals(DialogueCatalog? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is null || other._pools.Count != _pools.Count)
        {
            return false;
        }

        using var left = _pools.GetEnumerator();
        using var right = other._pools.GetEnumerator();
        while (left.MoveNext() && right.MoveNext())
        {
            if (!string.Equals(left.Current.Key, right.Current.Key, StringComparison.Ordinal) || !left.Current.Value.Equals(right.Current.Value))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Value equality with any object.</summary>
    public override bool Equals(object? obj)
    {
        return Equals(obj as DialogueCatalog);
    }

    /// <summary>Hash over the ordinal-sorted pairs.</summary>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        foreach (var pair in _pools)
        {
            hash.Add(pair.Key, StringComparer.Ordinal);
            hash.Add(pair.Value);
        }

        return hash.ToHashCode();
    }
}
