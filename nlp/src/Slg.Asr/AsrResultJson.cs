using System.Collections.Immutable;
using System.Text;
using Slg.Core.Determinism;
using Slg.Core.Stages;

namespace Slg.Asr;

/// <summary>Canonical JSON helpers for persisting recognition results in replay logs.</summary>
public static class AsrResultJson
{
    /// <summary>Serializes one result to canonical JSON.</summary>
    public static string Serialize(AsrResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return CanonicalJson.Serialize(result);
    }

    /// <summary>Parses one result from canonical JSON.</summary>
    public static AsrResult Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return CanonicalJson.Deserialize<AsrResult>(json);
    }

    /// <summary>Serializes results keyed by clip hash as one JSON object with ordinal-sorted keys.</summary>
    public static string SerializeMap(IReadOnlyDictionary<string, AsrResult> byClipHash)
    {
        ArgumentNullException.ThrowIfNull(byClipHash);
        var keys = byClipHash.Keys.ToArray();
        Array.Sort(keys, StringComparer.Ordinal);
        var builder = new StringBuilder("{");
        for (var i = 0; i < keys.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            builder.Append(CanonicalJson.Serialize(keys[i])).Append(':').Append(Serialize(byClipHash[keys[i]]));
        }

        return builder.Append('}').ToString();
    }

    /// <summary>Parses a map written by <see cref="SerializeMap"/> into an ordinal-sorted immutable dictionary.</summary>
    public static IReadOnlyDictionary<string, AsrResult> DeserializeMap(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var map = CanonicalJson.Deserialize<Dictionary<string, AsrResult>>(json);
        return map.ToImmutableSortedDictionary(StringComparer.Ordinal);
    }

    /// <summary>Returns the canonical hash of one result (SHA-256 of its canonical JSON).</summary>
    public static string Hash(AsrResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return CanonicalJson.Hash(result);
    }
}
