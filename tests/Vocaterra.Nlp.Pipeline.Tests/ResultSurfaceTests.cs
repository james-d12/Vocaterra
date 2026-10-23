using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Language.Lexicon;
using MatchType = Vocaterra.Nlp.Core.Stages.MatchType;

namespace Vocaterra.Nlp.Pipeline.Tests;

/// <summary>CR-002 and CR-004: the resolved words and the answer match on <see cref="PipelineResult"/>.</summary>
public class ResultSurfaceTests
{
    private static PromptSpec Prompt(string id)
    {
        return TestSupport.Bundle.Prompts[id];
    }

    private static PipelineResult Evaluate(string transcript, PromptSpec prompt, RecognitionMode mode = RecognitionMode.Open)
    {
        return TestSupport.Pipeline(transcript, mode).Evaluate(TestSupport.Clip(), prompt, TestSupport.Context());
    }

    private static PromptSpec IntroductionPrompt()
    {
        return new PromptSpec("introduce-self-test", RecognitionMode.Open, Difficulty.Beginner, "introductions", ["introduce_self"], null, []);
    }

    [Fact]
    public void WordsCarryTheSpansOfTheVerdictsErrors()
    {
        var result = Evaluate(TestSupport.WorkedExample, Prompt(TestSupport.OfferPrompt));

        Assert.NotNull(result.Words);
        Assert.Equal(["yo", "tiene", "2", "espada"], result.Words!.Select(static word => word.Token.Text).ToArray());
        Assert.Equal("dos", result.Words[2].Token.Original);
        Assert.Equal(result.Verdict.Errors[0].Span, result.Words[1].Token.Span);
        Assert.Equal(result.Verdict.Errors[1].Span, result.Words[3].Token.Span);
        Assert.Equal("tiene", result.Words[1].Token.Original);
    }

    [Fact]
    public void TheResolvedReadingOfTheConjugatedVerbIsItsLemma()
    {
        var result = Evaluate(TestSupport.WorkedExample, Prompt(TestSupport.OfferPrompt));

        var tiene = result.Words![1];
        Assert.Equal(WordRole.Word, tiene.Role);
        Assert.Equal("tener", tiene.Reading!.Lemma);
        Assert.Equal(LexiconSchema.Verb, tiene.Reading.Pos);
    }

    [Fact]
    public void TheNounOfTheWorkedExampleIsAFeminineSingularNoun()
    {
        var result = Evaluate(TestSupport.WorkedExample, Prompt(TestSupport.OfferPrompt));

        var espada = result.Words![3];
        Assert.Equal(WordRole.Word, espada.Role);
        Assert.Equal(new LexicalFeatures("espada", LexiconSchema.Noun, null, LexiconSchema.Singular, null, null, LexiconSchema.Feminine), espada.Reading);
    }

    [Fact]
    public void AVerbTakenFromTheAccentedReadingIsReportedAsResolved()
    {
        var result = Evaluate("donde esta el arbol", Prompt(TestSupport.OfferPrompt));

        var esta = result.Words![1];
        Assert.Equal("esta", esta.Token.Original);
        Assert.Equal("estar", esta.Reading!.Lemma);
        Assert.Equal(LexiconSchema.Verb, esta.Reading.Pos);
    }

    [Fact]
    public void M29j_AQuestionedEstarIsTheIndicativeNotTheImperative()
    {
        var result = Evaluate("dónde está el río", Prompt(TestSupport.OfferPrompt));

        var esta = result.Words![1];
        Assert.Equal(new LexicalFeatures("estar", LexiconSchema.Verb, "3", LexiconSchema.Singular, "pres", "ind", null), esta.Reading);
    }

    [Theory]
    [InlineData("gracias")]
    [InlineData("muchas gracias")]
    public void M29j_GraciasIsTheInterjection(string transcript)
    {
        var gracias = Evaluate(transcript, Prompt(TestSupport.OfferPrompt)).Words!.Last();

        Assert.Equal(new LexicalFeatures("gracias", LexiconSchema.Interjection, null, null, null, null, null), gracias.Reading);
    }

    [Fact]
    public void M29j_PorFavorIsOneExpressionOverTwoTokens()
    {
        var words = Evaluate("quiero agua por favor", Prompt(TestSupport.OfferPrompt)).Words!;

        Assert.Equal(["por", "favor"], words.Skip(2).Select(static w => w.Token.Text).ToArray());
        Assert.All(words.Skip(2), static w => Assert.Equal(new LexicalFeatures("por favor", LexiconSchema.Interjection, null, null, null, null, null), w.Reading));
        Assert.NotEqual(words[2].Token.Span, words[3].Token.Span);
    }

    [Fact]
    public void AName_BoundToAWordElement_IsMarkedAsAName()
    {
        var result = Evaluate("me llamo Ana", IntroductionPrompt());

        var ana = result.Words![2];
        Assert.Equal("Ana", ana.Token.Original);
        Assert.Equal(WordRole.Name, ana.Role);
        Assert.Null(ana.Reading);
        Assert.Equal("llamar", result.Words[1].Reading!.Lemma);
    }

    [Fact]
    public void AnUnknownWordIsMarkedUnknown()
    {
        var result = Evaluate("yo tiene dos zorbas", Prompt(TestSupport.OfferPrompt));

        var zorbas = result.Words![3];
        Assert.Equal(WordRole.Unknown, zorbas.Role);
        Assert.Null(zorbas.Reading);
        Assert.Contains(result.Verdict.Errors, static error => error.Tag == ErrorTags.VocabUnknown && error.Actual == "zorbas");
    }

    [Fact]
    public void AQuizAnswerExposesTheMatchedAnswerId()
    {
        var sword = TestSupport.Bundle.AnswerSets["items-basic"].Answers.Single(static answer => answer.Canonical == "espada");

        var result = Evaluate("espada", Prompt("quiz-item-name"), RecognitionMode.Constrained);

        Assert.NotNull(result.Answer);
        Assert.Equal(sword.AnswerId, result.Answer!.AnswerId);
        Assert.Equal(MatchType.Exact, result.Answer.Type);
        Assert.Equal(0, result.Answer.Distance);
        Assert.False(result.Answer.LikelyPronunciation);
    }

    [Fact]
    public void AQuizAnswerSpokenLikeTheCanonicalIsAFuzzyMatchWithLikelyPronunciation()
    {
        var sword = TestSupport.Bundle.AnswerSets["items-basic"].Answers.Single(static answer => answer.Canonical == "espada");

        var result = Evaluate("ezpada", Prompt("quiz-item-name"), RecognitionMode.Constrained);

        Assert.Equal(sword.AnswerId, result.Answer!.AnswerId);
        Assert.Equal(MatchType.Fuzzy, result.Answer.Type);
        Assert.Equal(0, result.Answer.Distance);
        Assert.True(result.Answer.LikelyPronunciation);
    }

    [Fact]
    public void TheSurfaceAndTheAnswerAreEqualForIdenticalRuns()
    {
        var first = Evaluate(TestSupport.WorkedExample, Prompt(TestSupport.OfferPrompt));
        var second = Evaluate(TestSupport.WorkedExample, Prompt(TestSupport.OfferPrompt));

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.Equal(TestSupport.Json(first), TestSupport.Json(second));
    }
}
