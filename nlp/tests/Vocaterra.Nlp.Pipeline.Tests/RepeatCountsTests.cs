using System.Collections.Immutable;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Decision.Feedback;
using Vocaterra.Nlp.Decision.Learning;

namespace Vocaterra.Nlp.Pipeline.Tests;

/// <summary>Tests for <see cref="RepeatCounts"/> and <see cref="ConversationContext.Next"/>: the "in a row" rule the host used to re-implement.</summary>
public class RepeatCountsTests
{
    private static Verdict Make(bool communicated, params string[] tags)
    {
        var errors = tags.Select(static tag => new ErrorTag(tag, new TextSpan(0, 1), "a", "b")).ToArray();
        return new Verdict(new IntentId("offer_item"), SlotMap.Empty, communicated, 0.5, errors, [], 1.0, ResolvedBy.Grammar, VersionStamp.Empty);
    }

    private static ImmutableSortedDictionary<string, int> Counts(params (string Tag, int Count)[] pairs)
    {
        return ImmutableSortedDictionary.CreateRange(StringComparer.Ordinal, pairs.Select(static pair => new KeyValuePair<string, int>(pair.Tag, pair.Count)));
    }

    [Fact]
    public void ATagInTheVerdictCountsOneMore()
    {
        var next = RepeatCounts.Next(Counts(("conj.person", 1), ("noun.number", 2)), Make(true, "conj.person"));

        Assert.Equal(Counts(("conj.person", 2)), next);
    }

    [Fact]
    public void ATagTheVerdictDoesNotCarryStartsAgain()
    {
        var next = RepeatCounts.Next(Counts(("noun.number", 4)), Make(true));

        Assert.Empty(next);
    }

    [Fact]
    public void ATagSeenForTheFirstTimeCountsOne_AndARepeatedTagInOneVerdictCountsOnce()
    {
        var next = RepeatCounts.Next(Counts(), Make(true, "vocab.wrong", "vocab.wrong"));

        Assert.Equal(Counts(("vocab.wrong", 1)), next);
    }

    [Fact]
    public void NotCommunicatedCountsWhenThePlayerWasNotUnderstood()
    {
        Assert.Equal(Counts((FeedbackPolicy.NotCommunicatedTag, 1)), RepeatCounts.Next(Counts(), Make(false)));
        Assert.Equal(Counts((FeedbackPolicy.NotCommunicatedTag, 4)), RepeatCounts.Next(Counts((FeedbackPolicy.NotCommunicatedTag, 3)), Make(false)));
        Assert.Equal(
            Counts((FeedbackPolicy.NotCommunicatedTag, 1), ("conj.person", 1)),
            RepeatCounts.Next(Counts(), Make(false, "conj.person")));
    }

    [Fact]
    public void NotCommunicatedResetsWhenThePlayerIsUnderstood()
    {
        var next = RepeatCounts.Next(Counts((FeedbackPolicy.NotCommunicatedTag, 2), ("conj.person", 1)), Make(true, "conj.person"));

        Assert.Equal(Counts(("conj.person", 2)), next);
    }

    [Fact]
    public void CountsBelowOneAreAbsent_AndTheCountStopsAtTheLargestInteger()
    {
        var next = RepeatCounts.Next(Counts(("conj.person", 0), ("noun.number", -3), ("gender.agree", int.MaxValue)), Make(true, "conj.person", "noun.number", "gender.agree"));

        Assert.Equal(Counts(("conj.person", 1), ("gender.agree", int.MaxValue), ("noun.number", 1)), next);
    }

