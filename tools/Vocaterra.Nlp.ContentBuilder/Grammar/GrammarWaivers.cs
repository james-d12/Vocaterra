using System.Text;
using System.Text.Json;

namespace Vocaterra.Nlp.ContentBuilder;

/// <summary>
/// One accepted match. It names the LanguageTool rule and the string it is accepted for, by string <c>id</c> or by exact authored <c>text</c>, and
/// it must say why (<see cref="Reason"/>, mandatory: a waiver without a reason is refused).
/// </summary>
internal sealed record GrammarWaiver(string Rule, string? Id, string? Text, string Reason)
{
    /// <summary>True when the waiver covers the rule on the item.</summary>
    public bool Covers(string rule, GrammarItem item)
    {
        return string.Equals(Rule, rule, StringComparison.Ordinal)
            && ((Id is not null && string.Equals(Id, item.Id, StringComparison.Ordinal))
                || (Text is not null && string.Equals(Text, item.Original, StringComparison.Ordinal)));
    }

    /// <summary>The waiver as the file names it, for messages: the rule and the id or the quoted text.</summary>
    public string Describe()
    {
        return Id is not null ? Rule + " " + Id : Rule + " \"" + Text + "\"";
    }
}

/// <summary>
/// The waiver file of the grammar check: <c>{"schemaVersion":1,"waivers":[{"rule":...,"id":...,"reason":...}]}</c>. A waiver that matches no finding
/// is reported as unused, so a fixed string does not keep a stale waiver. The file is beside the cache, not inside the bundle, so the bundle's hash
/// does not change when a waiver is added.
/// </summary>
internal sealed class GrammarWaivers
{
    private readonly List<GrammarWaiver> _waivers;
    private readonly HashSet<int> _used = [];

    private GrammarWaivers(List<GrammarWaiver> waivers)
    {
        _waivers = waivers;
    }

    /// <summary>The waivers in file order.</summary>
    public IReadOnlyList<GrammarWaiver> All => _waivers;

    /// <summary>An empty set of waivers.</summary>
    public static GrammarWaivers Empty()
    {
        return new GrammarWaivers([]);
    }

    /// <summary>
    /// Reads the waiver file. A missing file is an empty set when <paramref name="mustExist"/> is false (the default file) and an error when it is true
    /// (a path the user named). A waiver with no rule, no id or text, or no reason is refused.
    /// </summary>
    public static GrammarWaivers Load(string path, bool mustExist)
    {
        if (!File.Exists(path))
        {
            return mustExist ? throw new GrammarInputException($"waiver file not found: {path}") : Empty();
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
        }
        catch (JsonException ex)
        {
            throw new GrammarInputException($"{path}: not JSON ({ex.Message})");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("schemaVersion", out var schema) || schema.GetInt32() != 1)
            {
                throw new GrammarInputException($"{path}: the waiver file must be an object with \"schemaVersion\": 1.");
            }

            var waivers = new List<GrammarWaiver>();
            if (root.TryGetProperty("waivers", out var list))
            {
                var index = 0;
                foreach (var entry in list.EnumerateArray())
                {
                    index++;
                    waivers.Add(Parse(entry, $"{path}: waiver {index}"));
                }
            }

            return new GrammarWaivers(waivers);
        }
    }

    /// <summary>Returns the first waiver that covers the rule on the item, marking it used; null when none does.</summary>
    public GrammarWaiver? Find(string rule, GrammarItem item)
    {
        for (var i = 0; i < _waivers.Count; i++)
        {
            if (_waivers[i].Covers(rule, item))
            {
                _used.Add(i);
                return _waivers[i];
            }
        }

        return null;
    }

    /// <summary>The waivers no finding used this run, in file order.</summary>
    public IEnumerable<GrammarWaiver> Unused()
    {
        return _waivers.Where((_, i) => !_used.Contains(i));
    }

    private static GrammarWaiver Parse(JsonElement entry, string where)
    {
        if (entry.ValueKind != JsonValueKind.Object)
        {
            throw new GrammarInputException($"{where} must be an object.");
        }

        var rule = Optional(entry, "rule");
        var id = Optional(entry, "id");
        var text = Optional(entry, "text");
        var reason = Optional(entry, "reason");
        if (rule is null)
        {
            throw new GrammarInputException($"{where} needs a \"rule\" (a LanguageTool rule id such as PUNTOS_SUSPENSIVOS).");
        }

        if ((id is null) == (text is null))
        {
            throw new GrammarInputException($"{where} needs exactly one of \"id\" (a string id) or \"text\" (the exact authored text).");
        }

        if (reason is null || reason.Trim().Length == 0)
        {
            throw new GrammarInputException($"{where} ({rule}) has no reason; every waiver needs a \"reason\" saying why the match is accepted.");
        }

        return new GrammarWaiver(rule, id, text, reason);
    }

    private static string? Optional(JsonElement entry, string name)
    {
        if (!entry.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : throw new GrammarInputException($"\"{name}\" must be a string.");
    }
}
