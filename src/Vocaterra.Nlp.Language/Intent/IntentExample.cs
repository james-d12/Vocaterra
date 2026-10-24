using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vocaterra.Nlp.Language.Intent;

/// <summary>
/// One example utterance of an intent. In JSON it is either a plain string or an object
/// <c>{ "text": "yo es Rosa", "errors": ["conj.person"], "prompt": "introduce-open" }</c>; the object form lets the content lint check what the
/// grader says about the example. A plain string loads to an example with no expectations and is written back as a plain string, so a catalog that
/// only has string examples serializes exactly as before.
/// </summary>
/// <param name="Text">The utterance.</param>
/// <param name="ExpectedErrors">
/// The error tags the verdict must carry for the example (distinct, ordinal-sorted); an empty list asserts a clean verdict, and null means the example
/// states no expectation.
/// </param>
/// <param name="Prompt">The id of the prompt the expectation is evaluated against, or null to let the lint choose the first prompt (ordinal id) that allows the intent.</param>
[JsonConverter(typeof(IntentExampleJsonConverter))]
public sealed record IntentExample(string Text, IReadOnlyList<string>? ExpectedErrors = null, string? Prompt = null)
{
    /// <summary>True when the example states an expectation about the error tags.</summary>
    public bool HasExpectation => ExpectedErrors is not null;

    /// <summary>Structural equality including the expected tags.</summary>
    public bool Equals(IntentExample? other)
    {
        return other is not null
            && string.Equals(Text, other.Text, StringComparison.Ordinal)
            && string.Equals(Prompt, other.Prompt, StringComparison.Ordinal)
            && (ExpectedErrors is null ? other.ExpectedErrors is null : other.ExpectedErrors is not null && ExpectedErrors.SequenceEqual(other.ExpectedErrors));
    }

    /// <summary>Hash consistent with <see cref="Equals(IntentExample)"/>.</summary>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(Text, StringComparer.Ordinal);
        hash.Add(Prompt, StringComparer.Ordinal);
        if (ExpectedErrors is not null)
        {
            foreach (var tag in ExpectedErrors)
            {
                hash.Add(tag, StringComparer.Ordinal);
            }
        }

        return hash.ToHashCode();
    }
}

/// <summary>
/// The per-intent lint waivers: <c>"lint": { "allowOverlap": ["thank"], "allowRuleOnly": ["muchas gracias por todo"], "allowHomograph": ["costar"] }</c>.
/// A waived finding is still reported, marked as waived with the waiver that covers it.
/// </summary>
/// <param name="AllowOverlap">Intent ids this intent may overlap with: confusion findings between this intent and a listed one (in either direction) are waived.</param>
/// <param name="AllowRuleOnly">Example texts of this intent that are meant to be covered by a keyword rule only.</param>
/// <param name="AllowHomograph">Verb lemmas used in this intent's grammars whose forms are also nouns, and whose reading the author has checked.</param>
public sealed record IntentLintWaivers(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? AllowOverlap = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? AllowRuleOnly = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? AllowHomograph = null)
{
    /// <summary>No waivers.</summary>
    public static IntentLintWaivers None { get; } = new();

    /// <summary>True when the waivers list nothing.</summary>
    [JsonIgnore]
    public bool IsEmpty => (AllowOverlap?.Count ?? 0) == 0 && (AllowRuleOnly?.Count ?? 0) == 0 && (AllowHomograph?.Count ?? 0) == 0;

    /// <summary>Structural equality including the lists.</summary>
    public bool Equals(IntentLintWaivers? other)
    {
        return other is not null
            && Same(AllowOverlap, other.AllowOverlap)
            && Same(AllowRuleOnly, other.AllowRuleOnly)
            && Same(AllowHomograph, other.AllowHomograph);
    }

    /// <summary>Hash consistent with <see cref="Equals(IntentLintWaivers)"/>.</summary>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        foreach (var list in new[] { AllowOverlap, AllowRuleOnly, AllowHomograph })
        {
            foreach (var item in list ?? [])
            {
                hash.Add(item, StringComparer.Ordinal);
            }

            hash.Add('|');
        }

        return hash.ToHashCode();
    }

    private static bool Same(IReadOnlyList<string>? a, IReadOnlyList<string>? b)
    {
        return (a ?? []).SequenceEqual(b ?? []);
    }
}

/// <summary>Reads an example from a string or an object and writes it back as a string whenever it has no expectation and no prompt.</summary>
internal sealed class IntentExampleJsonConverter : JsonConverter<IntentExample>
{
    public override IntentExample Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return new IntentExample(reader.GetString()!);
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("An example must be a string or an object with 'text'.");
        }

        string? text = null;
        string? prompt = null;
        List<string>? errors = null;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var name = reader.GetString();
            reader.Read();
            switch (name)
            {
                case "text":
                    text = reader.TokenType == JsonTokenType.String ? reader.GetString() : throw new JsonException("An example's 'text' must be a string.");
                    break;
                case "prompt":
                    prompt = reader.TokenType == JsonTokenType.Null ? null : reader.TokenType == JsonTokenType.String ? reader.GetString() : throw new JsonException("An example's 'prompt' must be a string.");
                    break;
                case "errors":
                    errors = ReadTags(ref reader);
                    break;
                default:
                    throw new JsonException($"Unknown member '{name}' in an example; expected 'text', 'errors' or 'prompt'.");
            }
        }

        if (text is null)
        {
            throw new JsonException("An example object needs 'text'.");
        }

        return new IntentExample(text, errors?.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(), prompt);
    }

    public override void Write(Utf8JsonWriter writer, IntentExample value, JsonSerializerOptions options)
    {
        if (value.ExpectedErrors is null && value.Prompt is null)
        {
            writer.WriteStringValue(value.Text);
            return;
        }

        writer.WriteStartObject();
        writer.WriteString("text", value.Text);
        if (value.ExpectedErrors is not null)
        {
            writer.WriteStartArray("errors");
            foreach (var tag in value.ExpectedErrors)
            {
                writer.WriteStringValue(tag);
            }

            writer.WriteEndArray();
        }

        if (value.Prompt is not null)
        {
            writer.WriteString("prompt", value.Prompt);
        }

        writer.WriteEndObject();
    }

    private static List<string>? ReadTags(ref Utf8JsonReader reader)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("An example's 'errors' must be an array of error tags.");
        }

        var tags = new List<string>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType != JsonTokenType.String || string.IsNullOrWhiteSpace(reader.GetString()))
            {
                throw new JsonException("An example's 'errors' must hold non-empty strings.");
            }

            tags.Add(reader.GetString()!);
        }

        return tags;
    }
}
