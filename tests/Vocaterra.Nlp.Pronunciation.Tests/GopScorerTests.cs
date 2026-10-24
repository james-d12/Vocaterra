using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Nlp.Pronunciation.Tests;

/// <summary>Tests for the v2 forced-alignment and GOP scorer.</summary>
public class GopScorerTests
{
    private static readonly GopScorer Scorer = new();

    private static PronunciationResult Run(
        string text,
        PhonePosteriors posteriors,
        Difficulty difficulty = Difficulty.Intermediate,
        bool allDialects = false)
    {
        var variants = allDialects ? TestSupport.Variants(text) : TestSupport.Primary(text);
        return Scorer.Score(posteriors, variants, TestSupport.Predicted(variants), difficulty, TestSupport.FakeHash);
    }

    [Fact]
    public void SyntheticMatrixGivesTheGoldenGopScores()
    {
        // a: both frames are won by a, gap 0. ɾ: one frame, ɾ .40 against o .50, gap ln(.8); tap/trill temperature 3.
        // o: frames with gaps 0 and ln(.8), mean ln(.8)/2; vowel temperature 2.
        var result = Scorer.Score(
            CtcForcedAlignerTests.SyntheticMatrix(),
            [TestSupport.Manual("a", "ɾ", "o")],
            [],
            Difficulty.Intermediate,
            TestSupport.FakeHash);

        Assert.Equal(1.0, result.Phones[0].Score, 6);
        Assert.Equal(0.928318, result.Phones[1].Score, 6);
        Assert.Equal(0.945742, result.Phones[2].Score, 6);
        Assert.Equal(0.958020, result.WordScore, 6);
        Assert.Equal(0.7, result.Confidence, 6);
        Assert.Equal(["a", "o", "o"], result.Phones.Select(static p => p.Actual));
        Assert.Equal(["a", "ɾ", "o"], result.Phones.Select(static p => p.Expected));
    }

    [Fact]
    public void GopMatchesTheFormulaForOneSegment()
    {
        var posteriors = CtcForcedAlignerTests.SyntheticMatrix();
        var segment = new PhoneSegment(2, "o", 4, 6);

        var gop = GopScorer.SegmentGop(posteriors, target: 3, segment);

        Assert.Equal(Math.Log(0.8) / 2, gop, 5);
        Assert.Equal(Math.Exp(gop / 2.0), GopCalibration.Default.ScoreOf("o", gop), 12);
    }

    [Fact]
    public void PerfectRenditionScoresOneEverywhereWithoutTags()
    {
        foreach (var text in new[] { "perro", "hola", "casa", "me llamo Pedro" })
        {
            var variants = TestSupport.Primary(text);
            var result = Run(text, TestSupport.Fake.FromPhones([.. variants[0].Phones]));

            Assert.All(result.Phones, p => Assert.Equal(1.0, p.Score));
            Assert.Empty(result.Errors);
            Assert.Equal(1.0, result.WordScore);
            Assert.Equal(1.0, result.Confidence);
        }
    }

    [Fact]
    public void EnglishRForTrillInPerroTagsPronROnTheRrSpan()
    {
        var posteriors = TestSupport.Fake.FromPhones(
            ["p", "e", "r", "o"],
            confusions: new Dictionary<string, string> { ["r"] = "ɹ" });

        var result = Run("perro", posteriors);

        var tag = Assert.Single(result.Errors);
        Assert.Equal(ErrorTags.PronR, tag.Tag);
        Assert.Equal(new TextSpan(2, 2), tag.Span);
        Assert.Equal("r", tag.Expected);
        Assert.Equal("ɹ", tag.Actual);

        // exp(-10 / 3) for a target at the floor while ɹ wins every frame.
        Assert.Equal(0.035674, result.Phones[2].Score, 6);
        Assert.Equal("ɹ", result.Phones[2].Actual);
        Assert.Equal(1.0, result.Phones[0].Score);
    }

