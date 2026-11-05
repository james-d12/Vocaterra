using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Taxonomy;

namespace Vocaterra.Nlp.Pipeline.Tests;

/// <summary>
/// M78: a name the lexicon does not know is no unknown word where a name is said. The rule (<c>IAnalysisRules.IsNamePosition</c>, the analyzer's name fallback): at a
/// prompt that takes a free line, a word with no reading that no grammar's <c>[WORD]</c> binds is a name when it is the only word of the line and capitalised, or follows
/// <c>soy</c>, <c>me llamo</c>, <c>mi nombre es</c>, a greeting (<c>hola</c>, <c>buenos días</c>, <c>buenas tardes</c>) or a farewell. Any other unknown word stays unknown, a quiz
/// keeps its misspelt answers, and the content lint still flags an example with a word nobody knows. The owner's line stays the same: <c>me llamo pedro</c> is
/// <c>ask_name</c> where the prompt takes it.
/// </summary>
public class NameLineTests
{
    private static PipelineResult Evaluate(string transcript, string promptId)
    {
        var prompt = TestSupport.Bundle.Prompts[promptId];
        return TestSupport.Pipeline(transcript, prompt.Mode).Evaluate(TestSupport.Clip(), prompt, TestSupport.Context(agentId: prompt.AgentId ?? "player"));
    }

    [Theory]
    [InlineData("Pedro")]
    [InlineData("soy pedro")]
    [InlineData("soy Pedro")]
    [InlineData("hola, soy pedro")]
    [InlineData("hola pedro")]
    [InlineData("buenos días pedro")]
    [InlineData("buenas tardes, pedro")]
    [InlineData("adiós pedro")]
    [InlineData("me llamo pedro")]
    [InlineData("mi nombre es pedro")]
    public void AnUnknownNameWhereANameIsSaidIsNoUnknownWord(string line)
    {
        var verdict = Evaluate(line, "villager-open").Verdict;

        Assert.DoesNotContain(verdict.Errors, static error => error.Tag == ErrorTags.VocabUnknown);
        Assert.Empty(verdict.Errors);
        Assert.Equal(1.0, verdict.Correctness);
    }

    [Theory]
    [InlineData("Pedro", "unknown", false)]
    [InlineData("hola, soy pedro", "greet", true)]
    [InlineData("hola pedro", "greet", true)]
    [InlineData("me llamo pedro", "ask_name", true)]
    public void TheIntentAndCommunicationOfTheNameLines(string line, string intent, bool communicated)
    {
        var verdict = Evaluate(line, "smith-open").Verdict;

        Assert.Equal((intent, communicated), (verdict.Intent.Value, verdict.Communicated));
    }

    [Theory]
    [InlineData("pedro")] // a lone lower-case word cannot be told from a mistyped one (the game's xyzzy)
    [InlineData("xyzzy")]
    [InlineData("quiero pedro")] // not after an introducer, not alone
    [InlineData("hace pedro")]
    [InlineData("hola quiero pedro")] // the introducer is not directly before it
    [InlineData("pedro quiero pan")] // first of several words
    [InlineData("el pan es pedro")]
    [InlineData("hace dos días pedro")] // días after another word than buenos
    public void AnyOtherUnknownWordStaysUnknown(string line)
    {
        var verdict = Evaluate(line, "villager-open").Verdict;

        Assert.Contains(verdict.Errors, static error => error.Tag == ErrorTags.VocabUnknown);
        Assert.True(verdict.Correctness < 1.0);
    }

    [Theory]
    [InlineData("pedro")]
    [InlineData("soy pedro")]
    [InlineData("hola pedro")]
    public void AQuizKeepsAnUnknownWordUnknown(string line)
    {
        var verdict = TestSupport.Pipeline(line, Vocaterra.Nlp.Core.Prompts.RecognitionMode.Constrained)
            .Evaluate(TestSupport.Clip(), TestSupport.QuizPrompt, TestSupport.Context(state: "none", agentId: "player")).Verdict;

        Assert.Contains(verdict.Errors, static error => error.Tag == ErrorTags.VocabUnknown);
    }

    [Theory]
    [InlineData("hola pedro", "villager-open")]
    [InlineData("soy pedro", "smith-open")]
    public void TheTextGraderReadsTheNameAsThePipelineDoes(string line, string promptId)
    {
        var prompt = TestSupport.Bundle.Prompts[promptId];

        Assert.Equal(Evaluate(line, promptId).Verdict.Errors, new TextGrader(TestSupport.Bundle).Grade(line, prompt).Errors);
        Assert.Empty(new TextGrader(TestSupport.Bundle).Grade(line, prompt).Errors);
    }

    // M78, accepted (the owner's point 3 of the follow-ups; M66's known limit): a name said to the listener at the end of a line is a word of the lexicon, domingo, the day,
    // and the core reads the sentence with it, so ayuda becomes a verb form (conj.tense). The host takes the NPC's name out of the line before the core reads it (the
    // game's path is right), and a core fix would need the bundle's agents' names in the analysis and a change of the reading resolver, not only the vocative. The CLI keeps
    // flagging it; this test records that, and the name first (domingo, necesito ayuda) is clean.
    [Fact]
    public void ANameSaidAtTheEndOfALineIsFlaggedByTheCoreAndTakenOutByTheHost()
    {
        var atTheEnd = Evaluate("necesito ayuda domingo", "villager-open").Verdict;
        var first = Evaluate("domingo, necesito ayuda", "villager-open").Verdict;

        Assert.Equal(ErrorTags.ConjTense, Assert.Single(atTheEnd.Errors).Tag);
        Assert.Empty(first.Errors);
        Assert.Equal("ask_help", first.Intent.Value);
    }
}
