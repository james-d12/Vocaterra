using System.Text.Json;

namespace Vocaterra.Nlp.ContentBuilder;

/// <summary>One LanguageTool match, reduced to what the check keeps and caches: the rule, its issue type, the message, the span and up to three suggestions.</summary>
/// <param name="Rule">The rule id, for example <c>AGREEMENT_DET_NOUN</c>.</param>
/// <param name="IssueType">The rule's issue type, for example <c>grammar</c>, <c>misspelling</c> or <c>style</c>.</param>
/// <param name="Message">The rule's message.</param>
/// <param name="Offset">The start of the span, in characters of the checked text.</param>
/// <param name="Length">The length of the span.</param>
/// <param name="Replacements">Up to three suggested replacements, in the server's order.</param>
internal sealed record LtMatch(string Rule, string IssueType, string Message, int Offset, int Length, IReadOnlyList<string> Replacements);

/// <summary>Raised when the LanguageTool server cannot be reached (nothing is listening, or the connection fails).</summary>
internal sealed class LanguageToolUnreachableException : Exception
{
    /// <summary>Creates the exception with the underlying message.</summary>
    public LanguageToolUnreachableException(string message, Exception? inner)
        : base(message, inner)
    {
    }
}

/// <summary>
/// A plain HTTP client for LanguageTool's <c>POST /v2/check</c>. No LanguageTool code is linked: the server is a separate local process started by
/// <c>scripts/languagetool.sh</c> at development time (D10). The client refuses a server whose version is not <see cref="PinnedVersion"/>, because the
/// cache is keyed by that version.
/// </summary>
internal sealed class LanguageToolClient : IDisposable
{
    /// <summary>The LanguageTool version the cache and <c>tools/manifest.json</c> pin.</summary>
    public const string PinnedVersion = "6.6";

    /// <summary>The language every check uses: plain Spanish (the stand-in for neutral Latin American Spanish, D10).</summary>
    public const string Language = "es";

    /// <summary>The mother tongue passed to the server (English, the learner's language in the game).</summary>
    public const string MotherTongue = "en";

    private readonly HttpClient _http;
    private readonly Uri _checkUri;

    /// <summary>Creates a client for the server; a handler replaces the network (tests use a stub).</summary>
    public LanguageToolClient(string server, HttpMessageHandler? handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(server);
        var baseUri = new Uri(server.EndsWith('/') ? server : server + "/", UriKind.Absolute);
        _checkUri = new Uri(baseUri, "v2/check");
        Server = server;
        _http = handler is null ? new HttpClient { Timeout = TimeSpan.FromSeconds(60) } : new HttpClient(handler, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(60) };
    }

    /// <summary>The server address as the user gave it.</summary>
    public string Server { get; }

    /// <summary>The settings part of the cache key: the language, the level and the mother tongue.</summary>
    public static string SettingsFor(string level)
    {
        return "language=" + Language + ";level=" + level + ";motherTongue=" + MotherTongue;
    }

    /// <summary>Checks one text. Throws <see cref="LanguageToolUnreachableException"/> when no server answers.</summary>
    /// <returns>The server's version and the matches, in the server's order.</returns>
    public (string Version, IReadOnlyList<LtMatch> Matches) Check(string text, string level)
    {
        var form = new FormUrlEncodedContent(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["text"] = text,
            ["language"] = Language,
            ["level"] = level,
            ["motherTongue"] = MotherTongue,
        });
        string body;
        try
        {
            using var response = _http.PostAsync(_checkUri, form).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();
            body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException)
        {
            throw new LanguageToolUnreachableException($"no LanguageTool answer from {Server} ({ex.Message})", ex);
        }

        return Parse(body);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _http.Dispose();
    }

    /// <summary>Reads a <c>/v2/check</c> response: the software version and the matches.</summary>
    internal static (string Version, IReadOnlyList<LtMatch> Matches) Parse(string body)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var version = root.TryGetProperty("software", out var software) && software.TryGetProperty("version", out var v) ? v.GetString() ?? string.Empty : string.Empty;
        var matches = new List<LtMatch>();
        if (root.TryGetProperty("matches", out var list))
        {
            foreach (var match in list.EnumerateArray())
            {
                var rule = match.GetProperty("rule");
                var replacements = new List<string>();
                foreach (var replacement in match.GetProperty("replacements").EnumerateArray())
                {
                    if (replacements.Count < 3 && replacement.TryGetProperty("value", out var value) && value.GetString() is { } text)
                    {
                        replacements.Add(text);
                    }
                }

                matches.Add(new LtMatch(
                    rule.GetProperty("id").GetString() ?? string.Empty,
                    rule.TryGetProperty("issueType", out var issue) ? issue.GetString() ?? "uncategorized" : "uncategorized",
                    match.GetProperty("message").GetString() ?? string.Empty,
                    match.GetProperty("offset").GetInt32(),
                    match.GetProperty("length").GetInt32(),
                    replacements));
            }
        }

        return (version, matches);
    }
}