    [Fact]
    public void LeadingHInHolaTagsPronSilentHAndLeavesTheNextPhoneAlone()
    {
        var result = Run("hola", TestSupport.Fake.FromPhones(TestSupport.Ipa("hola")));

        var tag = Assert.Single(result.Errors);
        Assert.Equal(ErrorTags.PronSilentH, tag.Tag);
        Assert.Equal(new TextSpan(0, 1), tag.Span);
        Assert.Equal("h", tag.Actual);
        Assert.Equal([1.0, 1.0, 1.0], result.Phones.Select(static p => p.Score));
    }

    [Fact]
    public void SchwaForUnstressedAInCasaTagsPronVowel()
    {
        var result = Run("casa", TestSupport.Fake.FromPhones(TestSupport.Ipa("kasə")));

        var tag = Assert.Single(result.Errors);
        Assert.Equal(ErrorTags.PronVowel, tag.Tag);
        Assert.Equal(new TextSpan(3, 1), tag.Span);
        Assert.Equal("ə", result.Phones[3].Actual);
        Assert.Equal(0.006738, result.Phones[3].Score, 6);
    }

    [Fact]
    public void MumbledFramesAreUncertainAndProduceNoTag()
    {
        // The final a of casa is half-swallowed: a keeps .2 and the schwa takes .8. The gap ln(.25) over temperature 2
        // gives a score of exactly .5, which is inside the intermediate vowel band .45 +- .10.
        var posteriors = TestSupport.Fake.FromMumbled(["k", "a", "s", "a"], mumbledIndex: 3, alternative: "ə", targetMass: 0.2);

        var intermediate = Run("casa", posteriors);

        Assert.Equal(0.5, intermediate.Phones[3].Score, 6);
        Assert.Equal("ə", intermediate.Phones[3].Actual);
        Assert.Equal(
            MarginDecision.Uncertain,
            PronunciationThresholds.Default.Evaluate("a", Difficulty.Intermediate, intermediate.Phones[3].Score));
        Assert.Empty(intermediate.Errors);

        // The same audio is fine for a beginner prompt (accept from .45) and still uncertain at advanced level.
        var beginner = Run("casa", posteriors, Difficulty.Beginner);
        Assert.Equal(MarginDecision.Accept, PronunciationThresholds.Default.Evaluate("a", Difficulty.Beginner, beginner.Phones[3].Score));
        Assert.Empty(beginner.Errors);
        Assert.Empty(Run("casa", posteriors, Difficulty.Advanced).Errors);
    }

    [Fact]
    public void ADominantCompetitorIsRejectedAndTagged()
    {
        // Target mass .05 against .95: score exp(ln(.05 / .95) / 2) = .2294, below the intermediate reject line of .35.
        var posteriors = TestSupport.Fake.FromMumbled(["k", "a", "s", "a"], mumbledIndex: 3, alternative: "ə", targetMass: 0.05);

        var result = Run("casa", posteriors);

        Assert.Equal(0.229416, result.Phones[3].Score, 5);
        var tag = Assert.Single(result.Errors);
        Assert.Equal(ErrorTags.PronVowel, tag.Tag);
    }

    [Fact]
    public void GreedyHasNoConfidenceAndTagsTheSameMumbledFrames()
    {
        // v1 only sees the argmax (the schwa), so it scores 0 and tags. This documents why GOP is the mode for margin-aware tagging.
        var posteriors = TestSupport.Fake.FromMumbled(["k", "a", "s", "a"], mumbledIndex: 3, alternative: "ə", targetMass: 0.2);
        var variants = TestSupport.Primary("casa");

        var result = new GreedyPhoneScorer().Score(posteriors, variants, TestSupport.Predicted(variants), Difficulty.Intermediate, TestSupport.FakeHash);

        Assert.Equal(0.0, result.Phones[3].Score);
        Assert.Single(result.Errors);
    }

