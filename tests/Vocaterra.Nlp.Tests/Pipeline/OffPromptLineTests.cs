using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Nlp.Pipeline.Tests;

/// <summary>
/// M72 (the owner, D-game-22, 2026-11-02, option 2): a correct line whose intent an NPC's constrained open prompt does not take was graded as an error,
/// <c>vocab.wrong</c> with an empty expected word and correctness 0.4, and said nothing of what it meant. Through the whole pipeline at the game's prompts, as the
/// CLI and the host grade them: such a line has no error, stays not communicated with an unknown intent, and carries the intent the catalog gives it in
/// <see cref="Verdict.OffPromptIntent"/>. The neighbours keep their verdicts: lines the prompt takes, a near miss of an answer, a quiz.
/// </summary>
public class OffPromptLineTests
{
    private static PipelineResult Evaluate(string transcript, string promptId)
    {
        var prompt = TestSupport.Bundle.Prompts[promptId];
        return TestSupport.Pipeline(transcript, prompt.Mode).Evaluate(TestSupport.Clip(), prompt, TestSupport.Context(agentId: prompt.AgentId ?? "player"));
    }

    // The orchestrator's table of 2026-11-02 and its kind: before M72 each constrained row was vocab.wrong (expected '') at 0.4, and none said what it meant.
    [Theory]
    [InlineData("la tienda está en el pueblo", "farmer-open", null)]
    [InlineData("tengo un hacha", "farmer-open", "state_have")]
    [InlineData("tengo dos manzanas", "fisher-open", "state_have")]
    [InlineData("me gusta el río", "smith-open", "state_like")]
    [InlineData("me gusta el río", "villager-open", "state_like")]
    [InlineData("el río es grande", "baker-open", "describe")]
    [InlineData("la panadería está cerca", "smith-open", null)]
    [InlineData("me gusta el pan", "fisher-open", "state_like")]
    [InlineData("dónde está la panadería", "farmer-open", "ask_directions")]
    [InlineData("Dos panes son caros", "farmer-open", null)]
    public void ACorrectLineThePromptDoesNotTakeHasNoErrorAndCarriesItsIntent(string transcript, string promptId, string? offPrompt)
    {
        var verdict = Evaluate(transcript, promptId).Verdict;

        Assert.Empty(verdict.Errors);
        Assert.Equal(1.0, verdict.Correctness);
        Assert.False(verdict.Communicated);
        Assert.Equal(IntentId.Unknown, verdict.Intent);
        Assert.Equal(offPrompt is null ? null : new IntentId(offPrompt), verdict.OffPromptIntent);
    }

    // The clean neighbours: the same kinds of line at a prompt that takes them are understood, with no off-prompt intent.
    [Theory]
    [InlineData("tengo un hacha", "smith-open", "state_have")]
    [InlineData("tengo un hacha", "baker-open", "state_have")]
    [InlineData("me gustan las manzanas", "farmer-open", "state_like")]
    [InlineData("dónde está la panadería", "fisher-open", "ask_directions")]
    [InlineData("el pan es bonito", "farmer-open", "describe")]
    [InlineData("quiero dos panes por favor", "baker-open", "request_item")]
    public void TheSameKindOfLineAtAPromptThatTakesItIsUnderstood(string transcript, string promptId, string intent)
    {
        var verdict = Evaluate(transcript, promptId).Verdict;

        Assert.Equal(new IntentId(intent), verdict.Intent);
        Assert.True(verdict.Communicated);
        Assert.Empty(verdict.Errors);
        Assert.Null(verdict.OffPromptIntent);
    }

    [Fact]
    public void ANearMissOfAnAnswerKeepsItsTag()
    {
        // cuál es esto for qué es esto (M67): a keyword rule understood it, and it misses the answer by one question word for another (M75), which the tag names.
        var verdict = Evaluate("cuál es esto", "lesson-what").Verdict;

        Assert.Equal(new ErrorTag(ErrorTags.VocabWrong, new TextSpan(0, 4), "qué", "cuál"), Assert.Single(verdict.Errors));
        Assert.Null(verdict.OffPromptIntent);
    }

