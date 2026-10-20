using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Slg.Core.Determinism;

/// <summary>Canonical JSON: camelCase, declaration order, sorted maps, string enums, shortest round-trip numbers, no whitespace.</summary>
public static class CanonicalJson
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    /// <summary>Creates a fresh copy of the canonical options for callers that need to compose their own serializer settings.</summary>
    public static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            NumberHandling = JsonNumberHandling.Strict,
            IncludeFields = false,
            RespectNullableAnnotations = true,

            // Keeps Spanish accents and punctuation readable; output stays deterministic because the encoder is managed code.
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        options.Converters.Add(new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false));
        options.Converters.Add(new IntentIdJsonConverter());
        options.Converters.Add(new TextSpanJsonConverter());
        options.Converters.Add(new SlotMapJsonConverter());
        options.Converters.Add(new StringMapJsonConverter());
        return options;
    }

    /// <summary>Serializes a value to canonical JSON; NaN and Infinity throw.</summary>
    public static string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, Options);
    }

    /// <summary>Deserializes canonical JSON; a JSON null result throws <see cref="JsonException"/>.</summary>
    public static T Deserialize<T>(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return JsonSerializer.Deserialize<T>(json, Options)
            ?? throw new JsonException("JSON deserialized to null.");
    }

    /// <summary>Returns the deterministic hash of the canonical serialization of the value.</summary>
    public static string Hash<T>(T value)
    {
        return DeterministicHash.OfText(Serialize(value));
    }
}