    [Fact]
    public void TheResultIsOrdinallySortedAndDoesNotDependOnInputOrder()
    {
        var verdictA = Make(false, "pron.r", "conj.person", "Zeta.x", "ser_estar");
        var verdictB = Make(false, "ser_estar", "Zeta.x", "conj.person", "pron.r");
        var before = new[] { ("pron.r", 1), ("conj.person", 2), (FeedbackPolicy.NotCommunicatedTag, 5), ("Zeta.x", 3) };
        var forward = Counts(before);
        var backward = Counts(before.Reverse().ToArray());

        var first = RepeatCounts.Next(forward, verdictA);
        var second = RepeatCounts.Next(backward, verdictB);

        var expectedKeys = first.Keys.Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(expectedKeys, first.Keys.ToArray());
        Assert.Equal(first.Keys.ToArray(), second.Keys.ToArray());
        Assert.Equal(first.Values.ToArray(), second.Values.ToArray());
        Assert.Equal(
            ["Zeta.x", "conj.person", FeedbackPolicy.NotCommunicatedTag, "pron.r", "ser_estar"],
            first.Keys.ToArray());
        Assert.Equal(CanonicalJson.Serialize(first), CanonicalJson.Serialize(second));
    }

    [Fact]
    public void NullArgumentsAreRefused()
    {
        Assert.Throws<ArgumentNullException>(() => RepeatCounts.Next(null!, Make(true)));
        Assert.Throws<ArgumentNullException>(() => RepeatCounts.Next(Counts(), null!));
    }

    [Fact]
    public void ContextNextCarriesTheDecisionIntoTheNextTurn()
    {
        var pipeline = TestSupport.Pipeline(TestSupport.WorkedExample);
        var context = TestSupport.Context();
        var prompt = TestSupport.Bundle.Prompts[TestSupport.OfferPrompt];
        var now = new StoredTimestamp(1_700_000_050);

        var first = pipeline.Evaluate(TestSupport.Clip(), prompt, context);
        var next = context.Next(first, now);

        Assert.Equal(context.WorldSeed, next.WorldSeed);
        Assert.Equal("blacksmith", next.AgentId);
        Assert.Equal(first.Decision.Agent!.Next, next.AgentState);
        Assert.Equal(now, next.Now);
        Assert.Equal(first.Decision.Learner, next.Learner);
        Assert.Equal(Counts(("conj.person", 1), ("noun.number", 1)), next.RepeatCountsByTag);
        Assert.Equal(ConversationContext.Create(context.WorldSeed, "blacksmith", first.Decision.Agent!.Next, now, first.Decision.Learner, Counts(("conj.person", 1), ("noun.number", 1))), next);
    }

    [Fact]
    public void ContextNextCountsRepeatsAcrossTurns()
    {
        var pipeline = TestSupport.Pipeline(TestSupport.WorkedExample);
        var prompt = TestSupport.Bundle.Prompts[TestSupport.OfferPrompt];
        var context = TestSupport.Context();

        var first = pipeline.Evaluate(TestSupport.Clip(), prompt, context);
        var afterFirst = context.Next(first, new StoredTimestamp(1_700_000_050));
        var second = pipeline.Evaluate(TestSupport.Clip(), prompt, afterFirst);
        var afterSecond = afterFirst.Next(second, new StoredTimestamp(1_700_000_100));

        Assert.Equal(["conj.person", "noun.number"], second.Verdict.Errors.Select(static e => e.Tag).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(Counts(("conj.person", 2), ("noun.number", 2)), afterSecond.RepeatCountsByTag);
    }

    [Fact]
    public void ContextNextKeepsTheStateOfAnAgentWithoutAStateMachine()
    {
        var pipeline = TestSupport.Pipeline("lanzar espada");
        var prompt = TestSupport.Bundle.Prompts["spell-cast-open"];
        var context = TestSupport.Context(state: "none", agentId: "player");

        var result = pipeline.Evaluate(TestSupport.Clip(), prompt, context);
        var next = context.Next(result, new StoredTimestamp(1_700_000_050));

        Assert.Null(result.Decision.Agent);
        Assert.Equal(context.AgentState, next.AgentState);
        Assert.Equal("player", next.AgentId);
    }
}
