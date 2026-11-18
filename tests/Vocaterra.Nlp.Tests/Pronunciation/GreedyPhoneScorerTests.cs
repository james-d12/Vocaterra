using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Phonology;

namespace Vocaterra.Nlp.Pronunciation.Tests;

/// <summary>Tests for the v1 greedy scorer and predicted-error tagging.</summary>
public class GreedyPhoneScorerTests
{
    private static readonly GreedyPhoneScorer Scorer = new();

    private static PronunciationResult Run(
        string text,
        string[] spoken,
        Difficulty difficulty = Difficulty.Intermediate,
        bool allDialects = false)
    {
        var variants = allDialects ? TestSupport.Variants(text) : TestSupport.Primary(text);
        return Scorer.Score(
            TestSupport.Fake.FromPhones(spoken),
            variants,
            TestSupport.Predicted(variants),
            difficulty,
            TestSupport.FakeHash);
    }

    [Fact]
    public void PerfectRenditionScoresOneEverywhereWithoutTags()
    {
        foreach (var text in new[] { "perro", "hola", "casa", "me llamo Pedro", "el teléfono" })
        {
            var variants = TestSupport.Primary(text);
            var result = Run(text, [.. variants[0].Phones]);

            Assert.All(result.Phones, p => Assert.Equal(1.0, p.Score));
            Assert.Empty(result.Errors);
            Assert.Equal(1.0, result.WordScore);
            Assert.Equal(1.0, result.Confidence);
            Assert.Equal(TestSupport.FakeHash, result.ModelHash);
            Assert.Equal(variants[0].Phones.Count, result.Phones.Count);
        }
    }

    [Fact]
    public void PhoneScoresCarryExpectedActualAndTheGraphemeSpan()
    {
        var result = Run("perro", [.. TestSupport.Primary("perro")[0].Phones]);

        Assert.Equal(
            ["p", "e", "r", "o"],
            result.Phones.Select(static p => p.Expected));
        Assert.Equal(
            ["p", "e", "r", "o"],
            result.Phones.Select(static p => p.Actual));
        Assert.Equal(new TextSpan(0, 1), result.Phones[0].GraphemeSpan);
        Assert.Equal(new TextSpan(2, 2), result.Phones[2].GraphemeSpan);
    }

    [Fact]
    public void EnglishRForTrillInPerroTagsPronROnTheRrSpan()
    {
        var result = Run("perro", TestSupport.Ipa("peɹo"));

        var tag = Assert.Single(result.Errors);
        Assert.Equal(ErrorTags.PronR, tag.Tag);
        Assert.Equal(new TextSpan(2, 2), tag.Span);
        Assert.Equal("r", tag.Expected);
        Assert.Equal("ɹ", tag.Actual);
        Assert.Equal(0.0, result.Phones[2].Score);
        Assert.Equal("ɹ", result.Phones[2].Actual);
        Assert.Equal(0.75, result.WordScore);
    }

    [Fact]
    public void EnglishRForTapInPeroTagsPronR()
    {
        var result = Run("pero", TestSupport.Ipa("peɹo"));

        var tag = Assert.Single(result.Errors);
        Assert.Equal(ErrorTags.PronR, tag.Tag);
        Assert.Equal(new TextSpan(2, 1), tag.Span);
        Assert.Equal("ɾ", tag.Expected);
    }

    [Fact]
    public void LeadingHInHolaTagsPronSilentH()
    {
        var result = Run("hola", TestSupport.Ipa("hola"));

        var tag = Assert.Single(result.Errors);
        Assert.Equal(ErrorTags.PronSilentH, tag.Tag);
        Assert.Equal(new TextSpan(0, 1), tag.Span);
        Assert.Equal(string.Empty, tag.Expected);
        Assert.Equal("h", tag.Actual);

        // The extra phone takes half a point off the phone after it.
        Assert.Equal([0.5, 1.0, 1.0], result.Phones.Select(static p => p.Score));
        Assert.Equal(0.833333, result.WordScore, 6);
    }

