using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Vocaterra.Nlp.Core.Determinism;
using Xunit.Sdk;

namespace Vocaterra.Nlp.Fuzzing.Tests.Oracles;

/// <summary>
/// Helpers shared by every oracle: failure messages that carry the generated input as paste-ready text, and the "run twice, compare
/// <see cref="CanonicalJson"/>, name the first differing path" check behind every determinism claim of the plan.
/// </summary>
public static class Determinism
{
    private const int MaxInputCharacters = 12_000;

    private static readonly JsonSerializerOptions TextOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Builds the exception an oracle throws: what broke, plus the generated input serialized so it can be pasted into a regression test.</summary>
    public static XunitException Fail(string oracle, string what, object? input)
    {
        return new XunitException($"[{oracle}] {what}\nInput: {Describe(input)}\nReplay: set CsCheck_Seed to the seed printed above, or paste the input into a [Fact] in the owning module's test project.");
    }

    /// <summary>
    /// Serializes an input for a failure message: strings as JSON string literals, everything else as canonical JSON, falling back to plain
    /// JSON (which tolerates NaN) and then to <c>ToString</c> for a value canonical JSON refuses.
    /// </summary>
    public static string Describe(object? input)
    {
        string text;
        try
        {
            text = input switch
            {
                null => "null",
                string s => JsonSerializer.Serialize(s, TextOptions),
                _ => SerializeObject(input),
            };
        }
        catch (Exception ex) when (ex is not XunitException)
        {
            text = Fallback(input, ex);
        }

        return text.Length <= MaxInputCharacters ? text : string.Concat(text.AsSpan(0, MaxInputCharacters), $"... [{text.Length - MaxInputCharacters} more characters]");
    }

    /// <summary>
    /// Runs the operation twice and requires byte-identical canonical JSON from both runs; the operations should build their own fresh
    /// instances so that hidden state would show up as a difference.
    /// </summary>
    public static void AssertSame<T>(string oracle, string what, object? input, Func<T> first, Func<T> second)
    {
        var left = CanonicalJson.Serialize(first());
        var right = CanonicalJson.Serialize(second());
        if (!string.Equals(left, right, StringComparison.Ordinal))
        {
            throw Fail(oracle, $"{what} is not deterministic; first difference at {FirstDifference(left, right)}.\nfirst:  {Truncate(left)}\nsecond: {Truncate(right)}", input);
        }
    }

    /// <summary>Returns the JSON path of the first difference between two JSON documents, or <c>(identical)</c>.</summary>
    public static string FirstDifference(string leftJson, string rightJson)
    {
        var left = JsonNode.Parse(leftJson);
        var right = JsonNode.Parse(rightJson);
        var path = Compare("$", left, right);
        if (path is not null)
        {
            return path;
        }

        if (string.Equals(leftJson, rightJson, StringComparison.Ordinal))
        {
            return "(identical)";
        }

        // The documents hold the same values but their text differs (number formatting or escaping); show where.
        var offset = 0;
        while (offset < leftJson.Length && offset < rightJson.Length && leftJson[offset] == rightJson[offset])
        {
            offset++;
        }

        string Around(string text) => text.Substring(Math.Max(0, offset - 40), Math.Min(80, text.Length - Math.Max(0, offset - 40)));
        return $"(same values, different text at offset {offset}: '{Around(leftJson)}' versus '{Around(rightJson)}')";
    }

    private static string Fallback(object? input, Exception reason)
    {
        try
        {
            return JsonSerializer.Serialize(input, new JsonSerializerOptions(TextOptions) { NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals });
        }
        catch (Exception ex) when (ex is not XunitException)
        {
            return $"{input} (not serializable: {reason.GetBaseException().Message})";
        }
    }

    private static string SerializeObject(object input)
    {
        var json = typeof(CanonicalJson).GetMethod(nameof(CanonicalJson.Serialize))!.MakeGenericMethod(input.GetType());
        return (string)json.Invoke(null, [input])!;
    }

    private static string Truncate(string text)
    {
        return text.Length <= 600 ? text : string.Concat(text.AsSpan(0, 600), "...");
    }

    private static string? Compare(string path, JsonNode? left, JsonNode? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null ? null : path;
        }

        switch (left)
        {
            case JsonObject leftObject when right is JsonObject rightObject:
                foreach (var pair in leftObject)
                {
                    if (!rightObject.TryGetPropertyValue(pair.Key, out var other))
                    {
                        return $"{path}.{pair.Key}";
                    }

                    var found = Compare($"{path}.{pair.Key}", pair.Value, other);
                    if (found is not null)
                    {
                        return found;
                    }
                }

                foreach (var pair in rightObject)
                {
                    if (!leftObject.ContainsKey(pair.Key))
                    {
                        return $"{path}.{pair.Key}";
                    }
                }

                return null;
            case JsonArray leftArray when right is JsonArray rightArray:
                for (var i = 0; i < Math.Min(leftArray.Count, rightArray.Count); i++)
                {
                    var found = Compare($"{path}[{i}]", leftArray[i], rightArray[i]);
                    if (found is not null)
                    {
                        return found;
                    }
                }

                return leftArray.Count == rightArray.Count ? null : $"{path}.length";
            default:
                return string.Equals(left.ToJsonString(), right.ToJsonString(), StringComparison.Ordinal) ? null : path;
        }
    }
}
