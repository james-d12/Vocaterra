using Slg.Core.Prompts;
using Slg.Core.Stages;
using Slg.Nlp.Matching;
using Slg.Nlp.Normalization;

// System.IO.MatchType (implicit usings) would otherwise make the name ambiguous.
using MatchType = Slg.Core.Stages.MatchType;

namespace Slg.Nlp.Tests.Matching;

/// <summary>Tests for <see cref="AnswerMatcher"/>.</summary>
public class AnswerMatcherTests
{
    private static readonly AnswerSet CarAndSword = new(
        "shop",
        [
            new Answer("sword", "espada", [], []),
            new Answer("car", "coche", ["carro", "auto"], ["es-ES", "es-MX", "es-AR"]),
        ]);

    private static NormalizedText Norm(string text, NormalizationOptions? options = null)
    {
        return TextNormalizer.Normalize(text, options);
    }

    private static AnswerSet Single(string phrase, params string[] aliases)
    {
        return new AnswerSet("single", [new Answer("only", phrase, aliases, [])]);
    }

    [Fact]
    public void CanonicalPhraseIsAnExactMatch()
    {
        var result = AnswerMatcher.Match(Norm("¡Coche!"), CarAndSword, Difficulty.Beginner);

        Assert.Equal(new MatchResult("car", MatchType.Exact, 0, false, 1.0), result);
    }

    [Theory]
    [InlineData("carro")]
    [InlineData("auto")]
    [InlineData("¿Auto?")]
    public void AliasesResolveToTheSameAnswerIdWithAliasType(string spoken)
    {
        foreach (var difficulty in Enum.GetValues<Difficulty>())
        {
            var result = AnswerMatcher.Match(Norm(spoken), CarAndSword, difficulty);

            Assert.Equal(new MatchResult("car", MatchType.Alias, 0, false, 1.0), result);
        }
    }

    [Fact]
    public void CocheCarroAndAutoAllResolveToOneAnswer()
    {
        var ids = new[] { "coche", "carro", "auto" }
            .Select(word => AnswerMatcher.Match(Norm(word), CarAndSword, Difficulty.Intermediate).AnswerId)
            .Distinct()
            .ToArray();

        Assert.Equal("car", Assert.Single(ids));
    }

    [Theory]
    [InlineData("camion")]
    [InlineData("camión")]
    [InlineData("CAMIÓN")]
    public void AccentsAreIgnoredOnBothSides(string spoken)
    {
        var withAccent = Single("camión");
        var withoutAccent = Single("camion");

        Assert.Equal(MatchType.Exact, AnswerMatcher.Match(Norm(spoken), withAccent, Difficulty.Advanced).Type);
        Assert.Equal(MatchType.Exact, AnswerMatcher.Match(Norm(spoken), withoutAccent, Difficulty.Advanced).Type);
    }

    [Fact]
    public void ExactBeatsAliasEvenWhenAnotherAnswerListsTheAlias()
    {
        var set = new AnswerSet(
            "clash",
            [
                new Answer("a", "perro", ["can"], []),
                new Answer("b", "can", [], []),
            ]);

        var result = AnswerMatcher.Match(Norm("can"), set, Difficulty.Beginner);

        Assert.Equal(new MatchResult("b", MatchType.Exact, 0, false, 1.0), result);
    }

    [Theory]
    [InlineData(Difficulty.Beginner)]
    [InlineData(Difficulty.Intermediate)]
    [InlineData(Difficulty.Advanced)]
    public void EspadoMatchesEspadaFuzzyAtDistanceOneWhichIsExactlyTheBudget(Difficulty difficulty)
    {
        // Six characters allow one edit at every difficulty, so the match sits on the budget's edge and is still a match.
        var result = AnswerMatcher.Match(Norm("espado"), CarAndSword, difficulty);

        Assert.Equal(new MatchResult("sword", MatchType.Fuzzy, 1, false, 1.0 - (1.0 / 6.0)), result);
    }

    [Fact]
    public void DistanceInsideAndAtTheBudgetAreBothMatchesWithTheConfidenceOfTheirDistance()
    {
        var set = Single("caballero");

        var inside = AnswerMatcher.Match(Norm("cabalero"), set, Difficulty.Beginner);
        var atBudget = AnswerMatcher.Match(Norm("cabalero"), set, Difficulty.Advanced);

        Assert.Equal(MatchType.Fuzzy, inside.Type);
        Assert.Equal(1, inside.Distance);
        Assert.Equal(1.0 - (1.0 / 9.0), inside.Confidence);
        Assert.Equal(new MatchResult("only", MatchType.Fuzzy, 1, false, 1.0 - (1.0 / 9.0)), atBudget);
    }

