using Vocaterra.Nlp.Asr;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;

namespace Vocaterra.Nlp.Pipeline.Tests;

/// <summary>
/// M59 (d) at the game's own prompts: the reading of each word that a host reads (<c>UtteranceAnalysis.Words</c>, which the game's lesson person check takes the verb's
/// person from). <c>¿qué hace Domingo?</c>, at a prompt that takes <c>ask_activity</c>, matches no grammar of the prompt and was reported with <c>hace</c> as the
/// preposition of <c>hace dos días</c>, so the host found no verb; it is the verb <c>hacer</c> now. On <c>content/game-v1</c>.
/// </summary>
public class ReportedVerbReadingTests
{
    private static ResolvedWord WordOf(string transcript, string promptId, string word)
    {
        var bundle = TestSupport.Bundle;
        var prompt = bundle.Prompts[promptId];
        var pipeline = new UtterancePipeline(new PipelineServices(new ScriptedWordRecognizer(TestSupport.Asr(transcript, prompt.Mode)), null, null, null, bundle));
        return pipeline.Analyze(TestSupport.Clip(), prompt).Words!.Single(w => w.Token.Text == word);
    }

    [Theory]
    [InlineData("¿qué hace Domingo?", "farmer-open", "hace", "3", "sg")]
    [InlineData("¿qué hace Domingo?", "smith-open", "hace", "3", "sg")]
    [InlineData("¿qué hace?", "farmer-open", "hace", "3", "sg")]
    [InlineData("¿qué haces?", "farmer-open", "haces", "2", "sg")]
    [InlineData("va a hace un hacha", "lesson-will-make", "hace", "3", "sg")]
    public void HaceWhereNoTimeFollowsIsReportedAsTheVerb(string transcript, string promptId, string word, string person, string number)
    {
        var reading = WordOf(transcript, promptId, word).Reading!;

        Assert.Equal(("verb", "hacer", person, number), (reading.Pos, reading.Lemma, reading.Person, reading.Number));
    }

    [Fact]
    public void HaceBeforeAPhraseOfTimeIsReportedAsThePreposition()
    {
        var reading = WordOf("hace dos días comí pan", "villager-open", "hace").Reading!;

        Assert.Equal(("prep", "hace"), (reading.Pos, reading.Lemma));
    }
}
