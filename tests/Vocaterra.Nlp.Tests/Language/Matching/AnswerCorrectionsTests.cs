using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Language.Matching;
using Vocaterra.Nlp.Language.Normalization;
using MatchType = Vocaterra.Nlp.Core.Stages.MatchType;

namespace Vocaterra.Nlp.Language.Tests.Matching;

/// <summary>Tests for <see cref="AnswerCorrections"/>: an unknown word of a fuzzy near miss takes the canonical word of the answer it matched.</summary>
public class AnswerCorrectionsTests
{
    private static readonly AnswerSet Items = new(
        "items",
        [
            new Answer("sword", "espada", ["una espada"], []),
            new Answer("potion", "poción", ["brebaje"], []),
            new Answer("day", "buenos días", ["buen día"], []),
        ]);

    private static readonly NormalizationOptions Options = new(ConvertIndefiniteArticles: false);

    [Fact]
    public void ASingleUnknownWordTakesTheCanonicalWordOfTheFuzzyMatch()
    {
        var result = Correct("ezpada", new MatchResult("sword", MatchType.Fuzzy, 0, true, 1.0), [Unknown(0, 6, "ezpada")]);

        Assert.Equal(new ErrorTag(ErrorTags.VocabUnknown, new TextSpan(0, 6), "espada", "ezpada"), Assert.Single(result.Errors));
    }

    [Fact]
    public void TheCanonicalSpellingWithItsAccentsIsUsed()
    {
        var result = Correct("pocion", new MatchResult("potion", MatchType.Fuzzy, 0, true, 1.0), [Unknown(0, 6, "pocion")]);

        Assert.Equal("poción", Assert.Single(result.Errors).Expected);
    }

    [Fact]
    public void AnUnknownWordAmongKnownWordsTakesTheNearestCanonicalWord()
    {
        // "una" is a lexicon form and gets no error, so the recast reads "una espada" rather than "una ezpada".
        var result = Correct("una ezpada", new MatchResult("sword", MatchType.Fuzzy, 1, false, 0.8), [Unknown(4, 6, "ezpada")]);

        Assert.Equal("espada", Assert.Single(result.Errors).Expected);
    }

    // M70: the word comes from the phrase of the answer nearest the line, here the alias buen día (before M70 the canonical buenos días gave días, and the recast
    // buen días).
    [Fact]
    public void TheWordComesFromTheAnswersPhraseNearestTheLine()
    {
        var result = Correct("buen dia", new MatchResult("day", MatchType.Fuzzy, 1, false, 0.9), [Unknown(5, 3, "dia")]);

        Assert.Equal("día", Assert.Single(result.Errors).Expected);
    }

    // M70 (QA-language-6): the unknown word takes the answer's word at its own place, not the word nearest its spelling wherever it stands, and a word the phrase has
    // no place for takes none. Before M70 three took compré (the recast said compré compré manzanas) and glorp took iré (mañana iré al iré); the phrase nearest
    // glorp compré tres manzanas is the alias yo compré tres manzanas, whose yo stands at glorp's place.
    [Theory]
    [InlineData("compré three manzanas", 7, 5, "three", "tres")]
    [InlineData("compré tres manzanaz", 9, 8, "manzanaz", "manzanas")]
    [InlineData("mañana iré al glorp", 14, 5, "glorp", "río")]
    [InlineData("glorp compré tres manzanas", 0, 5, "glorp", "yo")]
    [InlineData("compré tres manzanas glorp", 18, 5, "glorp", "")]
    public void AnUnknownWordTakesTheAnswersWordAtItsPlace(string text, int start, int length, string actual, string expected)
    {
        var set = new AnswerSet(
            "lessons",
            [
                new Answer("market", "compré tres manzanas", ["compré manzanas", "yo compré tres manzanas"], []),
                new Answer("river", "iré al río", ["mañana iré al río"], []),
            ]);
        var answerId = text.Contains("iré", StringComparison.Ordinal) ? "river" : "market";
        var errors = new[] { Unknown(start, length, actual) };

        var result = AnswerCorrections.WithCanonicalWords(Analysis(errors), Norm(text), new MatchResult(answerId, MatchType.Fuzzy, 4, false, 0.8), set, SpanishLanguagePack.Instance, Options);

        Assert.Equal(expected, Assert.Single(result.Errors).Expected);
    }

