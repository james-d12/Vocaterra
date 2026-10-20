using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vocaterra.Nlp.Response.Templates;

/// <summary>Reads and writes the catalog JSON with ordinal-sorted intent keys and duplicate detection.</summary>
internal sealed class DialogueCatalogJsonConverter : JsonConverter<DialogueCatalog>
{
    public override DialogueCatalog Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("A dialogue catalog must be a JSON object.");
        }

        List<KeyValuePair<string, VariantPool>>? pools = null;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return new DialogueCatalog(pools ?? throw new JsonException("Dialogue catalog is missing 'pools'."));
            }

            var name = reader.GetString();
            if (!string.Equals(name, "pools", StringComparison.Ordinal))
            {
                throw new JsonException($"Unexpected dialogue catalog property '{name}'.");
            }

            reader.Read();
            pools = ReadPools(ref reader, options);
        }

        throw new JsonException("Unterminated dialogue catalog.");
    }

    public override void Write(Utf8JsonWriter writer, DialogueCatalog value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("pools");
        writer.WriteStartObject();
        foreach (var pair in value.Pools)
        {
            writer.WritePropertyName(pair.Key);
            JsonSerializer.Serialize(writer, pair.Value, options);
        }

        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static List<KeyValuePair<string, VariantPool>> ReadPools(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("'pools' must be a JSON object.");
        }

        var pools = new List<KeyValuePair<string, VariantPool>>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return pools;
            }

            var intent = reader.GetString()!;
            if (!seen.Add(intent))
            {
                throw new JsonException($"Duplicate dialogue intent '{intent}'.");
            }

            reader.Read();
            var pool = JsonSerializer.Deserialize<VariantPool>(ref reader, options)
                ?? throw new JsonException($"Pool of '{intent}' is null.");
            pools.Add(new KeyValuePair<string, VariantPool>(intent, pool));
        }

        throw new JsonException("Unterminated 'pools' object.");
    }
}
