using System.Text.Json;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Determinism;

namespace Vocaterra.Host.Tests;

/// <summary>Smoke tests: the two demo utterances through the host's evaluation path on the game bundle, and the bundle's hash.</summary>
public sealed class HostSmokeTests
{
    private static readonly StoredTimestamp Now = new(1_760_000_000);

    [Fact]
    public void OfferingTwoSwordsGivesTheTwoDemoErrors()
    {
        var session = new ConversationSession(GameContent.Bundle);

        var result = session.Evaluate("yo tiene dos espada", "offer-items-open", Now);

        var tags = result.Verdict.Errors.Select(static error => error.Tag).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "conj.person", "noun.number" }, tags);
        Assert.Equal(0.5, result.Verdict.Correctness, 9);
    }

    [Fact]
    public void AskingThePriceOfTheSwordIsCorrect()
    {
        var session = new ConversationSession(GameContent.Bundle);

        var result = session.Evaluate("cuánto cuesta la espada", "ask-price-open", Now);

        Assert.Empty(result.Verdict.Errors);
        Assert.Equal(1.0, result.Verdict.Correctness, 9);
    }

    [Fact]
    public void TheBundleHashInTheManifestIsTheHashTheLoaderComputes()
    {
        var manifestPath = Path.Combine(GameContent.BundleDirectory(), ContentPaths.Manifest);
        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var declared = manifest.RootElement.GetProperty("bundleHash").GetString();

        Assert.Equal(declared, GameContent.Bundle.ContentHash);
    }

    [Fact]
    public void RepeatCountsStopAtIntMaxValueInsteadOfWrapping()
    {
        // The counts are "in a row" and an int: a count at int.MaxValue must stay there. The host's old rule added one with wrap-around and
        // turned it into a negative number.
        var atLimit = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["conj.person"] = int.MaxValue,
            ["noun.number"] = int.MaxValue,
        };
        var session = new ConversationSession(GameContent.Bundle, repeatCountsByTag: atLimit);

        var result = session.Evaluate("yo tiene dos espada", "offer-items-open", Now);

        Assert.Equal(new[] { "conj.person", "noun.number" }, result.Verdict.Errors.Select(static error => error.Tag).Order(StringComparer.Ordinal));
        Assert.Equal(int.MaxValue, session.Context.RepeatCountsByTag["conj.person"]);
        Assert.Equal(int.MaxValue, session.Context.RepeatCountsByTag["noun.number"]);
    }

    [Fact]
    public void TheConversationCarriesRepeatCountsAndLearnerAcrossUtterances()
    {
        var session = new ConversationSession(GameContent.Bundle);
        session.Evaluate("yo tiene dos espada", "offer-items-open", Now);

        var second = session.Evaluate("yo tiene dos espada", "offer-items-open", Now.AddSeconds(60));

        Assert.Equal(2, session.Context.RepeatCountsByTag["conj.person"]);
        Assert.Equal(2, session.Context.RepeatCountsByTag["noun.number"]);
        Assert.Equal(second.Decision.Learner, session.Context.Learner);

        // A correct utterance carries no error tags, so the counts start again: they count errors in a row.
        session.Evaluate("cuánto cuesta la espada", "ask-price-open", Now.AddSeconds(120));
        Assert.Empty(session.Context.RepeatCountsByTag);
    }
}
