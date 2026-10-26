using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Response.Templates;

namespace Vocaterra.Nlp.Pipeline.Tests;

/// <summary>
/// <see cref="TextGrader"/> (which the content lint grades example expectations with) is the text path of <see cref="UtterancePipeline"/>. These tests keep
/// the two from drifting apart: for every example of every intent and every prompt that allows the intent, both give the same verdict.
/// </summary>
public class TextGraderTests
{
    private static readonly TextGrader Grader = new(TestSupport.Bundle);

    // The lint grader carries no recognizer hash, so the stamps differ; everything else must be identical.
    private static Verdict WithoutVersions(Verdict verdict)
    {
        return verdict with { Versions = VersionStamp.Empty };
    }

    [Fact]
    public void EveryExampleOfEveryAllowedPromptGetsThePipelinesVerdict()
    {
        var checkedPairs = 0;
        foreach (var intent in TestSupport.Bundle.Intents.Intents)
        {
            foreach (var prompt in TestSupport.Bundle.Prompts.Values.Where(p => p.AllowedIntentIds.Contains(intent.Id.Value)))
            {
                foreach (var example in intent.Examples)
                {
                    var pipeline = TestSupport.Pipeline(example, prompt.Mode);
                    var context = TestSupport.Context(agentId: prompt.AgentId ?? "player");
                    var expected = pipeline.Evaluate(TestSupport.Clip(), prompt, context).Verdict;

                    var actual = Grader.Grade(example, prompt);

                    Assert.Equal(TestSupport.Json(WithoutVersions(expected)), TestSupport.Json(WithoutVersions(actual)));
                    checkedPairs++;
                }
            }
        }

        Assert.True(checkedPairs > 90, $"only {checkedPairs} pairs were compared");
    }

    [Fact]
    public void ThePromptWithoutIntentsGradesAsAnAnswerOnly()
    {
        var quiz = TestSupport.QuizPrompt;
        var expected = TestSupport.Pipeline("una piedra", quiz.Mode).Evaluate(TestSupport.Clip(), quiz, TestSupport.Context(agentId: "player")).Verdict;

        Assert.Equal(TestSupport.Json(WithoutVersions(expected)), TestSupport.Json(WithoutVersions(Grader.Grade("una piedra", quiz))));
        Assert.True(Grader.Grade("una piedra", quiz).Intent.IsUnknown);
    }

    [Fact]
    public void TheReadmeDemoGivesConjPersonAndNounNumber()
    {
        var verdict = Grader.Grade(TestSupport.WorkedExample, TestSupport.Bundle.Prompts[TestSupport.OpenPrompt]);

        Assert.Equal(["conj.person", "noun.number"], verdict.Errors.Select(static e => e.Tag).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void AnUnknownAnswerSetIsRejected()
    {
        var prompt = new PromptSpec("p", RecognitionMode.Open, Difficulty.Beginner, "nope", [], null, []);

        Assert.Throws<ArgumentException>(() => Grader.Grade("hola", prompt));
    }

    [Fact]
    public void ThePipelineUsesTheSharedPoolNames()
    {
        Assert.Equal(ReservedPools.AnswerAccepted, UtterancePipeline.AnswerAcceptedPool);
        Assert.Equal(ReservedPools.NotUnderstood, ResponsePlanner.NotUnderstoodIntent);
    }
}