    [Fact]
    public void ASoundAlikeOfAThreeLetterPhraseAtABudgetOfZeroIsConfident()
    {
        // Three characters at Beginner allow no edit, so a distance of 0 is exactly the budget's edge; the sound-alike is still a full-confidence match.
        var set = new AnswerSet("sayings", [new Answer("today", "hoy", [], [])]);

        var result = AnswerMatcher.Match(Norm("oy"), set, Difficulty.Beginner);

        Assert.Equal(new MatchResult("today", MatchType.Fuzzy, 0, true, 1.0), result);
    }

    [Fact]
    public void DistanceBeyondTheLimitIsNoMatch()
    {
        var set = Single("caballero");

        var beginner = AnswerMatcher.Match(Norm("caballo"), set, Difficulty.Beginner);
        var intermediate = AnswerMatcher.Match(Norm("caballo"), set, Difficulty.Intermediate);

        Assert.Equal(new MatchResult("only", MatchType.Fuzzy, 2, false, 1.0 - (2.0 / 9.0)), beginner);
        Assert.Equal(new MatchResult(null, MatchType.None, 2, false, 0.0), intermediate);
    }

    [Fact]
    public void MaxDistanceUsesLengthDividedByTheDifficultyDivisor()
    {
        Assert.Equal(0, AnswerMatcher.MaxDistance(3, Difficulty.Beginner));
        Assert.Equal(1, AnswerMatcher.MaxDistance(6, Difficulty.Beginner));
        Assert.Equal(2, AnswerMatcher.MaxDistance(9, Difficulty.Beginner));
        Assert.Equal(1, AnswerMatcher.MaxDistance(9, Difficulty.Intermediate));
        Assert.Equal(2, AnswerMatcher.MaxDistance(10, Difficulty.Intermediate));
        Assert.Equal(1, AnswerMatcher.MaxDistance(11, Difficulty.Advanced));
        Assert.Equal(2, AnswerMatcher.MaxDistance(12, Difficulty.Advanced));
    }

    [Fact]
    public void SpellingVariantsThatSoundAlikeAreLikelyPronunciationMatches()
    {
        var vaca = Single("vaca");

        var beginner = AnswerMatcher.Match(Norm("baca"), vaca, Difficulty.Beginner);
        var intermediate = AnswerMatcher.Match(Norm("baca"), vaca, Difficulty.Intermediate);

        Assert.Equal(new MatchResult("only", MatchType.Fuzzy, 0, true, 1.0), beginner);
        // Four characters at Intermediate allow no edit, so the sound-alike sits on the budget's edge and keeps full confidence.
        Assert.Equal(new MatchResult("only", MatchType.Fuzzy, 0, true, 1.0), intermediate);
    }

    [Fact]
    public void LongerSoundAlikeSpellingStaysWithinTheLimitEvenWhenSpellingDiffersALot()
    {
        var result = AnswerMatcher.Match(Norm("serbesa"), Single("cerveza"), Difficulty.Beginner);

        Assert.Equal(new MatchResult("only", MatchType.Fuzzy, 0, true, 1.0), result);
    }

    [Fact]
    public void ASpellingSlipThatChangesTheSoundIsNotALikelyPronunciationMatch()
    {
        var result = AnswerMatcher.Match(Norm("espado"), Single("espada"), Difficulty.Beginner);

        Assert.False(result.LikelyPronunciation);
    }

    [Theory]
    [InlineData("xyzzy")]
    [InlineData("quizás mañana")]
    [InlineData("")]
    [InlineData("¿?")]
    public void NonsenseAndEmptyInputMatchNothing(string spoken)
    {
        var result = AnswerMatcher.Match(Norm(spoken), CarAndSword, Difficulty.Beginner);

        Assert.Null(result.AnswerId);
        Assert.Equal(MatchType.None, result.Type);
        Assert.False(result.LikelyPronunciation);
        Assert.Equal(0.0, result.Confidence);
    }

    [Fact]
    public void AliasesTakePartInFuzzyMatching()
    {
        var result = AnswerMatcher.Match(Norm("carra"), CarAndSword, Difficulty.Beginner);

        // carro has five characters: one edit is the whole budget at Beginner, and the confidence is the distance's 1 - 1/5.
        Assert.Equal(new MatchResult("car", MatchType.Fuzzy, 1, false, 1.0 - (1.0 / 5.0)), result);
    }

    [Fact]
    public void FuzzyTiesGoToTheOrdinallySmallestAnswerId()
    {
        var set = new AnswerSet(
            "tie",
            [
                new Answer("zeta", "gato", [], []),
                new Answer("alfa", "pato", [], []),
                new Answer("beta", "rato", [], []),
            ]);

        var result = AnswerMatcher.Match(Norm("dato"), set, Difficulty.Beginner);

        Assert.Equal("alfa", result.AnswerId);
        Assert.Equal(MatchType.Fuzzy, result.Type);
        Assert.Equal(1, result.Distance);
    }

