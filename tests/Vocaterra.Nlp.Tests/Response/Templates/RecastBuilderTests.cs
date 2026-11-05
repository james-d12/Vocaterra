namespace Vocaterra.Nlp.Response.Tests.Templates;

using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Language.Normalization;
using Vocaterra.Nlp.Response.Templates;

public class RecastBuilderTests
{
    private static string Recast(string playerText, bool numbersToDigits = false)
    {
        var normalized = TestSupport.Normalize(playerText, numbersToDigits);
        return RecastBuilder.Build(normalized, TestSupport.Analyzer.Analyze(normalized), SpanishLexicon.Empty, SpanishLanguagePack.Instance);
    }

    private static AnalysisResult Analysis(params ErrorTag[] errors)
    {
        return new AnalysisResult([], [], errors, null, 1.0);
    }

    private static NormalizedText Plain(string text)
    {
        return SpanishTextNormalizer.Normalize(text, new NormalizationOptions { NumbersToDigits = false, ConvertIndefiniteArticles = false });
    }

    [Theory]
    [InlineData("yo tiene dos espada", "yo tengo dos espadas")]
    [InlineData("yo tengo dos espadas", "yo tengo dos espadas")]
    [InlineData("yo comprar una espada", "yo compro una espada")]
    [InlineData("tengo un espada", "tengo una espada")]
    [InlineData("quiero las espada", "quiero las espadas")]
    [InlineData("tú tengo dos libros", "tú tienes dos libros")]
    [InlineData("ayer yo compro una espada", "ayer yo compré una espada")]
    [InlineData("yo tiene un espada rojo", "yo tengo una espada roja")]
    [InlineData("la espada rojas", "la espada roja")]
    public void RecastCorrectsEveryError(string player, string expected)
    {
        Assert.Equal(expected, Recast(player));
    }

    [Theory]
    [InlineData("una roja espada", "una espada roja")]
    [InlineData("la rojo casa", "la casa roja")]
    [InlineData("los rojo espada", "las espadas rojas")]
    [InlineData("unas roja espadas", "unas espadas rojas")]
    public void WordOrderRecastsKeepTheAgreementCorrectionsInsideTheMovedWords(string player, string expected)
    {
        Assert.Equal(expected, Recast(player));
    }

    [Fact]
    public void RecastWorksWhenTheNormalizerTurnedNumbersIntoDigits()
    {
        var normalized = SpanishTextNormalizer.Normalize("yo tiene dos espada", new NormalizationOptions { ConvertIndefiniteArticles = false });
        Assert.Equal("yo tiene 2 espada", normalized.Text);
        Assert.Equal("yo tengo dos espadas", RecastBuilder.Build(normalized, TestSupport.Analyzer.Analyze(normalized), SpanishLexicon.Empty, SpanishLanguagePack.Instance));
        Assert.Equal("yo tengo dos espadas", Recast("yo tiene dos espada", numbersToDigits: true));
    }

    [Theory]
    [InlineData("tengo 1 espada", "tengo una espada")]
    [InlineData("tengo 1 libro", "tengo un libro")]
    [InlineData("tengo 1 grande queso", "tengo un grande queso")]
    [InlineData("tengo 1 buena espada", "tengo una buena espada")]
    [InlineData("tengo 1 mi espada", "tengo una mi espada")]
    [InlineData("tengo 1 tu 1 espada", "tengo una tu una espada")]
    [InlineData("tengo 1 tu 1 libro", "tengo un tu un libro")]
    [InlineData("tengo 21 espadas", "tengo veintiuna espadas")]
    [InlineData("tengo 3 libros", "tengo tres libros")]
    [InlineData("tengo 1", "tengo uno")]
    [InlineData("quiero 0 monedas", "quiero cero monedas")]
    [InlineData("tengo 007 monedas", "tengo 007 monedas")]
    [InlineData("tengo 1234567 monedas", "tengo 1234567 monedas")]
    public void DigitsAreSpelledOutBeforeSpeaking(string player, string expected)
    {
        var normalized = SpanishTextNormalizer.Normalize(player, new NormalizationOptions { NumbersToDigits = false, ConvertIndefiniteArticles = false });
        Assert.Equal(expected, RecastBuilder.Build(normalized, TestSupport.Analyzer.Analyze(normalized), SpanishLexicon.Empty, SpanishLanguagePack.Instance));
    }

    [Fact]
    public void PronunciationTagsAndEmptyExpectedAreSkipped()
    {
        var text = Plain("tengo dos espadas");
        var analysis = Analysis(
            new ErrorTag(ErrorTags.PronR, new TextSpan(6, 3), "ɾ", "ɹ"),
            new ErrorTag(ErrorTags.PronStress, new TextSpan(0, 5), "tenGo", "tengo"),
            new ErrorTag(ErrorTags.VocabUnknown, new TextSpan(11, 6), string.Empty, "espadas"));
        Assert.Equal("tengo dos espadas", RecastBuilder.Build(text, analysis, SpanishLexicon.Empty, SpanishLanguagePack.Instance));
    }

