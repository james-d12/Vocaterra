using Vocaterra.Nlp.Core.Stages;

namespace Vocaterra.Nlp.Pronunciation.Tests;

/// <summary>Golden tests for the CTC Viterbi forced alignment.</summary>
public class CtcForcedAlignerTests
{
    /// <summary>Six frames over blank, a, ɾ, o; the best path for [a, ɾ, o] is a a _ ɾ o o.</summary>
    internal static PhonePosteriors SyntheticMatrix()
    {
        return TestSupport.Matrix(
            ["a", "ɾ", "o"],
            [
                [0.05, 0.90, 0.03, 0.02],
                [0.10, 0.80, 0.05, 0.05],
                [0.70, 0.10, 0.10, 0.10],
                [0.05, 0.05, 0.40, 0.50],
                [0.10, 0.05, 0.05, 0.80],
                [0.05, 0.05, 0.50, 0.40],
            ]);
    }

    [Fact]
    public void SyntheticMatrixSegmentsAreTheGoldenValues()
    {
        var alignment = CtcForcedAligner.Align(SyntheticMatrix(), ["a", "ɾ", "o"]);

        Assert.NotNull(alignment);
        Assert.Equal(
            [
                new PhoneSegment(0, "a", 0, 2),
                new PhoneSegment(1, "ɾ", 3, 4),
                new PhoneSegment(2, "o", 4, 6),
            ],
            alignment.Segments);
        Assert.Equal(-2.7409040259732884, alignment.LogLikelihood, 4);
    }

    [Fact]
    public void FakePosteriorsAlignToTheirOwnFrames()
    {
        // a a a _ b b b _ c c c
        var fake = new FakePhonemeRecognizer(["a", "b", "c"]);

        var alignment = CtcForcedAligner.Align(fake.FromPhones(["a", "b", "c"]), ["a", "b", "c"]);

        Assert.NotNull(alignment);
        Assert.Equal(
            [new PhoneSegment(0, "a", 0, 3), new PhoneSegment(1, "b", 4, 7), new PhoneSegment(2, "c", 8, 11)],
            alignment.Segments);
        Assert.Equal(0.0, alignment.LogLikelihood, 6);
        Assert.Equal(3, alignment.Segments[0].FrameCount);
    }

    [Fact]
    public void BlanksAbsorbFramesTheTargetDoesNotExplain()
    {
        // The speaker says h o l a but the target is o l a: the h frames and the blank after them must not
        // be given to the o, so the o segment holds only its own three frames.
        var fake = TestSupport.Fake;

        var alignment = CtcForcedAligner.Align(fake.FromPhones(["h", "o", "l", "a"]), ["o", "l", "a"]);

        Assert.NotNull(alignment);
        Assert.Equal(new PhoneSegment(0, "o", 4, 7), alignment.Segments[0]);
        Assert.Equal(new PhoneSegment(1, "l", 8, 11), alignment.Segments[1]);
        Assert.Equal(new PhoneSegment(2, "a", 12, 15), alignment.Segments[2]);
    }

    [Fact]
    public void TrailingFramesGoToTheBlankAsWell()
    {
        var alignment = CtcForcedAligner.Align(TestSupport.Fake.FromPhones(["o", "l", "a", "s"]), ["o", "l", "a"]);

        Assert.NotNull(alignment);
        Assert.Equal(new PhoneSegment(2, "a", 8, 11), alignment.Segments[2]);
    }

    [Fact]
    public void EqualAdjacentPhonesNeedABlankFrame()
    {
        Assert.Equal(6, CtcForcedAligner.MinimumFrames(["a", "a", "b", "b"]));
        Assert.Equal(2, CtcForcedAligner.MinimumFrames(["a", "b"]));
        Assert.Equal(3, CtcForcedAligner.MinimumFrames(["a", "a"]));

        var fake = new FakePhonemeRecognizer(["a"]);
        var twoFrames = fake.FromPhones(["a", "a"], framesPerPhone: 1, blankFramesBetween: 0);
        Assert.Equal(2, twoFrames.FrameCount);
        Assert.Null(CtcForcedAligner.Align(twoFrames, ["a", "a"]));

        var threeFrames = fake.FromPhones(["a", "a"], framesPerPhone: 1, blankFramesBetween: 1);
        var alignment = CtcForcedAligner.Align(threeFrames, ["a", "a"]);
        Assert.NotNull(alignment);
        Assert.Equal([new PhoneSegment(0, "a", 0, 1), new PhoneSegment(1, "a", 2, 3)], alignment.Segments);
    }

    [Fact]
    public void DifferentAdjacentPhonesMayFollowEachOtherDirectly()
    {
        var fake = new FakePhonemeRecognizer(["a", "b"]);
        var posteriors = fake.FromPhones(["a", "b"], framesPerPhone: 1, blankFramesBetween: 0);

        var alignment = CtcForcedAligner.Align(posteriors, ["a", "b"]);

        Assert.NotNull(alignment);
        Assert.Equal([new PhoneSegment(0, "a", 0, 1), new PhoneSegment(1, "b", 1, 2)], alignment.Segments);
    }

    [Fact]
    public void TargetsThatCannotBeAlignedReturnNull()
    {
        var fake = new FakePhonemeRecognizer(["a", "b"]);
        var posteriors = fake.FromPhones(["a", "b"], framesPerPhone: 1, blankFramesBetween: 0);

        Assert.Null(CtcForcedAligner.Align(posteriors, ["a", "b", "a"]));
        Assert.Null(CtcForcedAligner.Align(posteriors, ["a", "z"]));
        Assert.Null(CtcForcedAligner.Align(posteriors, [CtcDecoder.BlankSymbol]));
    }

    [Fact]
    public void AnEmptyTargetAlignsToNothing()
    {
        var alignment = CtcForcedAligner.Align(new FakePhonemeRecognizer(["a"]).FromPhones(["a"]), []);

        Assert.NotNull(alignment);
        Assert.Empty(alignment.Segments);
    }

    [Fact]
    public void AlignmentIsRepeatableAndValueEqual()
    {
        var first = CtcForcedAligner.Align(SyntheticMatrix(), ["a", "ɾ", "o"]);
        var second = CtcForcedAligner.Align(SyntheticMatrix(), ["a", "ɾ", "o"]);

        Assert.Equal(first, second);
        Assert.Equal(first!.GetHashCode(), second!.GetHashCode());
    }

    [Fact]
    public void EveryPhoneGetsAtLeastOneFrameEvenWhenTheEvidenceIsFlat()
    {
        // Flat posteriors: every path ties. The segments must still be non-empty, ordered and inside the clip.
        string[] vocabulary = [CtcDecoder.BlankSymbol, "a", "b", "c"];
        var flat = new PhonePosteriors(vocabulary, Enumerable.Repeat(-1.5f, 8 * 4).ToArray(), 8, 0.02);

        var alignment = CtcForcedAligner.Align(flat, ["a", "b", "c"]);

        Assert.NotNull(alignment);
        var previousEnd = 0;
        foreach (var segment in alignment.Segments)
        {
            Assert.True(segment.FrameCount >= 1);
            Assert.True(segment.StartFrame >= previousEnd);
            previousEnd = segment.EndFrame;
        }

        Assert.True(previousEnd <= 8);
    }
}
