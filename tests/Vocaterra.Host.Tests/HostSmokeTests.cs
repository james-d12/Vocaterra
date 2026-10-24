using System.Text.Json;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Pipeline;

namespace Vocaterra.Host.Tests;

/// <summary>Smoke tests: the two demo utterances through the host's evaluation path on the game bundle, and the bundle's hash.</summary>
public sealed class HostSmokeTests
{
    private static readonly StoredTimestamp Now = new(1_760_000_000);

    // "yo quieres dos agua por favor": the wrong person of querer (conj.person) and a plural number with a singular noun (noun.number).
    private const string DemoLine = "yo quieres dos agua por favor";

    [Fact]
    public void AskingForTwoWatersWrongGivesTheTwoDemoErrors()
    {
        var session = new ConversationSession(GameContent.Bundle);

        var result = session.Evaluate(DemoLine, "villager-open", Now);

        var tags = result.Verdict.Errors.Select(static error => error.Tag).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "conj.person", "noun.number" }, tags);
        Assert.Equal(0.6, result.Verdict.Correctness, 9);
    }

    [Fact]
    public void TheHostsRepeatLadderCountsEachUtteranceOnce()
    {
        // The core adds an utterance's own repeats (M26a, CR-012) and the host carries the counts with RepeatCounts.Next, so the same
        // mistake is recast once, then hinted, and the hint stays a hint: the ladder is one step ahead of the old core's.
        var session = new ConversationSession(GameContent.Bundle);

        var kinds = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            kinds.Add(FeedbackKind(session.Evaluate(DemoLine, "villager-open", Now.AddSeconds(i * 10))));
        }

        Assert.Equal(new[] { "recast", "hint", "hint" }, kinds);
        Assert.Equal(3, session.Context.RepeatCountsByTag["conj.person"]);
    }

    private static string FeedbackKind(PipelineResult result)
    {
        var line = result.Response.Lines.FirstOrDefault(static l => l.IsRecast || l.LineId.StartsWith("feedback.", StringComparison.Ordinal));
        return line is null ? "none" : line.IsRecast ? "recast" : line.LineId.StartsWith("feedback.hint", StringComparison.Ordinal) ? "hint" : line.LineId;
    }

    [Fact]
    public void AskingWhereTheRiverIsIsCorrect()
    {
        var session = new ConversationSession(GameContent.Bundle);

        var result = session.Evaluate("¿dónde está el río?", "villager-open", Now);

        Assert.Equal("ask_directions", result.Verdict.Intent.Value);
        Assert.Empty(result.Verdict.Errors);
        Assert.Equal(1.0, result.Verdict.Correctness, 9);
    }

    [Fact]
    public void ThanksIsRecognisedAsTheGenericThankIntentThroughTheHost()
    {
        var session = new ConversationSession(GameContent.Bundle);

        var result = session.Evaluate("gracias", "villager-open", Now);

        Assert.Equal("thank", result.Verdict.Intent.Value);
        Assert.True(result.Verdict.Communicated);
        Assert.Empty(result.Verdict.Errors);
    }

    [Fact]
    public void TheBreadIsGivenOnlyAfterTheLearnerSaysYes()
    {
        // The shopkeeper: a request gives the water and offers bread (offering), "sí, por favor" gives the bread (done).
        var session = new ConversationSession(GameContent.Bundle);

        var asked = session.Evaluate("quiero agua, por favor", "stall-need", Now);
        Assert.Equal("request_item", asked.Verdict.Intent.Value);
        Assert.Equal("offering", asked.Decision.Agent?.Next.StateId);
        Assert.Equal(new[] { "stall.give_water" }, HostEventsOf(asked));

        var confirmed = session.Evaluate("sí, por favor", "stall-offer", Now.AddSeconds(20));
        Assert.Equal("confirm", confirmed.Verdict.Intent.Value);
        Assert.Equal("done", confirmed.Decision.Agent?.Next.StateId);
        Assert.Equal(new[] { "stall.give_bread" }, HostEventsOf(confirmed));
    }

    private static string[] HostEventsOf(PipelineResult result)
    {
        return result.Decision.Agent!.Actions.Where(static a => a.HostEvent is not null).Select(static a => a.HostEvent!).ToArray();
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

        var result = session.Evaluate(DemoLine, "villager-open", Now);

        Assert.Equal(new[] { "conj.person", "noun.number" }, result.Verdict.Errors.Select(static error => error.Tag).Order(StringComparer.Ordinal));
        Assert.Equal(int.MaxValue, session.Context.RepeatCountsByTag["conj.person"]);
        Assert.Equal(int.MaxValue, session.Context.RepeatCountsByTag["noun.number"]);
    }

    [Fact]
    public void TheConversationCarriesRepeatCountsAndLearnerAcrossUtterances()
    {
        var session = new ConversationSession(GameContent.Bundle);
        session.Evaluate(DemoLine, "villager-open", Now);

        var second = session.Evaluate(DemoLine, "villager-open", Now.AddSeconds(60));

        Assert.Equal(2, session.Context.RepeatCountsByTag["conj.person"]);
        Assert.Equal(2, session.Context.RepeatCountsByTag["noun.number"]);
        Assert.Equal(second.Decision.Learner, session.Context.Learner);

        // A correct utterance carries no error tags, so the counts start again: they count errors in a row.
        session.Evaluate("¿dónde está el río?", "villager-open", Now.AddSeconds(120));
        Assert.Empty(session.Context.RepeatCountsByTag);
    }
}
