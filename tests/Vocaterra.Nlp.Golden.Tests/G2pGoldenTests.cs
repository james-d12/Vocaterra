using System.Text.Json;
using Vocaterra.Nlp.Phonology;

namespace Vocaterra.Nlp.Golden.Tests;

/// <summary>Checks the Spanish G2P against the hand-authored golden set in <c>Data/g2p-golden.jsonl</c>.</summary>
public class G2pGoldenTests
{
    private const int MinimumEntries = 300;
    private const int MinimumPhrases = 30;

    private static readonly string GoldenPath = Path.Combine(AppContext.BaseDirectory, "Data", "g2p-golden.jsonl");

    [Fact]
    public void GoldenSetIsLargeEnoughAndWellFormed()
    {
        var entries = LoadEntries();

        Assert.True(entries.Count >= MinimumEntries, $"expected at least {MinimumEntries} golden entries, found {entries.Count}");
        Assert.All(entries, entry =>
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.Text));
            Assert.False(string.IsNullOrWhiteSpace(entry.Ipa));
            Assert.Contains(entry.Dialect, new[] { "la", "es" });
        });

        var keys = entries.Select(static e => (e.Text, e.Dialect)).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());

        var phrases = entries.Where(static e => e.Dialect == "la" && e.Text.Contains(' ', StringComparison.Ordinal)).Count();
        Assert.True(phrases >= MinimumPhrases, $"expected at least {MinimumPhrases} multi-word phrases, found {phrases}");
    }

    [Fact]
    public void EveryGoldenLineMatchesTheG2p()
    {
        var entries = LoadEntries();
        var failures = new List<string>();
        foreach (var entry in entries)
        {
            var dialect = entry.Dialect == "es" ? DialectConfig.CastilianStandard : DialectConfig.LatinAmericanNeutral;
            var actual = SpanishG2p.Phonemize(entry.Text, dialect).ToIpaString();
            if (!string.Equals(actual, entry.Ipa, StringComparison.Ordinal))
            {
                failures.Add($"[{entry.Dialect}] '{entry.Text}': expected '{entry.Ipa}' but got '{actual}'");
            }
        }

        Assert.True(failures.Count == 0, $"{failures.Count} of {entries.Count} golden lines differ:{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
    }

    private static List<GoldenEntry> LoadEntries()
    {
        var entries = new List<GoldenEntry>();
        foreach (var line in File.ReadLines(GoldenPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            entries.Add(new GoldenEntry(
                root.GetProperty("text").GetString() ?? string.Empty,
                root.GetProperty("dialect").GetString() ?? string.Empty,
                root.GetProperty("ipa").GetString() ?? string.Empty));
        }

        return entries;
    }

    private sealed record GoldenEntry(string Text, string Dialect, string Ipa);
}