    [Fact]
    public void ADeletedPhoneScoresLow()
    {
        // "peo" for "pero": the tap has no frames of its own, so the frame it must take belongs to a neighbour or to a blank.
        var posteriors = TestSupport.Fake.FromPhones(["p", "e", "o"]);

        var result = Run("pero", posteriors);

        Assert.Equal(1.0, result.Phones[0].Score);
        Assert.True(result.Phones[2].Score < 0.1);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void TheVariantWithTheHighestPathLikelihoodIsChosen()
    {
        var castilian = Run("zapato", TestSupport.Fake.FromPhones(TestSupport.Ipa("θapato")), allDialects: true);
        Assert.Equal("θ", castilian.Phones[0].Expected);
        Assert.Equal(1.0, castilian.WordScore);

        var latinAmerican = Run("zapato", TestSupport.Fake.FromPhones(TestSupport.Ipa("sapato")), allDialects: true);
        Assert.Equal("s", latinAmerican.Phones[0].Expected);
        Assert.Equal(1.0, latinAmerican.WordScore);
    }

    [Fact]
    public void ATargetThatIsTooLongFallsBackToV1WithLowerConfidence()
    {
        // Three frames cannot hold the four phones of "perro".
        var posteriors = TestSupport.Fake.FromPhones(["p", "e", "r"], framesPerPhone: 1, blankFramesBetween: 0);
        var variants = TestSupport.Primary("perro");
        var predicted = TestSupport.Predicted(variants);

        var gop = Scorer.Score(posteriors, variants, predicted, Difficulty.Intermediate, TestSupport.FakeHash);
        var greedy = new GreedyPhoneScorer().Score(posteriors, variants, predicted, Difficulty.Intermediate, TestSupport.FakeHash);

        Assert.Equal(greedy.Phones, gop.Phones);
        Assert.Equal(greedy.Errors, gop.Errors);
        Assert.Equal(greedy.WordScore, gop.WordScore);
        Assert.Equal(1.0, greedy.Confidence);
        Assert.Equal(0.75, gop.Confidence, 6);
    }

    [Fact]
    public void ATargetPhoneOutsideTheVocabularyFallsBackWithZeroConfidence()
    {
        var narrow = new FakePhonemeRecognizer(["p", "e", "r"]);
        var posteriors = narrow.FromPhones(["p", "e", "r"]);
        var variants = TestSupport.Primary("perro");

        var result = Scorer.Score(posteriors, variants, [], Difficulty.Intermediate, TestSupport.FakeHash);

        Assert.Equal(0.0, result.Confidence);
        Assert.Equal(["p", "e", "r", "o"], result.Phones.Select(static p => p.Expected));
        Assert.Equal(0.0, result.Phones[3].Score);
    }

    [Fact]
    public void LowerGopTemperaturesAreStricter()
    {
        var posteriors = CtcForcedAlignerTests.SyntheticMatrix();
        var strict = new GopScorer(calibration: GopCalibration.Default.With(PhoneClass.TapTrill, 0.5));

        var result = strict.Score(posteriors, [TestSupport.Manual("a", "ɾ", "o")], [], Difficulty.Intermediate, TestSupport.FakeHash);

        Assert.Equal(Math.Exp(Math.Log(0.8) / 0.5), result.Phones[1].Score, 5);
        Assert.True(result.Phones[1].Score < 0.928318);
    }

    [Fact]
    public void IdenticalInputsGiveCanonicalJsonIdenticalResults()
    {
        var variants = TestSupport.Variants("perro");
        var posteriors = TestSupport.Fake.FromPhones(TestSupport.Ipa("peɹo"));
        var predicted = TestSupport.Predicted(variants);

        var first = Scorer.Score(posteriors, variants, predicted, Difficulty.Intermediate, TestSupport.FakeHash);
        var second = Scorer.Score(posteriors, variants, predicted, Difficulty.Intermediate, TestSupport.FakeHash);

        Assert.Equal(CanonicalJson.Serialize(first), CanonicalJson.Serialize(second));
        Assert.Equal(first, second);
    }

    [Fact]
    public void InvalidInputsAreRejected()
    {
        var posteriors = TestSupport.Fake.FromPhones(["a"]);

        Assert.Throws<ArgumentException>(() => Scorer.Score(posteriors, [], [], Difficulty.Beginner, TestSupport.FakeHash));
        Assert.Throws<ArgumentNullException>(() => Scorer.Score(posteriors, TestSupport.Primary("a"), [], Difficulty.Beginner, null!));
    }
}
