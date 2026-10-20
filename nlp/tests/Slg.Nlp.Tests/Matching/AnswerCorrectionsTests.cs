using Slg.Core.Stages;
using Slg.Core.Taxonomy;
using Slg.Core.Verdicts;
using Slg.Nlp.Matching;
using Slg.Nlp.Normalization;
using MatchType = Slg.Core.Stages.MatchType;

namespace Slg.Nlp.Tests.Matching;

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

    [Fact]
    public void ANearestWordIsChosenFromTheCanonicalPhraseByAccentInsensitiveDistance()
    {
        var result = Correct("buen dia", new MatchResult("day", MatchType.Fuzzy, 1, false, 0.9), [Unknown(5, 3, "dia")]);

        Assert.Equal("días", Assert.Single(result.Errors).Expected);
    }

    [Fact]
    public void ASingleWordThatStandsForThePhraseTakesTheWholeCanonicalPhrase()
    {
        var result = Correct("buenosdias", new MatchResult("day", MatchType.Fuzzy, 1, false, 0.9), [Unknown(0, 10, "buenosdias")]);

        Assert.Equal("buenos días", Assert.Single(result.Errors).Expected);
    }

    [Fact]
    public void TheAnswerIsCorrectedByItsCanonicalSpellingNotByAnAlias()
    {
        // The alias "una espada" matched; the expected word is still the canonical "espada".
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

        var result = AnswerCorrections.WithCanonicalWords(analysis, text, new MatchResult("sword", type, 0, false, 1.0), Items, Options);

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

        var result = AnswerCorrections.WithCanonicalWords(analysis, Norm("ezpada"), new MatchResult("sword", MatchType.Fuzzy, 0, true, 1.0), Items, Options);

        Assert.Same(analysis, result);
    }

    [Fact]
    public void AWordThatAlreadyEqualsTheCanonicalWordIsNotCorrectedToItself()
    {
        var analysis = Analysis([Unknown(0, 6, "espada")]);

        var result = AnswerCorrections.WithCanonicalWords(analysis, Norm("espada"), new MatchResult("sword", MatchType.Fuzzy, 0, false, 1.0), Items, Options);

        Assert.Same(analysis, result);
    }

    [Fact]
    public void AnUnknownAnswerIdChangesNothing()
    {
        var analysis = Analysis([Unknown(0, 6, "ezpada")]);

        var result = AnswerCorrections.WithCanonicalWords(analysis, Norm("ezpada"), new MatchResult("missing", MatchType.Fuzzy, 0, true, 1.0), Items, Options);

        Assert.Same(analysis, result);
    }

    private static AnalysisResult Correct(string text, MatchResult match, IReadOnlyList<ErrorTag> errors)
    {
        return AnswerCorrections.WithCanonicalWords(Analysis(errors), Norm(text), match, Items, Options);
    }

    private static NormalizedText Norm(string text)
    {
        return TextNormalizer.Normalize(text, Options);
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
