using System.Text;
using System.Text.Json;

namespace Vocaterra.Nlp.Language.Lexicon;

/// <summary>
/// Reads a lexicon JSON document with <see cref="Utf8JsonReader"/> into a <see cref="LexiconFile"/>. It accepts exactly what the canonical
/// serializer writes (camelCase names, <c>generated_by</c>), ignores unknown properties like the serializer does, and reports damage as
/// <see cref="JsonException"/> (wrong JSON kind, malformed text) or <see cref="FormatException"/> (a missing <c>entries</c> array; the schema
/// checks of <see cref="LexiconSchema.Validate"/> happen in the caller). It exists because the imported lexicon has tens of thousands of entries
/// and the reflection-based deserializer was the largest part of loading a content bundle.
/// </summary>
internal static class LexiconFileReader
{
    public static LexiconFile Read(string json)
    {
        return Read(Encoding.UTF8.GetBytes(json));
    }

    public static LexiconFile Read(ReadOnlySpan<byte> utf8)
    {
        // A byte order mark is not part of the JSON text.
        if (utf8.Length >= 3 && utf8[0] == 0xEF && utf8[1] == 0xBB && utf8[2] == 0xBF)
        {
            utf8 = utf8[3..];
        }

        var reader = new Utf8JsonReader(utf8);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("The lexicon must be a JSON object.");
        }

        var schemaVersion = 0;
        string? generatedBy = null;
        List<LexiconEntry>? entries = null;
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            if (reader.ValueTextEquals("schemaVersion"u8))
            {
                reader.Read();
                if (reader.TokenType != JsonTokenType.Number || !reader.TryGetInt32(out schemaVersion))
                {
                    throw new JsonException("schemaVersion must be a whole number.");
                }
            }
            else if (reader.ValueTextEquals("generated_by"u8))
            {
                reader.Read();
                generatedBy = ReadStringOrNull(ref reader, "generated_by");
            }
            else if (reader.ValueTextEquals("entries"u8))
            {
                reader.Read();
                entries = ReadEntries(ref reader);
            }
            else
            {
                reader.Read();
                reader.Skip();
            }
        }

        if (reader.TokenType != JsonTokenType.EndObject)
        {
            throw new JsonException("The lexicon object is not well formed.");
        }

        // Anything after the closing brace is an error; Read() throws for non-whitespace text and returns false at the end.
        if (reader.Read())
        {
            throw new JsonException("Unexpected content after the lexicon object.");
        }

        // A missing "entries" property is reported by the caller like the serializer's null: the record declares it non-nullable.
        return new LexiconFile(schemaVersion, generatedBy, entries!);
    }

    private static List<LexiconEntry> ReadEntries(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("entries must be an array.");
        }

        var entries = new List<LexiconEntry>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                throw new FormatException("The lexicon contains a null entry.");
            }

            entries.Add(ReadEntry(ref reader));
        }

        return entries;
    }

    private static LexiconEntry ReadEntry(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Every lexicon entry must be an object.");
        }

        string? form = null;
        string? lemma = null;
        string? pos = null;
        EntryFeatures? features = null;
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            if (reader.ValueTextEquals("form"u8))
            {
                reader.Read();
                form = ReadStringOrNull(ref reader, "form");
            }
            else if (reader.ValueTextEquals("lemma"u8))
            {
                reader.Read();
                lemma = ReadStringOrNull(ref reader, "lemma");
            }
            else if (reader.ValueTextEquals("pos"u8))
            {
                reader.Read();
                pos = ReadKnownOrNull(ref reader, LexiconSchema.PartsOfSpeechArray, "pos");
            }
            else if (reader.ValueTextEquals("features"u8))
            {
                reader.Read();
                features = ReadFeatures(ref reader);
            }
            else
            {
                reader.Read();
                reader.Skip();
            }
        }

        if (reader.TokenType != JsonTokenType.EndObject)
        {
            throw new JsonException("A lexicon entry is not well formed.");
        }

        // Missing members stay null; LexiconSchema.Validate rejects them with a FormatException that names the entry.
        return new LexiconEntry(form!, lemma!, pos!, features!);
    }

    private static EntryFeatures? ReadFeatures(ref Utf8JsonReader reader)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("features must be an object.");
        }

        string? person = null;
        string? number = null;
        string? tense = null;
        string? mood = null;
        string? gender = null;
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            if (reader.ValueTextEquals("person"u8))
            {
                reader.Read();
                person = ReadKnownOrNull(ref reader, LexiconSchema.PersonsArray, "person");
            }
            else if (reader.ValueTextEquals("number"u8))
            {
                reader.Read();
                number = ReadKnownOrNull(ref reader, LexiconSchema.NumbersArray, "number");
            }
            else if (reader.ValueTextEquals("tense"u8))
            {
                reader.Read();
                tense = ReadKnownOrNull(ref reader, LexiconSchema.TensesArray, "tense");
            }
            else if (reader.ValueTextEquals("mood"u8))
            {
                reader.Read();
                mood = ReadKnownOrNull(ref reader, LexiconSchema.MoodsArray, "mood");
            }
            else if (reader.ValueTextEquals("gender"u8))
            {
                reader.Read();
                gender = ReadKnownOrNull(ref reader, LexiconSchema.GendersArray, "gender");
            }
            else
            {
                reader.Read();
                reader.Skip();
            }
        }

        if (reader.TokenType != JsonTokenType.EndObject)
        {
            throw new JsonException("features is not well formed.");
        }

        return new EntryFeatures(person, number, tense, mood, gender);
    }

    private static string? ReadStringOrNull(ref Utf8JsonReader reader, string name)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => GetString(ref reader),
            JsonTokenType.Null => null,
            _ => throw new JsonException($"{name} must be a string."),
        };
    }

    // The closed vocabularies are shared string instances, so tens of thousands of entries do not allocate a string per feature.
    private static string? ReadKnownOrNull(ref Utf8JsonReader reader, string[] known, string name)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            foreach (var candidate in known)
            {
                if (reader.ValueTextEquals(candidate))
                {
                    return candidate;
                }
            }

            return GetString(ref reader);
        }

        return reader.TokenType == JsonTokenType.Null ? null : throw new JsonException($"{name} must be a string.");
    }

    // Invalid UTF-8 inside a string surfaces from GetString() as an InvalidOperationException; the lexicon file is damaged, which is a JsonException.
    private static string GetString(ref Utf8JsonReader reader)
    {
        try
        {
            return reader.GetString()!;
        }
        catch (InvalidOperationException ex)
        {
            throw new JsonException("The lexicon is not valid UTF-8 text.", ex);
        }
    }
}