    [Fact]
    public void AQuizStillNamesTheMissedWord()
    {
        // A quiz allows no intent and asks only for a word: the intent stage does not run, so there is no off-prompt intent, and the tag stays.
        var prompt = TestSupport.QuizPrompt;
        var verdict = TestSupport.Pipeline("me gusta el río", prompt.Mode).Evaluate(TestSupport.Clip(), prompt, TestSupport.Context(agentId: "player")).Verdict;

        Assert.Equal(ErrorTags.VocabWrong, Assert.Single(verdict.Errors).Tag);
        Assert.Null(verdict.OffPromptIntent);
        Assert.False(verdict.Communicated);
    }

    // A lesson step's miss, a line that answers another question, is still not communicated: the step's prompt does not take its intent (the game retries it,
    // V14). Before M72 each had vocab.wrong.
    [Theory]
    [InlineData("quiero madera", "lesson-have", "request_item")]
    [InlineData("tengo una fruta", "lesson-like-bread", "state_have")]
    [InlineData("voy al río", "lesson-coins", "state_action")]
    [InlineData("me gusta el pan", "lesson-hello", "state_like")]
    public void ALessonStepsMissIsStillNotCommunicated(string transcript, string promptId, string offPrompt)
    {
        var verdict = Evaluate(transcript, promptId).Verdict;

        Assert.False(verdict.Communicated);
        Assert.Equal(IntentId.Unknown, verdict.Intent);
        Assert.Empty(verdict.Errors);
        Assert.Equal(new IntentId(offPrompt), verdict.OffPromptIntent);
    }

    // (c) hace frío at smith-open resolved ask_activity by a keyword rule on hacer alone, so Domingo answered a remark about the weather with his job. The rule
    // now needs qué with hacer: the weather is no question about work at any prompt, taken or off the prompt.
    [Theory]
    [InlineData("hace frío")]
    [InlineData("hace calor")]
    [InlineData("hoy hace sol")]
    [InlineData("¿Hace frío?")]
    [InlineData("hoy hace mucho calor")]
    public void TheWeatherIsNoQuestionAboutWorkAtAnyPrompt(string line)
    {
        var grader = new Content.TextGrader(TestSupport.Bundle);
        var activity = new IntentId("ask_activity");

        foreach (var prompt in TestSupport.Bundle.Prompts.Values)
        {
            var verdict = grader.Grade(line, prompt);
            Assert.True(verdict.Intent != activity && verdict.OffPromptIntent != activity, $"'{line}' at {prompt.PromptId}: {verdict.Intent} (off the prompt {verdict.OffPromptIntent})");
        }
    }

    // The clean neighbours of (c): a question about work is still one, by the grammar or by the rule (qué tú hace, a person error the rule alone reads).
    [Theory]
    [InlineData("¿Qué haces?", "smith-open")]
    [InlineData("qué haces", "farmer-open")]
    [InlineData("qué hace usted", "smith-open")]
    [InlineData("¿En qué trabajas?", "smith-open")]
    [InlineData("qué tú hace", "smith-open")]
    [InlineData("y tú qué haces", "smith-open")]
    public void AQuestionAboutWorkIsStillAskActivity(string line, string promptId)
    {
        Assert.Equal(new IntentId("ask_activity"), Evaluate(line, promptId).Verdict.Intent);
    }

    [Fact]
    public void AQuestionAboutWorkAtAPromptThatDoesNotTakeItIsOffThePrompt()
    {
        var verdict = Evaluate("¿qué haces?", "villager-open").Verdict;

        Assert.Equal(IntentId.Unknown, verdict.Intent);
        Assert.Equal(new IntentId("ask_activity"), verdict.OffPromptIntent);
    }

    [Fact]
    public void TheTwoPhaseEvaluationAndTheTextGraderGiveTheSameOffPromptVerdict()
    {
        var prompt = TestSupport.Bundle.Prompts["smith-open"];
        var pipeline = TestSupport.Pipeline("me gusta el río", prompt.Mode);
        var context = TestSupport.Context(agentId: "smith");

        var once = pipeline.Evaluate(TestSupport.Clip(), prompt, context).Verdict;
        var twoPhase = pipeline.Complete(pipeline.Analyze(TestSupport.Clip(), prompt), TestSupport.Context(agentId: "smith")).Verdict;
        var graded = new Content.TextGrader(TestSupport.Bundle).Grade("me gusta el río", prompt);

        Assert.Equal(once, twoPhase);
        Assert.Equal(new IntentId("state_like"), graded.OffPromptIntent);
        Assert.Equal(once.Errors, graded.Errors);
    }
}