    [Fact]
    public void SchwaForUnstressedAInCasaTagsPronVowel()
    {
        var result = Run("casa", TestSupport.Ipa("kasə"));

        var tag = Assert.Single(result.Errors);
        Assert.Equal(ErrorTags.PronVowel, tag.Tag);
        Assert.Equal(new TextSpan(3, 1), tag.Span);
        Assert.Equal("a", tag.Expected);
        Assert.Equal("ə", tag.Actual);
    }

    [Fact]
    public void DiphthongizedEAndOTagPronVowel()
    {
        var result = Run("pero", ["p", "eɪ", "ɾ", "o"]);

        var tag = Assert.Single(result.Errors);
        Assert.Equal(ErrorTags.PronVowel, tag.Tag);
        Assert.Equal("e", tag.Expected);
        Assert.Equal("eɪ", tag.Actual);

        var o = Run("pero", ["p", "e", "ɾ", "oʊ"]);
        Assert.Equal("oʊ", Assert.Single(o.Errors).Actual);
    }

    [Fact]
    public void SeveralErrorsAreOrderedByPositionInTheText()
    {
        // "perro" + "hola" together: ɹ for rr and an h before the o of "hola".
        var result = Run("perro hola", TestSupport.Ipa("peɹohola"));

        Assert.Equal(["pron.r@2+2", "pron.silent_h@6+1"], TestSupport.Describe(result.Errors));
    }

    [Fact]
    public void OneExtraHBetweenTwoSilentHsIsOneTagAtTheFirstWord()
    {
        // "ah hambrientos": the h at the end of "ah" and the h at the start of "hambrientos" are the same site; the scorer cannot hear which.
        var reference = TestSupport.Primary("ah hambrientos")[0].Phones;
        Assert.Equal(["pron.silent_h@1+1"], TestSupport.Describe(RunHeard("ah hambrientos", reference, 1, ["h"]).Errors));
    }

    [Fact]
    public void TwoExtraHsAtTheSameSiteAreTwoTagsOneAtEachWord()
    {
        var reference = TestSupport.Primary("ah hambrientos")[0].Phones;
        Assert.Equal(["pron.silent_h@1+1", "pron.silent_h@3+1"], TestSupport.Describe(RunHeard("ah hambrientos", reference, 1, ["h", "h"]).Errors));
    }

    [Fact]
    public void AnExtraHAwayFromTheSharedSiteIsTaggedAtItsOwnWord()
    {
        // The h of "hambrientos" alone, with a vowel-starting word before it that has no silent h: no ambiguity.
        var reference = TestSupport.Primary("el hambre")[0].Phones;
        Assert.Equal(["pron.silent_h@3+1"], TestSupport.Describe(RunHeard("el hambre", reference, 2, ["h"]).Errors));
    }

    [Fact]
    public void ASilentHBeforeAVowelWordAndASingleSilentHWordEachGiveOneTag()
    {
        var ah = TestSupport.Primary("ah amigo")[0].Phones;
        Assert.Equal(["pron.silent_h@1+1"], TestSupport.Describe(RunHeard("ah amigo", ah, 1, ["h"]).Errors));

        var hola = TestSupport.Primary("hola")[0].Phones;
        Assert.Equal(["pron.silent_h@0+1"], TestSupport.Describe(RunHeard("hola", hola, 0, ["h"]).Errors));
    }

    private static PronunciationResult RunHeard(string text, IReadOnlyList<string> reference, int at, string[] extra)
    {
        var heard = reference.Take(at).Concat(extra).Concat(reference.Skip(at)).ToArray();
        return Run(text, heard);
    }

    [Fact]
    public void ADifferentSubstitutionLowersTheScoreButIsNotTagged()
    {
        var result = Run("perro", TestSupport.Ipa("pelo"));

        Assert.Empty(result.Errors);
        Assert.Equal(0.0, result.Phones[2].Score);
        Assert.Equal("l", result.Phones[2].Actual);
    }

