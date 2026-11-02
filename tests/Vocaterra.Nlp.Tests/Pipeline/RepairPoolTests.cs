using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Decision.Feedback;
using Vocaterra.Nlp.Language.Intent;
using Vocaterra.Nlp.Language.Packs;

namespace Vocaterra.Nlp.Pipeline.Tests;

/// <summary>
/// The repair lines (M33): the bundle's reserved pools <c>repair.ask_again</c> and <c>repair.slow_repeat</c> override the language pack's fallback
/// lines, and a bundle without them keeps the fallback, so <c>content/game-v1</c> speaks what it always spoke. The bundles are edited copies in memory.
/// </summary>
public class RepairPoolTests
{
    private const string PoolAskAgain = "¿Perdón, puedes repetirlo?";
    private const string PoolSlowRepeat = "Otra vez, despacito.";

    private static readonly ContentBundle WithPools = FixtureBundle.Create(pools =>
    {
        TwoPhaseTests.SetVariants(pools, "repair.ask_again", PoolAskAgain);
        TwoPhaseTests.SetVariants(pools, "repair.slow_repeat", PoolSlowRepeat);
    });

    private static PipelineResult Miss(ContentBundle bundle, int repeats)
    {
        var pipeline = TestSupport.Pipeline("xyzzy plugh", RecognitionMode.Constrained, bundle: bundle);
        var counts = new Dictionary<string, int> { [FeedbackPolicy.NotCommunicatedTag] = repeats };
        return pipeline.Evaluate(TestSupport.Clip(), TestSupport.QuizPrompt, TestSupport.Context(repeats: counts));
    }

    [Fact]
    public void TheAskAgainPoolOverridesThePackFallback()
    {
        var result = Miss(WithPools, repeats: 0);

        Assert.Equal(RepairKind.AskAgain, result.Repair!.Kind);
        Assert.Equal(PoolAskAgain, result.Repair.AgentLine);
    }

    [Fact]
    public void TheSlowRepeatPoolOverridesThePackFallback()
    {
        var result = Miss(WithPools, repeats: 1);

        Assert.Equal(RepairKind.SlowRepeat, result.Repair!.Kind);
        Assert.Equal(PoolSlowRepeat, result.Repair.AgentLine);
    }

    [Fact]
    public void ABundleWithoutThePoolsSpeaksThePackFallback()
    {
        var ask = Miss(TestSupport.Bundle, repeats: 0);
        var slow = Miss(TestSupport.Bundle, repeats: 1);

        Assert.Equal(SpanishLanguagePack.Instance.RepairFallback.AskAgain, ask.Repair!.AgentLine);
        Assert.Equal(SpanishLanguagePack.Instance.RepairFallback.SlowRepeat, slow.Repair!.AgentLine);
        Assert.Equal(SpanishRepairLines.AskAgainLine, ask.Repair.AgentLine);
        Assert.Equal(SpanishRepairLines.SlowRepeatLine, slow.Repair.AgentLine);
    }

    [Fact]
    public void ThePoolOfAnotherAttemptDoesNotChangeTheLine()
    {
        // Only the repair.ask_again pool speaks at the first attempt; the slow repetition keeps its fallback there.
        var askOnly = FixtureBundle.Create(pools => TwoPhaseTests.SetVariants(pools, "repair.ask_again", PoolAskAgain));

        Assert.Equal(PoolAskAgain, Miss(askOnly, repeats: 0).Repair!.AgentLine);
        Assert.Equal(SpanishRepairLines.SlowRepeatLine, Miss(askOnly, repeats: 1).Repair!.AgentLine);
    }

    [Fact]
    public void TheRepairPoolsAreReservedSoTheLintDoesNotCallThemUnused()
    {
        Assert.True(Vocaterra.Nlp.Response.Templates.ReservedPools.IsReserved("repair.ask_again"));
        Assert.True(Vocaterra.Nlp.Response.Templates.ReservedPools.IsReserved("repair.slow_repeat"));
        Assert.False(Vocaterra.Nlp.Response.Templates.ReservedPools.IsReserved("repair.give_up"));
    }

    [Fact]
    public void TheLinesOfTheStrategyAreTheGivenOnes()
    {
        var prompt = TestSupport.QuizPrompt;
        var lines = new RepairLines("A", "B", "C");

        Assert.Equal("A", RepairStrategy.Next(1, prompt, [], [], lines).AgentLine);
        Assert.Equal("B", RepairStrategy.Next(2, prompt, [], [], lines).AgentLine);
        Assert.Equal("C", RepairStrategy.Next(4, prompt, [], [], lines).AgentLine);
    }
}