    [Fact]
    public void TheLowestDistanceBeatsTheSmallerAnswerId()
    {
        var set = new AnswerSet(
            "distance",
            [
                new Answer("a", "mariposa nocturnas", [], []),
                new Answer("b", "mariposa nocturna", [], []),
            ]);

        var result = AnswerMatcher.Match(Norm("mariposa nocturno"), set, Difficulty.Beginner);

        Assert.Equal("b", result.AnswerId);
        Assert.Equal(1, result.Distance);
    }

    [Fact]
    public void ExactTiesGoToTheOrdinallySmallestAnswerId()
    {
        var set = new AnswerSet(
            "dup",
            [
                new Answer("b", "casa", [], []),
                new Answer("a", "casa", [], []),
            ]);

        Assert.Equal("a", AnswerMatcher.Match(Norm("casa"), set, Difficulty.Beginner).AnswerId);
    }

    [Fact]
    public void NumbersMatchWhetherWrittenAsDigitsOrWords()
    {
        var set = new AnswerSet("numbers", [new Answer("n", "dos espadas", ["22 espadas"], [])]);

        Assert.Equal(MatchType.Exact, AnswerMatcher.Match(Norm("2 espadas"), set, Difficulty.Beginner).Type);
        Assert.Equal(MatchType.Exact, AnswerMatcher.Match(Norm("dos espadas"), set, Difficulty.Beginner).Type);
        Assert.Equal(MatchType.Alias, AnswerMatcher.Match(Norm("veintidós espadas"), set, Difficulty.Beginner).Type);
    }

    [Fact]
    public void SetPhrasesAreNormalizedWithTheSuppliedOptions()
    {
        var expand = new NormalizationOptions(ExpandContractions: true);
        var set = Single("voy al mercado");
        var spoken = Norm("voy al mercado", expand);

        var withMatchingOptions = AnswerMatcher.Match(spoken, set, Difficulty.Advanced, expand);
        var withDefaultOptions = AnswerMatcher.Match(spoken, set, Difficulty.Advanced);

        Assert.Equal(MatchType.Exact, withMatchingOptions.Type);
        Assert.NotEqual(MatchType.Exact, withDefaultOptions.Type);
    }

    [Fact]
    public void DialectTagsDoNotRestrictMatching()
    {
        var set = new AnswerSet("dialects", [new Answer("car", "coche", ["carro"], ["es-ES"])]);

        Assert.Equal(MatchType.Alias, AnswerMatcher.Match(Norm("carro"), set, Difficulty.Beginner).Type);
    }

    [Fact]
    public void EmptySetMatchesNothing()
    {
        var result = AnswerMatcher.Match(Norm("hola"), new AnswerSet("empty", []), Difficulty.Beginner);

        Assert.Equal(new MatchResult(null, MatchType.None, 0, false, 0.0), result);
    }

    [Fact]
    public void AnswerOrderInTheSetDoesNotChangeTheResult()
    {
        var reversed = new AnswerSet("shop", CarAndSword.Answers.Reverse().ToArray());

        foreach (var spoken in new[] { "coche", "auto", "espado", "carra", "xyzzy" })
        {
            Assert.Equal(
                AnswerMatcher.Match(Norm(spoken), CarAndSword, Difficulty.Beginner),
                AnswerMatcher.Match(Norm(spoken), reversed, Difficulty.Beginner));
        }
    }

    [Fact]
    public void IsDeterministic()
    {
        foreach (var difficulty in Enum.GetValues<Difficulty>())
        {
            foreach (var spoken in new[] { "coche", "carro", "espado", "baca", "xyzzy" })
            {
                var text = Norm(spoken);
                Assert.Equal(AnswerMatcher.Match(text, CarAndSword, difficulty), AnswerMatcher.Match(text, CarAndSword, difficulty));
            }
        }
    }

    [Fact]
    public void RejectsBadArguments()
    {
        var text = Norm("hola");

        Assert.Throws<ArgumentNullException>(() => AnswerMatcher.Match(null!, CarAndSword, Difficulty.Beginner));
        Assert.Throws<ArgumentNullException>(() => AnswerMatcher.Match(text, null!, Difficulty.Beginner));
        Assert.Throws<ArgumentOutOfRangeException>(() => AnswerMatcher.Match(text, CarAndSword, (Difficulty)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => AnswerMatcher.MaxDistance(-1, Difficulty.Beginner));
    }
}
