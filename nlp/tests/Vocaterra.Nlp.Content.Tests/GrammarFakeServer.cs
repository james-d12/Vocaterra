using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Vocaterra.Nlp.Content.Tests;

/// <summary>One match the fake server reports: a rule, its issue type, the span and the suggestions.</summary>
/// <param name="Rule">The LanguageTool rule id.</param>
/// <param name="IssueType">The rule's issue type.</param>
/// <param name="Offset">The start of the span.</param>
/// <param name="Length">The length of the span.</param>
/// <param name="Replacements">The suggestions.</param>
internal sealed record FakeMatch(string Rule, string IssueType, int Offset, int Length, params string[] Replacements);

/// <summary>
/// A stand-in for the LanguageTool server, as an <see cref="HttpMessageHandler"/>: no Java and no port. It answers <c>POST /v2/check</c> with the
/// matches <see cref="Rules"/> gives for the text, counts the requests, and can be taken "down" (every request fails to connect).
/// </summary>
internal sealed class FakeLanguageTool : HttpMessageHandler
{
    /// <summary>The number of check requests received (the probe included).</summary>
    public int Requests { get; private set; }

    /// <summary>When true, every request fails as a refused connection (no server running).</summary>
    public bool Down { get; set; }

    /// <summary>The software version the fake reports.</summary>
    public string Version { get; set; } = "6.6";

    /// <summary>The matches for a text.</summary>
    public Func<string, IReadOnlyList<FakeMatch>> Rules { get; set; } = static _ => [];

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (Down)
        {
            return Task.FromException<HttpResponseMessage>(new HttpRequestException("connection refused (test)"));
        }

        Requests++;
        var body = request.Content!.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
        var text = Form(body)["text"];
        var matches = Rules(text).Select(match => new
        {
            message = match.Rule + " test message",
            replacements = match.Replacements.Select(value => new { value }).ToArray(),
            offset = match.Offset,
            length = match.Length,
            rule = new { id = match.Rule, issueType = match.IssueType },
        }).ToArray();
        var json = JsonSerializer.Serialize(new { software = new { name = "LanguageTool", version = Version }, matches });
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
    }

    private static Dictionary<string, string> Form(string body)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in body.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pieces = part.Split('=', 2);
            fields[Uri.UnescapeDataString(pieces[0].Replace('+', ' '))] = Uri.UnescapeDataString(pieces[1].Replace('+', ' '));
        }

        return fields;
    }
}