    [Fact]
    public void DeletionScoresZeroWithAnEmptyActual()
    {
        var result = Run("perro", TestSupport.Ipa("peo"));

        Assert.Equal(0.0, result.Phones[2].Score);
        Assert.Equal(string.Empty, result.Phones[2].Actual);
        Assert.Empty(result.Errors);
        Assert.Equal(0.75, result.WordScore);
    }

    [Fact]
    public void InsertionPenaltyHitsTheNextPhoneAndIsFlooredAtZero()
    {
        var reference = new G2pResult(["a", "b"], [new SyllableAlignment(new TextSpan(0, 2), 0, 2, true)], 0, DialectConfig.LatinAmericanNeutral);

        var one = GreedyPhoneScorer.BuildEvidence(reference, ["x", "a", "b"]);
        Assert.Equal([0.5, 1.0], one.Phones.Select(static p => p.Score));

        var two = GreedyPhoneScorer.BuildEvidence(reference, ["x", "y", "a", "b"]);
        Assert.Equal([0.0, 1.0], two.Phones.Select(static p => p.Score));

        var three = GreedyPhoneScorer.BuildEvidence(reference, ["x", "y", "z", "a", "b"]);
        Assert.Equal([0.0, 1.0], three.Phones.Select(static p => p.Score));
    }

    [Fact]
    public void AnExtraPhoneAtTheEndPenalizesTheLastPhone()
    {
        var reference = new G2pResult(["a", "b"], [new SyllableAlignment(new TextSpan(0, 2), 0, 2, true)], 0, DialectConfig.LatinAmericanNeutral);

        var evidence = GreedyPhoneScorer.BuildEvidence(reference, ["a", "b", "x"]);

        Assert.Equal([1.0, 0.5], evidence.Phones.Select(static p => p.Score));
        Assert.Equal([new InsertedPhone(2, "x")], evidence.Insertions);
        Assert.Equal(0.75, evidence.MeanScore);
    }

    [Fact]
    public void TheVariantWithTheHighestMeanScoreIsChosen()
    {
        var variants = TestSupport.Variants("zapato");
        Assert.True(variants.Count >= 2);

        var latinAmerican = Run("zapato", TestSupport.Ipa("sapato"), allDialects: true);
        Assert.Equal("s", latinAmerican.Phones[0].Expected);
        Assert.Equal(1.0, latinAmerican.WordScore);

        var castilian = Run("zapato", TestSupport.Ipa("θapato"), allDialects: true);
        Assert.Equal("θ", castilian.Phones[0].Expected);
        Assert.Equal(1.0, castilian.WordScore);
    }

    [Fact]
    public void AnExactTieKeepsTheFirstVariant()
    {
        // Neither "s" nor "θ" is spoken, so both variants score the same and the primary one wins.
        var result = Run("zapato", TestSupport.Ipa("tapato"), allDialects: true);

        Assert.Equal("s", result.Phones[0].Expected);
    }

    [Fact]
    public void PredictedErrorsFromAnotherDialectStillApplyToTheChosenVariant()
    {
        // A Castilian speaker who says an English r in "zapatero".
        var variants = TestSupport.Variants("zapatero");
        var result = Scorer.Score(
            TestSupport.Fake.FromPhones(TestSupport.Ipa("θapateɹo")),
            variants,
            TestSupport.Predicted(variants),
            Difficulty.Intermediate,
            TestSupport.FakeHash);

        Assert.Equal("θ", result.Phones[0].Expected);
        var tag = Assert.Single(result.Errors);
        Assert.Equal(ErrorTags.PronR, tag.Tag);
    }

