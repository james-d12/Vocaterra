using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Nlp.Pipeline.Tests;

/// <summary>
/// M75, the near-miss rule (the owner, 2026-11-02, D-game-22: "a near miss must name its word"), through the whole pipeline at the game's prompts as the CLI grades
/// them, and through <see cref="TextGrader"/>, which the content lint and a host's typed input use. A line that matched no answer keeps the derived
/// <c>vocab.wrong</c> only when it misses the nearest answer phrase by one word of a slip pair, and the tag names the answer's word. Before M75 (M67's rule: within
/// the fuzzy budget for the line's own length) each correct line below scored 0.4 with an empty expected word while the agent praised it, and the kept tag of
/// <c>cuál es esto</c> sat on <c>es</c> with an empty expected word.
/// </summary>
public class NearMissLineTests
{
    [Fact]
    public void CuálForQuéKeepsItsTagNowNamingQué()
    {
        // lesson-what asks ¿Qué es esto?; cuál is a question word for the answer's question word.
        var result = Evaluate("cuál es esto", "lesson-what");

        var verdict = result.Verdict;
        Assert.Equal(("ask_what", true), (verdict.Intent.Value, verdict.Communicated));
        Assert.Equal(new ErrorTag(ErrorTags.VocabWrong, new TextSpan(0, 4), "qué", "cuál"), Assert.Single(verdict.Errors));
        Assert.Equal(0.4, verdict.Correctness);
    }

    // M78 (M75's known-not-fixed): the recast is built from the verdict's errors too, so it names the answer's word where the analysis has no error of its own.
    [Fact]
    public void TheRecastOfANearMissNamesTheAnswersWord()
    {
        var result = Evaluate("cuál es esto", "lesson-what");

        var recast = Assert.Single(result.Response.Lines, static line => line.IsRecast);
        Assert.Contains("«Qué es esto»", recast.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("uál", recast.Text, StringComparison.Ordinal);
        Assert.Equal("qué", Assert.Single(result.Verdict.Errors).Expected);
    }

    [Theory]
    [InlineData("smith-open", "quiero hacer un hacha", "state_want")] // a word added to an answer
    [InlineData("lesson-will-go", "pasado mañana iré al río con Mar", "state_action")] // a word added to the answer mañana iré al río con Mar
    public void ACorrectLineAKeywordRuleUnderstoodLosesTheTagSoThePraiseAndTheScoreAgree(string promptId, string transcript, string intent)
    {
        var result = Evaluate(transcript, promptId);

        var verdict = result.Verdict;
        Assert.Equal((intent, true), (verdict.Intent.Value, verdict.Communicated));
        Assert.Empty(verdict.Errors);
        Assert.Equal(1.0, verdict.Correctness);
        Assert.Empty(result.Decision.Feedback.Items);
    }

    [Fact]
    public void RápidoPorFavorIsNoNearMissOfAyudaPorFavor()
    {
        // One word from the alias ayuda por favor at lesson-had, but rápido and ayuda are content words far apart. It resolved no intent the step takes, so it is
        // not communicated; with no word to name it has no error either (M72's rule for a line the prompt does not take).
        var verdict = Evaluate("rápido por favor", "lesson-had").Verdict;

        Assert.Equal((IntentId.Unknown, false), (verdict.Intent, verdict.Communicated));
        Assert.Empty(verdict.Errors);
        Assert.Equal(1.0, verdict.Correctness);
    }

    [Theory]
    [InlineData("cuál es esto", "lesson-what")]
    [InlineData("quiero hacer un hacha", "smith-open")]
    [InlineData("pasado mañana iré al río con Mar", "lesson-will-go")]
    [InlineData("rápido por favor", "lesson-had")]
    [InlineData("quiero dos pam", "baker-open")]
    public void TheTextGraderGivesThePipelinesErrors(string transcript, string promptId)
    {
        var prompt = TestSupport.Bundle.Prompts[promptId];

        Assert.Equal(Evaluate(transcript, promptId).Verdict.Errors, new TextGrader(TestSupport.Bundle).Grade(transcript, prompt).Errors);
    }

    // The owner's spot checks (the brief's rules): natural, correct lines at the NPCs' prompts carry no vocab.wrong, and a true slip keeps its word.
    [Theory]
    [InlineData("smith-open", "me llamo domingo")]
    [InlineData("farmer-open", "me llamo león")]
    [InlineData("baker-open", "hola luz quiero pan")]
    [InlineData("baker-open", "buenas tardes luz")]
    [InlineData("smith-open", "buenos días domingo")]
    [InlineData("smith-open", "¿cómo está usted?")]
    [InlineData("farmer-open", "¿cómo están ustedes?")]
    [InlineData("villager-open", "¿cómo estás?")]
    [InlineData("smith-open", "hace frío")]
    [InlineData("lesson-ate", "no quise")]
    public void ANaturalCorrectLineHasNoVocabWrong(string promptId, string transcript)
    {
        Assert.DoesNotContain(Evaluate(transcript, promptId).Verdict.Errors, static e => e.Tag == ErrorTags.VocabWrong);
    }

    private static PipelineResult Evaluate(string transcript, string promptId)
    {
        var prompt = TestSupport.Bundle.Prompts[promptId];
        return TestSupport.Pipeline(transcript, prompt.Mode).Evaluate(TestSupport.Clip(), prompt, TestSupport.Context(agentId: prompt.AgentId ?? "player"));
    }
}