    [Fact]
    public void ASingleWordThatStandsForThePhraseTakesTheWholeCanonicalPhrase()
    {
        var result = Correct("buenosdias", new MatchResult("day", MatchType.Fuzzy, 1, false, 0.9), [Unknown(0, 10, "buenosdias")]);

        Assert.Equal("buenos días", Assert.Single(result.Errors).Expected);
    }

    [Fact]
    public void TheAnswerIsCorrectedByTheWordOfItsNearestPhrase()
    {
        // The alias "una espada" is the phrase nearest the line; its word at the place of ezpada is espada.
        var result = Correct("una ezpada", new MatchResult("sword", MatchType.Fuzzy, 1, false, 0.8), [Unknown(4, 6, "ezpada")]);

        Assert.Equal("espada", Assert.Single(result.Errors).Expected);
    }

    [Theory]
    [InlineData(MatchType.Exact)]
    [InlineData(MatchType.Alias)]
    [InlineData(MatchType.None)]
    public void ExactAliasAndNoMatchChangeNothing(MatchType type)
    {
        var analysis = Analysis([Unknown(0, 6, "ezpada")]);
        var text = Norm("ezpada");

        var result = AnswerCorrections.WithCanonicalWords(analysis, text, new MatchResult("sword", type, 0, false, 1.0), Items, SpanishLanguagePack.Instance, Options);

        Assert.Same(analysis, result);
    }

    [Fact]
    public void ErrorsThatAreNotUnknownWordsOrAlreadyHaveAnExpectedWordAreKept()
    {
        var errors = new[]
        {
            new ErrorTag(ErrorTags.ConjPerson, new TextSpan(0, 6), "tengo", "tiene"),
            new ErrorTag(ErrorTags.VocabUnknown, new TextSpan(0, 6), "ya", "ezpada"),
        };
        var analysis = Analysis(errors);

        var result = AnswerCorrections.WithCanonicalWords(analysis, Norm("ezpada"), new MatchResult("sword", MatchType.Fuzzy, 0, true, 1.0), Items, SpanishLanguagePack.Instance, Options);

        Assert.Same(analysis, result);
    }

    [Fact]
    public void AWordThatAlreadyEqualsTheCanonicalWordIsNotCorrectedToItself()
    {
        var analysis = Analysis([Unknown(0, 6, "espada")]);

        var result = AnswerCorrections.WithCanonicalWords(analysis, Norm("espada"), new MatchResult("sword", MatchType.Fuzzy, 0, false, 1.0), Items, SpanishLanguagePack.Instance, Options);

        Assert.Same(analysis, result);
    }

    [Fact]
    public void AnUnknownAnswerIdChangesNothing()
    {
        var analysis = Analysis([Unknown(0, 6, "ezpada")]);

        var result = AnswerCorrections.WithCanonicalWords(analysis, Norm("ezpada"), new MatchResult("missing", MatchType.Fuzzy, 0, true, 1.0), Items, SpanishLanguagePack.Instance, Options);

        Assert.Same(analysis, result);
    }

    private static AnalysisResult Correct(string text, MatchResult match, IReadOnlyList<ErrorTag> errors)
    {
        return AnswerCorrections.WithCanonicalWords(Analysis(errors), Norm(text), match, Items, SpanishLanguagePack.Instance, Options);
    }

    private static NormalizedText Norm(string text)
    {
        return SpanishTextNormalizer.Normalize(text, Options);
    }

    private static ErrorTag Unknown(int start, int length, string actual)
    {
        return new ErrorTag(ErrorTags.VocabUnknown, new TextSpan(start, length), string.Empty, actual);
    }

    private static AnalysisResult Analysis(IReadOnlyList<ErrorTag> errors)
    {
        return new AnalysisResult([], [], errors, null, 0.5);
    }
}