    [Fact]
    public void StressVariantsAreSkippedInV1()
    {
        var variants = TestSupport.Primary("teléfono");
        var predicted = TestSupport.Predicted(variants);
        Assert.Contains(predicted, static p => p.ErrorTag == ErrorTags.PronStress);

        var result = Scorer.Score(
            TestSupport.Fake.FromPhones([.. variants[0].Phones]),
            variants,
            predicted,
            Difficulty.Intermediate,
            TestSupport.FakeHash);

        Assert.DoesNotContain(result.Errors, static e => e.Tag == ErrorTags.PronStress);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void AnUncertainSiteIsNotTagged()
    {
        // A threshold of .5 +- .6 puts every score, including the substitution's 0, inside the uncertain band.
        var lenient = PronunciationThresholds.Default.With(PhoneClass.TapTrill, Difficulty.Intermediate, new Vocaterra.Nlp.Core.Determinism.Margin(0.5, 0.6));
        var variants = TestSupport.Primary("perro");

        var result = new GreedyPhoneScorer(lenient).Score(
            TestSupport.Fake.FromPhones(TestSupport.Ipa("peɹo")),
            variants,
            TestSupport.Predicted(variants),
            Difficulty.Intermediate,
            TestSupport.FakeHash);

        Assert.Empty(result.Errors);
        Assert.Equal(0.0, result.Phones[2].Score);
    }

    [Fact]
    public void ConfidenceIsTheMeanOfTheFrameMaximaAsProbabilities()
    {
        // Two frames of probability 1 and two of probability 0.25 -> mean 0.625.
        var posteriors = TestSupport.Matrix(
            ["a"],
            [[0.25, 0.25], [0.25, 0.25], [1.0, 1e-9], [1e-9, 1.0]]);
        var variants = (IReadOnlyList<G2pResult>)[new G2pResult(["a"], [new SyllableAlignment(new TextSpan(0, 1), 0, 1, true)], 0, DialectConfig.LatinAmericanNeutral)];

        var result = Scorer.Score(posteriors, variants, [], Difficulty.Beginner, TestSupport.FakeHash);

        Assert.Equal(0.625, result.Confidence, 6);
    }

    [Fact]
    public void NoFramesGivesAnEmptyHypothesisAndZeroConfidence()
    {
        var variants = TestSupport.Primary("perro");

        var result = Scorer.Score(TestSupport.Fake.FromPhones([]), variants, [], Difficulty.Beginner, TestSupport.FakeHash);

        Assert.All(result.Phones, p => Assert.Equal(0.0, p.Score));
        Assert.Equal(0.0, result.WordScore);
        Assert.Equal(0.0, result.Confidence);
    }

    [Fact]
    public void TextWithoutPhonesScoresZeroAndKeepsNoPhoneRows()
    {
        var variants = TestSupport.Primary("123");
        Assert.Empty(variants[0].Phones);

        var result = Scorer.Score(TestSupport.Fake.FromPhones(TestSupport.Ipa("a")), variants, [], Difficulty.Beginner, TestSupport.FakeHash);

        Assert.Empty(result.Phones);
        Assert.Equal(0.0, result.WordScore);
    }

    [Fact]
    public void SpansFallBackToTheSyllableWhenThePhoneSpansAreMissing()
    {
        var handBuilt = new G2pResult(
            ["k", "a", "s", "a"],
            [new SyllableAlignment(new TextSpan(0, 2), 0, 2, true), new SyllableAlignment(new TextSpan(2, 2), 2, 2, false)],
            0,
            DialectConfig.LatinAmericanNeutral);

        var result = Scorer.Score(
            TestSupport.Fake.FromPhones(["k", "a", "s", "a"]),
            [handBuilt],
            [],
            Difficulty.Beginner,
            TestSupport.FakeHash);

        Assert.Equal(
            [new TextSpan(0, 2), new TextSpan(0, 2), new TextSpan(2, 2), new TextSpan(2, 2)],
            result.Phones.Select(static p => p.GraphemeSpan));
    }

    [Fact]
    public void InvalidInputsAreRejected()
    {
        var posteriors = TestSupport.Fake.FromPhones(["a"]);
        var variants = TestSupport.Primary("a");

        Assert.Throws<ArgumentException>(() => Scorer.Score(posteriors, [], [], Difficulty.Beginner, TestSupport.FakeHash));
        Assert.Throws<ArgumentNullException>(() => Scorer.Score(null!, variants, [], Difficulty.Beginner, TestSupport.FakeHash));
        Assert.Throws<ArgumentException>(() => Scorer.Score(new PhonePosteriors(["a"], [0f], 1, 0.02), variants, [], Difficulty.Beginner, TestSupport.FakeHash));
    }
}
