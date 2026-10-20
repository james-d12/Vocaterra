using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Phonology;

namespace Vocaterra.Nlp.Pronunciation.Tests;

/// <summary>Tests for the scorer facade and for determinism across the whole scoring path.</summary>
public class PronunciationScorerTests
{
    private static readonly PronunciationScorer Scorer = new();

    [Theory]
    [InlineData(ScoringMode.Greedy)]
    [InlineData(ScoringMode.Gop)]
    public void IdenticalInputsGiveCanonicalJsonIdenticalResults(ScoringMode mode)
    {
        var variants = TestSupport.Variants("hola, me llamo Pedro");
        var predicted = TestSupport.Predicted(variants);
        var posteriors = TestSupport.Fake.FromPhones(TestSupport.Ipa("holameʝamopeðɾo"));

        var first = Scorer.Score(posteriors, variants, predicted, Difficulty.Intermediate, mode, TestSupport.FakeHash);
        var second = new PronunciationScorer().Score(
            TestSupport.Fake.FromPhones(TestSupport.Ipa("holameʝamopeðɾo")),
            TestSupport.Variants("hola, me llamo Pedro"),
            TestSupport.Predicted(TestSupport.Variants("hola, me llamo Pedro")),
            Difficulty.Intermediate,
            mode,
            TestSupport.FakeHash);

        Assert.Equal(CanonicalJson.Serialize(first), CanonicalJson.Serialize(second));
        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void ResultsSurviveACanonicalJsonRoundTrip()
    {
        var variants = TestSupport.Variants("perro");
        var result = Scorer.Score(
            TestSupport.Fake.FromPhones(TestSupport.Ipa("peɹo")),
            variants,
            TestSupport.Predicted(variants),
            Difficulty.Intermediate,
            ScoringMode.Gop,
            TestSupport.FakeHash);

        var json = CanonicalJson.Serialize(result);

        Assert.Equal(result, CanonicalJson.Deserialize<PronunciationResult>(json));
    }

    [Fact]
    public void EachModeRunsItsOwnScorer()
    {
        var variants = TestSupport.Primary("perro");
        var predicted = TestSupport.Predicted(variants);
        var posteriors = TestSupport.Fake.FromPhones(TestSupport.Ipa("peɹo"));

        var greedy = Scorer.Score(posteriors, variants, predicted, Difficulty.Intermediate, ScoringMode.Greedy, TestSupport.FakeHash);
        var gop = Scorer.Score(posteriors, variants, predicted, Difficulty.Intermediate, ScoringMode.Gop, TestSupport.FakeHash);

        Assert.Equal(0.0, greedy.Phones[2].Score);
        Assert.Equal(0.035674, gop.Phones[2].Score, 6);
        Assert.Equal(ErrorTags.PronR, Assert.Single(greedy.Errors).Tag);
        Assert.Equal(ErrorTags.PronR, Assert.Single(gop.Errors).Tag);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Scorer.Score(posteriors, variants, predicted, Difficulty.Intermediate, (ScoringMode)99, TestSupport.FakeHash));
    }

    [Fact]
    public void ScoringARecognizerOutputUsesItsModelHash()
    {
        var variants = TestSupport.Primary("perro");
        var fake = new FakePhonemeRecognizer(PhoneInventory.All, "fake-hash-7");
        fake.Returns(fake.FromPhones([.. variants[0].Phones]));
        var clip = PcmClip.FromSamples(new short[1600]);

        var result = Scorer.Score(clip, fake, variants, [], Difficulty.Beginner, ScoringMode.Greedy);

        Assert.Equal("fake-hash-7", result.ModelHash);
        Assert.Equal(1.0, result.WordScore);
    }

    [Fact]
    public void PredictedErrorsForIsTheDistinctUnionAcrossVariants()
    {
        var variants = TestSupport.Variants("zapato");
        Assert.True(variants.Count >= 2);

        var union = PronunciationScorer.PredictedErrorsFor(variants);

        Assert.Equal(union.Count, union.Distinct().Count());
        foreach (var variant in variants)
        {
            Assert.All(PredictedErrorVariants.Generate(variant), p => Assert.Contains(p, union));
        }

        Assert.Empty(PronunciationScorer.PredictedErrorsFor([]));
    }
}
