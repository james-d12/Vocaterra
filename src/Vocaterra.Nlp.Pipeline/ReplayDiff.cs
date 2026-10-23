using System.Text.Json.Nodes;

namespace Vocaterra.Nlp.Pipeline;

/// <summary>The differences between a recorded and a replayed evaluation, as JSON pointer paths (RFC 6901) such as <c>/verdict/slots/qty</c>.</summary>
/// <param name="Paths">The paths of every differing value, in document order (object keys ordinal-sorted, array elements by index).</param>
public sealed record ReplayDiff(IReadOnlyList<string> Paths)
{
    /// <summary>Compares the path lists element by element.</summary>
    public bool Equals(ReplayDiff? other)
    {
        return other is not null && ListEquality.Equal(Paths, other.Paths);
    }

    /// <summary>Hash consistent with <see cref="Equals(ReplayDiff)"/>.</summary>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        ListEquality.Add(ref hash, Paths);
        return hash.ToHashCode();
    }

    /// <summary>Lists the paths separated by commas.</summary>
    public override string ToString()
    {
        return string.Join(", ", Paths);
    }
}

/// <summary>Structural comparison of two JSON documents that reports where they differ.</summary>
internal static class JsonDiff
{
    public static List<string> Paths(string expectedJson, string actualJson, string prefix = "")
    {
        var paths = new List<string>();
        Collect(JsonNode.Parse(expectedJson), JsonNode.Parse(actualJson), prefix, paths);
        return paths;
    }

    private static void Collect(JsonNode? expected, JsonNode? actual, string path, List<string> paths)
    {
        if (expected is JsonObject expectedObject && actual is JsonObject actualObject)
        {
            var keys = new SortedSet<string>(StringComparer.Ordinal);
            keys.UnionWith(expectedObject.Select(static pair => pair.Key));
            keys.UnionWith(actualObject.Select(static pair => pair.Key));
            foreach (var key in keys)
            {
                var child = path + "/" + Escape(key);
                var hasExpected = expectedObject.ContainsKey(key);
                if (hasExpected != actualObject.ContainsKey(key))
                {
                    paths.Add(child);
                }
                else
                {
                    Collect(expectedObject[key], actualObject[key], child, paths);
                }
            }

            return;
        }

        if (expected is JsonArray expectedArray && actual is JsonArray actualArray)
        {
            var count = Math.Max(expectedArray.Count, actualArray.Count);
            for (var i = 0; i < count; i++)
            {
                var child = path + "/" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (i >= expectedArray.Count || i >= actualArray.Count)
                {
                    paths.Add(child);
                }
                else
                {
                    Collect(expectedArray[i], actualArray[i], child, paths);
                }
            }

            return;
        }

        if (!string.Equals(expected?.ToJsonString(), actual?.ToJsonString(), StringComparison.Ordinal))
        {
            paths.Add(path);
        }
    }

    private static string Escape(string key)
    {
        return key.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
    }
}
