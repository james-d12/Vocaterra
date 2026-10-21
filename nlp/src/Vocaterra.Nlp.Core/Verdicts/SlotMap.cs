using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Vocaterra.Nlp.Core.Determinism;

namespace Vocaterra.Nlp.Core.Verdicts;

/// <summary>Immutable slot name to value map that always enumerates in ordinal key order.</summary>
[JsonConverter(typeof(SlotMapJsonConverter))]
public sealed class SlotMap : IReadOnlyDictionary<string, string>, IEquatable<SlotMap>
{
    private readonly KeyValuePair<string, string>[] _pairs;

    private SlotMap(KeyValuePair<string, string>[] sortedPairs)
    {
        _pairs = sortedPairs;
    }

    /// <summary>The map with no slots.</summary>
    public static SlotMap Empty { get; } = new([]);

    /// <summary>Number of slots.</summary>
    public int Count => _pairs.Length;

    /// <summary>The slot names in ordinal order.</summary>
    public IEnumerable<string> Keys => _pairs.Select(static pair => pair.Key);

    /// <summary>The slot values in key order.</summary>
    public IEnumerable<string> Values => _pairs.Select(static pair => pair.Value);

    /// <summary>Gets the value of a slot; throws <see cref="KeyNotFoundException"/> when absent.</summary>
    public string this[string key] =>
        TryGetValue(key, out var value) ? value : throw new KeyNotFoundException($"Slot '{key}' not found.");

    /// <summary>Builds a map from pairs; keys must be unique (ordinal) and neither keys nor values may be null.</summary>
    public static SlotMap From(IEnumerable<KeyValuePair<string, string>> pairs)
    {
        ArgumentNullException.ThrowIfNull(pairs);
        var array = pairs.ToArray();
        foreach (var pair in array)
        {
            ArgumentNullException.ThrowIfNull(pair.Key, "key");
            ArgumentNullException.ThrowIfNull(pair.Value, "value");
        }

        Array.Sort(array, static (a, b) => string.CompareOrdinal(a.Key, b.Key));
        for (var i = 1; i < array.Length; i++)
        {
            if (string.Equals(array[i - 1].Key, array[i].Key, StringComparison.Ordinal))
            {
                throw new ArgumentException($"Duplicate slot key '{array[i].Key}'.", nameof(pairs));
            }
        }

        return array.Length == 0 ? Empty : new SlotMap(array);
    }

    /// <summary>Returns a new map with the slot added or replaced.</summary>
    public SlotMap With(string key, string value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        var index = IndexOf(key);
        if (index >= 0)
        {
            var copy = (KeyValuePair<string, string>[])_pairs.Clone();
            copy[index] = new KeyValuePair<string, string>(key, value);
            return new SlotMap(copy);
        }

        var insertAt = ~index;
        var result = new KeyValuePair<string, string>[_pairs.Length + 1];
        Array.Copy(_pairs, 0, result, 0, insertAt);
        result[insertAt] = new KeyValuePair<string, string>(key, value);
        Array.Copy(_pairs, insertAt, result, insertAt + 1, _pairs.Length - insertAt);
        return new SlotMap(result);
    }

    /// <summary>
    /// Returns this map plus every slot of <paramref name="fallback"/> whose name this map does not have; where both have a name, this map's
    /// value stays.
    /// </summary>
    public SlotMap WithFallback(SlotMap fallback)
    {
        ArgumentNullException.ThrowIfNull(fallback);
        if (fallback.Count == 0)
        {
            return this;
        }

        var merged = _pairs.ToList();
        merged.AddRange(fallback._pairs.Where(pair => !ContainsKey(pair.Key)));
        return From(merged);
    }

    /// <summary>Returns true when the slot exists.</summary>
    public bool ContainsKey(string key)
    {
        return IndexOf(key) >= 0;
    }

    /// <summary>Looks up a slot value.</summary>
    public bool TryGetValue(string key, [MaybeNullWhen(false)] out string value)
    {
        var index = IndexOf(key);
        if (index >= 0)
        {
            value = _pairs[index].Value;
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>Enumerates the pairs in ordinal key order.</summary>
    public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
    {
        return ((IEnumerable<KeyValuePair<string, string>>)_pairs).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>Value equality over the ordinal-sorted pairs.</summary>
    public bool Equals(SlotMap? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is null || other._pairs.Length != _pairs.Length)
        {
            return false;
        }

        for (var i = 0; i < _pairs.Length; i++)
        {
            if (!string.Equals(_pairs[i].Key, other._pairs[i].Key, StringComparison.Ordinal)
                || !string.Equals(_pairs[i].Value, other._pairs[i].Value, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Value equality with any object.</summary>
    public override bool Equals(object? obj)
    {
        return Equals(obj as SlotMap);
    }

    /// <summary>Hash over the ordinal-sorted pairs.</summary>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        foreach (var pair in _pairs)
        {
            hash.Add(pair.Key, StringComparer.Ordinal);
            hash.Add(pair.Value, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }

    /// <summary>Formats as <c>{key=value, ...}</c> in key order.</summary>
    public override string ToString()
    {
        return "{" + string.Join(", ", _pairs.Select(static pair => $"{pair.Key}={pair.Value}")) + "}";
    }

    /// <summary>Value equality operator.</summary>
    public static bool operator ==(SlotMap? left, SlotMap? right) => left is null ? right is null : left.Equals(right);

    /// <summary>Value inequality operator.</summary>
    public static bool operator !=(SlotMap? left, SlotMap? right) => !(left == right);

    private int IndexOf(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        var low = 0;
        var high = _pairs.Length - 1;
        while (low <= high)
        {
            var mid = low + ((high - low) >> 1);
            var cmp = string.CompareOrdinal(_pairs[mid].Key, key);
            if (cmp == 0)
            {
                return mid;
            }

            if (cmp < 0)
            {
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return ~low;
    }
}
