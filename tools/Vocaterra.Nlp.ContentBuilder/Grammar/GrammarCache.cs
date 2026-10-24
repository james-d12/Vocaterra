using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vocaterra.Nlp.ContentBuilder;

/// <summary>One cached check: the key, the settings and text it was made for (kept so a reviewer can read the file), and the server's matches.</summary>
internal sealed record GrammarCacheEntry(string Key, string Settings, string Text, IReadOnlyList<LtMatch> Matches);

/// <summary>
/// The committed result cache of the grammar check. An entry is keyed by the SHA-256 of the LanguageTool version, the settings and the text, so a
/// result is reused only for the same server version and settings. The file lists its entries sorted by key, with stable JSON, so a change shows as a
/// small diff. <c>--offline</c> reads it and never calls a server.
/// </summary>
internal sealed class GrammarCache
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly Dictionary<string, GrammarCacheEntry> _entries = new(StringComparer.Ordinal);
    private readonly HashSet<string> _used = new(StringComparer.Ordinal);

    /// <summary>Creates an empty cache.</summary>
    public GrammarCache()
    {
    }

    /// <summary>The number of entries.</summary>
    public int Count => _entries.Count;

    /// <summary>Entries added by this run.</summary>
    public int Added { get; private set; }

    /// <summary>Entries not used by this run that <see cref="Prune"/> removed.</summary>
    public int Pruned { get; private set; }

    /// <summary>
    /// The cache key of a check: the lower-case hex SHA-256 of the version, the settings and the text, separated by line feeds (no other
    /// normalisation, so the key names exactly what was sent).
    /// </summary>
    public static string KeyFor(string version, string settings, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(version + "\n" + settings + "\n" + text);
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    /// <summary>Reads the cache at the path; a missing file is an empty cache.</summary>
    public static GrammarCache Load(string path)
    {
        var cache = new GrammarCache();
        if (!File.Exists(path))
        {
            return cache;
        }

        var file = JsonSerializer.Deserialize<CacheFile>(File.ReadAllText(path, Encoding.UTF8), Options)
            ?? throw new GrammarInputException($"{path}: the cache is empty.");
        if (file.SchemaVersion != 1)
        {
            throw new GrammarInputException($"{path}: unsupported cache schema {file.SchemaVersion}.");
        }

        foreach (var entry in file.Entries ?? [])
        {
            cache._entries[entry.Key] = entry;
        }

        return cache;
    }

    /// <summary>Looks up an entry by key and marks it as used by this run.</summary>
    public bool TryGet(string key, out GrammarCacheEntry entry)
    {
        if (_entries.TryGetValue(key, out var found))
        {
            _used.Add(key);
            entry = found;
            return true;
        }

        entry = null!;
        return false;
    }

    /// <summary>Adds the result of a check made by this run.</summary>
    public void Add(GrammarCacheEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!_entries.ContainsKey(entry.Key))
        {
            Added++;
        }

        _entries[entry.Key] = entry;
        _used.Add(entry.Key);
    }

    /// <summary>Removes every entry this run did not use, so the file holds exactly the strings that were checked.</summary>
    public void Prune()
    {
        foreach (var key in _entries.Keys.Where(key => !_used.Contains(key)).ToList())
        {
            _entries.Remove(key);
            Pruned++;
        }
    }

    /// <summary>The file text: a version header and the entries sorted by key, with a final line feed.</summary>
    public string ToJson(string version)
    {
        var file = new CacheFile(1, version, _entries.Values.OrderBy(static entry => entry.Key, StringComparer.Ordinal).ToList());
        return JsonSerializer.Serialize(file, Options).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    /// <summary>Writes the cache to the path when its text differs from the file, creating the folder. Returns true when it wrote.</summary>
    public bool Save(string path, string version)
    {
        var text = ToJson(version);
        if (File.Exists(path) && File.ReadAllText(path, Encoding.UTF8) == text)
        {
            return false;
        }

        var folder = Path.GetDirectoryName(Path.GetFullPath(path));
        if (folder is not null)
        {
            Directory.CreateDirectory(folder);
        }

        File.WriteAllText(path, text, new UTF8Encoding(false));
        return true;
    }

    private sealed record CacheFile(
        int SchemaVersion,
        string LanguageTool,
        [property: JsonPropertyName("entries")] IReadOnlyList<GrammarCacheEntry>? Entries);
}
