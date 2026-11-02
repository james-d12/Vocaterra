using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Nlp.Pipeline.Tests;

/// <summary>
/// M70: the lines of the QA pass on the village (<c>docs/qa-reports/2026-11-01-language.md</c>) and of the owner's answers of 2026-11-01 (D-game-22, items 2 and 3),
/// through the whole pipeline at the game's prompts, as the CLI and the host grade them. Each failed before M70; the correct neighbours are tests as well.
/// </summary>
public class VillageQaLineTests
{
    private static PipelineResult Evaluate(string transcript, string promptId, ContentBundle? bundle = null)
    {
        var content = bundle ?? TestSupport.Bundle;
        var prompt = content.Prompts[promptId];
        return TestSupport.Pipeline(transcript, prompt.Mode, bundle: content).Evaluate(TestSupport.Clip(), prompt, TestSupport.Context(agentId: prompt.AgentId ?? "player"));
    }

    private static string Errors(Verdict verdict)
    {
        return string.Join(';', verdict.Errors.Select(static e => $"{e.Tag}|{e.Actual}|{e.Expected}"));
    }

    // ---- 1. A typed n for ñ (QA-language-7; D-game-22, item 2) ---------------------------------------------------------------------------------------------

    // Before M70: vocab.unknown on manana, correctness 0.6 at the lessons, and no intent at lesson-will-sell on the CLI.
    [Theory]
    [InlineData("manana compraré pan", "lesson-will-buy")]
    [InlineData("mañana compraré pan", "lesson-will-buy")]
    [InlineData("manana venderé fruta", "lesson-will-sell")]
    [InlineData("mañana venderé fruta", "lesson-will-sell")]
    public void ATypedNForNTildeIsNoErrorAndKeepsTheIntent(string transcript, string promptId)
    {
        var verdict = Evaluate(transcript, promptId).Verdict;

        Assert.Equal(string.Empty, Errors(verdict));
        Assert.Equal(1.0, verdict.Correctness);
        Assert.True(verdict.Communicated);
        Assert.Equal("state_action", verdict.Intent.Value);
    }

    [Fact]
    public void AWordOfItsOwnSpelledWithNIsGradedAsBefore()
    {
        Assert.Equal("vocab.unknown|anos|", Errors(Evaluate("tengo diez anos", "villager-open").Verdict));
    }
}
