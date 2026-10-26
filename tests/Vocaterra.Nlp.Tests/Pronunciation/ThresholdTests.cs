using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Phonology;

namespace Vocaterra.Nlp.Pronunciation.Tests;

/// <summary>Tests for phone classes, thresholds and GOP calibration.</summary>
public class ThresholdTests
{
    [Fact]
    public void ClassNamesAreTheDocumentedIdentifiers()
    {
        Assert.Equal(
            ["vowel", "tap_trill", "fricative", "stop", "nasal", "lateral", "glide"],
            PhoneClasses.All.Select(PhoneClasses.Name));
    }

    [Theory]
    [InlineData("a", PhoneClass.Vowel)]
    [InlineData("ə", PhoneClass.Vowel)]
    [InlineData("eɪ", PhoneClass.Vowel)]
    [InlineData("oʊ", PhoneClass.Vowel)]
    [InlineData("ɾ", PhoneClass.TapTrill)]
    [InlineData("r", PhoneClass.TapTrill)]
    [InlineData("ɹ", PhoneClass.TapTrill)]
    [InlineData("s", PhoneClass.Fricative)]
    [InlineData("h", PhoneClass.Fricative)]
    [InlineData("ð", PhoneClass.Fricative)]
    [InlineData("ʝ", PhoneClass.Fricative)]
    [InlineData("p", PhoneClass.Stop)]
    [InlineData("tʃ", PhoneClass.Stop)]
    [InlineData("n", PhoneClass.Nasal)]
    [InlineData("ɲ", PhoneClass.Nasal)]
    [InlineData("l", PhoneClass.Lateral)]
    [InlineData("ʎ", PhoneClass.Lateral)]
    [InlineData("j", PhoneClass.Glide)]
    [InlineData("w", PhoneClass.Glide)]
    [InlineData("unknown", PhoneClass.Stop)]
    public void PhonesBelongToTheirClass(string phone, PhoneClass expected)
    {
        Assert.Equal(expected, PhoneClasses.Of(phone));
    }

    [Fact]
    public void OnlyStopsFallIntoTheStopClass()
    {
        // Stops and the affricate are the members of the fallback class; every other inventory phone is named explicitly.
        foreach (var phone in PhoneInventory.All)
        {
            var phoneClass = PhoneClasses.Of(phone);
            if (phoneClass == PhoneClass.Stop)
            {
                Assert.Contains(phone, new[] { "p", "b", "t", "d", "k", "g", "tʃ" });
            }
        }
    }

    [Fact]
    public void ThresholdsGetStricterWithDifficulty()
    {
        foreach (var phoneClass in PhoneClasses.All)
        {
            var beginner = PronunciationThresholds.Default.Get(phoneClass, Difficulty.Beginner);
            var intermediate = PronunciationThresholds.Default.Get(phoneClass, Difficulty.Intermediate);
            var advanced = PronunciationThresholds.Default.Get(phoneClass, Difficulty.Advanced);

            Assert.True(beginner.Threshold < intermediate.Threshold);
            Assert.True(intermediate.Threshold < advanced.Threshold);
            Assert.Equal(0.10, beginner.Epsilon);
        }
    }

    [Fact]
    public void TapAndTrillAreTheMostForgivingClass()
    {
        foreach (var difficulty in Enum.GetValues<Difficulty>())
        {
            var tap = PronunciationThresholds.Default.Get(PhoneClass.TapTrill, difficulty).Threshold;
            Assert.All(
                PhoneClasses.All.Where(static c => c != PhoneClass.TapTrill),
                other => Assert.True(tap < PronunciationThresholds.Default.Get(other, difficulty).Threshold));
        }
    }

    [Fact]
    public void EvaluateUsesTheMarginOfThePhonesClass()
    {
        var thresholds = PronunciationThresholds.Default;

        // Intermediate vowel: .45 +- .10.
        Assert.Equal(MarginDecision.Accept, thresholds.Evaluate("a", Difficulty.Intermediate, 0.55));
        Assert.Equal(MarginDecision.Uncertain, thresholds.Evaluate("a", Difficulty.Intermediate, 0.50));
        Assert.Equal(MarginDecision.Reject, thresholds.Evaluate("a", Difficulty.Intermediate, 0.30));

        // Intermediate tap: .35 +- .10, so .30 is not a rejection there.
        Assert.Equal(MarginDecision.Uncertain, thresholds.Evaluate("ɾ", Difficulty.Intermediate, 0.30));
        Assert.Equal(MarginDecision.Reject, thresholds.Evaluate("ɾ", Difficulty.Intermediate, 0.20));
    }

    [Fact]
    public void WithReplacesOneMarginAndLeavesTheOriginalAlone()
    {
        var changed = PronunciationThresholds.Default.With(PhoneClass.Nasal, Difficulty.Advanced, new Margin(0.9, 0.05));

        Assert.Equal(new Margin(0.9, 0.05), changed.Get(PhoneClass.Nasal, Difficulty.Advanced));
        Assert.Equal(new Margin(0.55, 0.10), PronunciationThresholds.Default.Get(PhoneClass.Nasal, Difficulty.Advanced));
        Assert.Equal(PronunciationThresholds.Default.Get(PhoneClass.Nasal, Difficulty.Beginner), changed.Get(PhoneClass.Nasal, Difficulty.Beginner));
    }

    [Fact]
    public void OutOfRangeArgumentsAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PronunciationThresholds.Default.Get((PhoneClass)99, Difficulty.Beginner));
        Assert.Throws<ArgumentOutOfRangeException>(() => PronunciationThresholds.Default.Get(PhoneClass.Vowel, (Difficulty)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => PhoneClasses.Name((PhoneClass)99));
    }

    [Fact]
    public void GopScoreIsOneAtZeroAndDecaysWithTheGap()
    {
        var calibration = GopCalibration.Default;

        Assert.Equal(1.0, calibration.ScoreOf("a", 0.0));
        Assert.Equal(Math.Exp(-1.0), calibration.ScoreOf("a", -2.0), 12);
        Assert.Equal(Math.Exp(-2.0 / 3.0), calibration.ScoreOf("ɾ", -2.0), 12);
        Assert.True(calibration.ScoreOf("a", -3.0) < calibration.ScoreOf("a", -1.0));

        // A positive gap cannot occur, but it is clamped rather than scored above one.
        Assert.Equal(1.0, calibration.ScoreOf("a", 0.5));
    }

    [Fact]
    public void DefaultTemperaturesAreTheDocumentedValues()
    {
        var calibration = GopCalibration.Default;

        Assert.Equal(2.0, calibration.Temperature(PhoneClass.Vowel));
        Assert.Equal(3.0, calibration.Temperature(PhoneClass.TapTrill));
        Assert.Equal(2.5, calibration.Temperature(PhoneClass.Fricative));
        Assert.Equal(2.0, calibration.Temperature(PhoneClass.Stop));
        Assert.Equal(2.0, calibration.Temperature(PhoneClass.Nasal));
        Assert.Equal(2.5, calibration.Temperature(PhoneClass.Lateral));
        Assert.Equal(2.5, calibration.Temperature(PhoneClass.Glide));
    }

    [Fact]
    public void CalibrationRejectsBadTemperatures()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GopCalibration.Default.With(PhoneClass.Vowel, 0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => GopCalibration.Default.With(PhoneClass.Vowel, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => GopCalibration.Default.Temperature((PhoneClass)42));
        Assert.Equal(1.0, GopCalibration.Default.With(PhoneClass.Vowel, 1.0).Temperature(PhoneClass.Vowel));
    }
}