    [Fact]
    public void ErrorsAreAppliedByOffsetFromTheLastSpanToTheFirst()
    {
        var text = Plain("yo tiene dos espada");
        var analysis = Analysis(
            new ErrorTag(ErrorTags.ConjPerson, new TextSpan(3, 5), "tengo", "tiene"),
            new ErrorTag(ErrorTags.NounNumber, new TextSpan(13, 6), "espadas", "espada"));
        Assert.Equal("yo tengo dos espadas", RecastBuilder.Build(text, analysis, SpanishLexicon.Empty, SpanishLanguagePack.Instance));
        var reversed = Analysis(analysis.Errors[1], analysis.Errors[0]);
        Assert.Equal("yo tengo dos espadas", RecastBuilder.Build(text, reversed, SpanishLexicon.Empty, SpanishLanguagePack.Instance));
    }

    [Fact]
    public void ReplacementsOfDifferentLengthKeepLaterOffsetsValid()
    {
        var text = Plain("a b c");
        var analysis = Analysis(
            new ErrorTag(ErrorTags.VocabWrong, new TextSpan(0, 1), "muchas palabras", "a"),
            new ErrorTag(ErrorTags.VocabWrong, new TextSpan(4, 1), "z", "c"));
        Assert.Equal("muchas palabras b z", RecastBuilder.Build(text, analysis, SpanishLexicon.Empty, SpanishLanguagePack.Instance));
    }

    [Fact]
    public void NarrowEditsInsideAWordOrderEditAreMergedIntoIt()
    {
        var text = Plain("rojo casa");
        var analysis = Analysis(
            new ErrorTag(ErrorTags.GenderAgree, new TextSpan(0, 4), "roja", "rojo"),
            new ErrorTag(ErrorTags.WordOrder, new TextSpan(0, 9), "casa rojo", "rojo casa"));
        Assert.Equal("casa roja", RecastBuilder.Build(text, analysis, SpanishLexicon.Empty, SpanishLanguagePack.Instance));
    }

    [Fact]
    public void PartiallyOverlappingEditsKeepOnlyTheFirst()
    {
        var text = Plain("abcdefgh");
        var analysis = Analysis(
            new ErrorTag(ErrorTags.VocabWrong, new TextSpan(0, 5), "X", "abcde"),
            new ErrorTag(ErrorTags.VocabWrong, new TextSpan(3, 5), "Y", "defgh"));
        Assert.Equal("Xfgh", RecastBuilder.Build(text, analysis, SpanishLexicon.Empty, SpanishLanguagePack.Instance));
    }

    [Fact]
    public void InvalidSpansAndNoOpEditsAreIgnored()
    {
        var text = Plain("tengo espadas");
        var analysis = Analysis(
            new ErrorTag(ErrorTags.VocabWrong, new TextSpan(-1, 3), "x", "y"),
            new ErrorTag(ErrorTags.VocabWrong, new TextSpan(5, 0), "x", "y"),
            new ErrorTag(ErrorTags.VocabWrong, new TextSpan(10, 99), "x", "y"),
            new ErrorTag(ErrorTags.NounNumber, new TextSpan(6, 7), "espadas", "espadas"));
        Assert.Equal("tengo espadas", RecastBuilder.Build(text, analysis, SpanishLexicon.Empty, SpanishLanguagePack.Instance));
    }

    [Fact]
    public void RecastIsDeterministic()
    {
        Assert.Equal(Recast("yo tiene dos espada"), Recast("yo tiene dos espada"));
    }

    // M78: the verdict's errors (the near miss of an answer, which the analysis lacks) are applied beside the analysis's own.
    [Fact]
    public void TheVerdictsErrorsAreAppliedBesideTheAnalysisOnes()
    {
        var text = TestSupport.Normalize("cuál es esto");
        var analysis = TestSupport.Analyzer.Analyze(text);
        var nearMiss = new ErrorTag(ErrorTags.VocabWrong, new TextSpan(0, 4), "qué", "cuál");

        Assert.Equal("cuál es esto", RecastBuilder.Build(text, analysis, SpanishLexicon.Empty, SpanishLanguagePack.Instance));
        Assert.Equal("cuál es esto", RecastBuilder.Build(text, analysis, SpanishLexicon.Empty, SpanishLanguagePack.Instance, []));
        Assert.Equal("qué es esto", RecastBuilder.Build(text, analysis, SpanishLexicon.Empty, SpanishLanguagePack.Instance, [nearMiss]));
    }

    [Fact]
    public void AnErrorTheAnalysisAlreadyHasIsNotAppliedTwice()
    {
        var text = TestSupport.Normalize("yo tiene dos espada");
        var analysis = TestSupport.Analyzer.Analyze(text);

        Assert.NotEmpty(analysis.Errors);
        Assert.Equal(
            RecastBuilder.Build(text, analysis, SpanishLexicon.Empty, SpanishLanguagePack.Instance),
            RecastBuilder.Build(text, analysis, SpanishLexicon.Empty, SpanishLanguagePack.Instance, analysis.Errors));
    }
}
