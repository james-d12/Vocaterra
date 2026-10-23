using System.Text.Json;
using Vocaterra.Nlp.Phonology;

namespace Vocaterra.Nlp.Pronunciation;

/// <summary>
/// Maps the labels of the espeak-style phoneme vocabulary to the <c>Vocaterra.Nlp.Phonology</c> inventory using the committed table
/// <c>Data/espeak-to-vocaterra.json</c>. A label already in the inventory maps to itself, special tokens follow the table (<c>&lt;pad&gt;</c> becomes the
/// CTC blank), and any other label is kept verbatim and counts as unmapped.
/// </summary>
public sealed class EspeakPhoneMap
{
    private const string ResourceName = "Vocaterra.Nlp.Pronunciation.Data.espeak-to-vocaterra.json";

    private static readonly Lazy<EspeakPhoneMap> DefaultInstance = new(LoadEmbedded);

    private readonly SortedDictionary<string, string> specials;
    private readonly SortedDictionary<string, string> map;

    private EspeakPhoneMap(SortedDictionary<string, string> specials, SortedDictionary<string, string> map)
    {
        this.specials = specials;
        this.map = map;
    }

    /// <summary>The table shipped in the assembly.</summary>
    public static EspeakPhoneMap Default => DefaultInstance.Value;

    /// <summary>The mapped labels (not the identity cases), sorted by ordinal key order.</summary>
    public IReadOnlyDictionary<string, string> Entries => map;

    /// <summary>The special tokens and what they map to, sorted by ordinal key order.</summary>
    public IReadOnlyDictionary<string, string> Specials => specials;

    /// <summary>Parses a mapping document with <c>specials</c> and <c>map</c> objects.</summary>
    public static EspeakPhoneMap Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        return new EspeakPhoneMap(ReadObject(root, "specials"), ReadObject(root, "map"));
    }

    /// <summary>Maps one vocabulary label; a label with no rule is returned unchanged.</summary>
    public string Map(string label)
    {
        ArgumentNullException.ThrowIfNull(label);
        if (specials.TryGetValue(label, out var special))
        {
            return special;
        }

        if (PhoneInventory.IsKnown(label))
        {
            return label;
        }

        return map.TryGetValue(label, out var mapped) ? mapped : label;
    }

    /// <summary>True when the label is a special token, is in the inventory, or has a table entry.</summary>
    public bool IsMapped(string label)
    {
        ArgumentNullException.ThrowIfNull(label);
        return specials.ContainsKey(label) || PhoneInventory.IsKnown(label) || map.ContainsKey(label);
    }

    private static EspeakPhoneMap LoadEmbedded()
    {
        var assembly = typeof(EspeakPhoneMap).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' is missing.");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }

    private static SortedDictionary<string, string> ReadObject(JsonElement root, string name)
    {
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
        if (!root.TryGetProperty(name, out var element))
        {
            throw new JsonException($"The mapping document has no '{name}' object.");
        }

        foreach (var property in element.EnumerateObject())
        {
            var value = property.Value.GetString() ?? throw new JsonException($"'{property.Name}' has no string value.");
            result.Add(property.Name, value);
        }

        return result;
    }
}
