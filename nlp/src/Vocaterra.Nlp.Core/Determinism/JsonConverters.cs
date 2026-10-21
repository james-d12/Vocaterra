using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Nlp.Core.Determinism;

/// <summary>Writes an <see cref="IntentId"/> as a plain JSON string.</summary>
internal sealed class IntentIdJsonConverter : JsonConverter<IntentId>
{
    public override IntentId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("IntentId must be a JSON string.");
        }

        return new IntentId(reader.GetString()!);
    }

    public override void Write(Utf8JsonWriter writer, IntentId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

/// <summary>Writes a <see cref="TextSpan"/> as <c>{"start":n,"length":n}</c>.</summary>
internal sealed class TextSpanJsonConverter : JsonConverter<TextSpan>
{
    public override TextSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("TextSpan must be a JSON object.");
        }

        int? start = null;
        int? length = null;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return new TextSpan(
                    start ?? throw new JsonException("TextSpan is missing 'start'."),
                    length ?? throw new JsonException("TextSpan is missing 'length'."));
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Unexpected token in TextSpan.");
            }

            var name = reader.GetString();
            reader.Read();
            switch (name)
            {
                case "start":
                    start = reader.GetInt32();
                    break;
                case "length":
                    length = reader.GetInt32();
                    break;
                default:
                    throw new JsonException($"Unexpected TextSpan property '{name}'.");
            }
        }

        throw new JsonException("Unterminated TextSpan.");
    }

    public override void Write(Utf8JsonWriter writer, TextSpan value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("start", value.Start);
        writer.WriteNumber("length", value.Length);
        writer.WriteEndObject();
    }
}

/// <summary>Reads and writes string maps as JSON objects with ordinal-sorted keys.</summary>
internal static class SortedStringMapJson
{
    public static ImmutableSortedDictionary<string, string> Read(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected a JSON object of string values.");
        }

        var builder = ImmutableSortedDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return builder.ToImmutable();
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Unexpected token in string map.");
            }

            var key = reader.GetString()!;
            reader.Read();
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException($"Value of '{key}' must be a JSON string.");
            }

            if (builder.ContainsKey(key))
            {
                throw new JsonException($"Duplicate key '{key}'.");
            }

            builder.Add(key, reader.GetString()!);
        }

        throw new JsonException("Unterminated string map.");
    }

    public static void Write(Utf8JsonWriter writer, IEnumerable<KeyValuePair<string, string>> pairs)
    {
        var sorted = pairs.ToArray();
        Array.Sort(sorted, static (a, b) => string.CompareOrdinal(a.Key, b.Key));
        writer.WriteStartObject();
        foreach (var pair in sorted)
        {
            writer.WriteString(pair.Key, pair.Value);
        }

        writer.WriteEndObject();
    }
}

/// <summary>Writes a <see cref="SlotMap"/> as a JSON object with ordinal-sorted keys.</summary>
internal sealed class SlotMapJsonConverter : JsonConverter<SlotMap>
{
    public override SlotMap Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return SlotMap.From(SortedStringMapJson.Read(ref reader));
    }

    public override void Write(Utf8JsonWriter writer, SlotMap value, JsonSerializerOptions options)
    {
        SortedStringMapJson.Write(writer, value);
    }
}

/// <summary>Writes <c>IReadOnlyDictionary&lt;string,string&gt;</c> with ordinal-sorted keys and reads it into a sorted immutable dictionary.</summary>
internal sealed class StringMapJsonConverter : JsonConverter<IReadOnlyDictionary<string, string>>
{
    public override IReadOnlyDictionary<string, string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return SortedStringMapJson.Read(ref reader);
    }

    public override void Write(Utf8JsonWriter writer, IReadOnlyDictionary<string, string> value, JsonSerializerOptions options)
    {
        SortedStringMapJson.Write(writer, value);
    }
}
