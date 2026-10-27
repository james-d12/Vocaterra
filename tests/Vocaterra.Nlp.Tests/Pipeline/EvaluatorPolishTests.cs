using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Nlp.Pipeline.Tests;

/// <summary>
/// M17c evaluator polish, end to end: near misses on answer-only prompts are communicated and recast with the canonical word, a derived
/// vocab.wrong names its answer only for a one-answer set, an accentless word is recast with its accents, and a leading adverb is understood.
/// </summary>
public class EvaluatorPolishTests
{
    private static PromptSpec Prompt(string id)
    {
        return TestSupport.Bundle.Prompts[id];
    }

    private static PipelineResult Quiz(string transcript)
    {
        return TestSupport.Pipeline(transcript, RecognitionMode.Constrained)
            .Evaluate(TestSupport.Clip(), TestSupport.QuizPrompt, TestSupport.Context(state: "none", agentId: "player"));
    }

    [Theory]
    [InlineData("pietra", "piedra")]
    [InlineData("piedro", "piedra")]
    [InlineData("árbal", "árbol")]
    public void EveryNearMissOnAQuizIsCommunicatedAndRecastWithTheCanonicalWord(string transcript, string canonical)
    {
        var result = Quiz(transcript);

        Assert.True(result.Verdict.Communicated);
        Assert.Equal(ResolvedBy.Fuzzy, result.Verdict.ResolvedBy);
        var error = Assert.Single(result.Verdict.Errors);
        Assert.Equal((ErrorTags.VocabUnknown, canonical, transcript), (error.Tag, error.Expected, error.Actual));
        var recast = Assert.Single(result.Response.Lines, static line => line.IsRecast);
        Assert.Contains("«" + canonical + "»", recast.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(transcript, recast.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void AnInflectedNearMissIsCommunicatedAsAFuzzyMatchOnTheBudgetsEdge()
    {
        // piedras is one edit from piedra, which is the whole budget for a six-letter answer at Beginner. The lexicon knows the plural, so no
        // vocabulary error is raised, and the match is communicated with the confidence of its distance.
        var verdict = Quiz("piedras").Verdict;

        Assert.True(verdict.Communicated);
        Assert.Equal(ResolvedBy.Fuzzy, verdict.ResolvedBy);
        Assert.Empty(verdict.Errors);
        Assert.Equal(1.0 - (1.0 / 6.0), verdict.Confidence, 9);
    }

    [Fact]
    public void AVocabWrongOnAQuizWithSeveralAnswersHasNoExpectedWord()
    {
        var error = Assert.Single(Quiz("fuego").Verdict.Errors);

        Assert.Equal((ErrorTags.VocabWrong, string.Empty, "fuego"), (error.Tag, error.Expected, error.Actual));
    }

    [Fact]
    public void ALeadingAdverbBeforeAnOfferIsUnderstoodByTheRulesAndKeepsItsTense()
    {
        var result = TestSupport.Pipeline("ayer tengo dos espadas", RecognitionMode.Open)
            .Evaluate(TestSupport.Clip(), TestSupport.HldPrompt, TestSupport.Context());

        var verdict = result.Verdict;
        Assert.Equal("state_have", verdict.Intent.Value);
        Assert.Equal(ResolvedBy.Rules, verdict.ResolvedBy);
        Assert.True(verdict.Communicated);
        Assert.Equal("espada", verdict.Slots["item"]);
        var tense = Assert.Single(verdict.Errors, static error => error.Tag == ErrorTags.ConjTense);
        Assert.Equal("tuve", tense.Expected);
    }

    [Fact]
    public void ARecastRestoresTheAccentsTheLexiconGivesAWordThePlayerLeftOut()
    {
        var result = TestSupport.Pipeline("buenas dias", RecognitionMode.Open)
            .Evaluate(TestSupport.Clip(), Prompt(TestSupport.OpenPrompt), TestSupport.Context());

        var recast = Assert.Single(result.Response.Lines, static line => line.IsRecast);
        Assert.Contains("buenos días", recast.Text, StringComparison.OrdinalIgnoreCase);
    }
}
