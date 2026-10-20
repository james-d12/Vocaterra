using Slg.Core.Audio;
using Slg.Core.Stages;

namespace Slg.Pronunciation.Tests;

/// <summary>Tests for the fake phoneme recognizer.</summary>
public class FakePhonemeRecognizerTests
{
    [Fact]
    public void BlankIsColumnZero()
    {
        var fake = new FakePhonemeRecognizer(["a", "b"]);

        Assert.Equal([CtcDecoder.BlankSymbol, "a", "b"], fake.Vocabulary);
    }

    [Fact]
    public void FromPhonesLaysOutFramesWithBlanksBetween()
    {
        var fake = new FakePhonemeRecognizer(["a", "b"]);

        var posteriors = fake.FromPhones(["a", "b"]);

        // a a a blank b b b
        Assert.Equal(7, posteriors.FrameCount);
        Assert.Equal(0.02, posteriors.FrameSeconds);
        int[] expectedColumns = [1, 1, 1, 0, 2, 2, 2];
        for (var frame = 0; frame < posteriors.FrameCount; frame++)
        {
            for (var column = 0; column < posteriors.PhoneCount; column++)
            {
                var expected = column == expectedColumns[frame] ? 0f : -10f;
                Assert.Equal(expected, posteriors[frame, column]);
            }
        }
    }

    [Fact]
    public void FrameCountsAreConfigurable()
    {
        var fake = new FakePhonemeRecognizer(["a", "b"]);

        Assert.Equal(2 + 2, fake.FromPhones(["a", "b"], framesPerPhone: 2, blankFramesBetween: 0).FrameCount);
        Assert.Equal((2 * 2) + 3 + (2 * 4), fake.FromPhones(["a", "b"], framesPerPhone: 2, blankFramesBetween: 3, edgeBlankFrames: 4).FrameCount);
        Assert.Equal(0, fake.FromPhones([]).FrameCount);
    }

    [Fact]
    public void ConfusionReplacesEveryOccurrence()
    {
        var fake = TestSupport.Fake;

        var posteriors = fake.FromPhones(["p", "e", "r", "o"], confusions: new Dictionary<string, string> { ["r"] = "ɹ" });

        Assert.Equal(["p", "e", "ɹ", "o"], CtcDecoder.Greedy(posteriors));
    }

    [Fact]
    public void MumbledFramesShareMassBetweenTwoSymbols()
    {
        var fake = new FakePhonemeRecognizer(["a", "b"]);

        var posteriors = fake.FromMumbled(["a", "b"], mumbledIndex: 1, alternative: "a", targetMass: 0.25);

        // frames 4..6 hold b with probability .25 and a with probability .75; the other symbols stay at the floor.
        Assert.Equal((float)Math.Log(0.25), posteriors[4, 2], 5);
        Assert.Equal((float)Math.Log(0.75), posteriors[4, 1], 5);
        Assert.Equal(-10f, posteriors[4, 0]);
        Assert.Equal(["a", "a"], CtcDecoder.Greedy(posteriors));
    }

    [Fact]
    public void UnknownPhonesAndBadArgumentsAreRejected()
    {
        var fake = new FakePhonemeRecognizer(["a"]);

        Assert.Throws<ArgumentException>(() => fake.FromPhones(["z"]));
        Assert.Throws<ArgumentException>(() => fake.FromPhones([CtcDecoder.BlankSymbol]));
        Assert.Throws<ArgumentOutOfRangeException>(() => fake.FromPhones(["a"], framesPerPhone: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => fake.FromMumbled(["a"], 0, "a", 1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => fake.FromMumbled(["a"], 3, "a", 0.5));
        Assert.Throws<ArgumentException>(() => new FakePhonemeRecognizer(["a", "a"]));
        Assert.Throws<ArgumentException>(() => new FakePhonemeRecognizer([CtcDecoder.BlankSymbol]));
    }

    [Fact]
    public void RecognizeReplaysTheCannedPosteriorsAndReportsTheModelHash()
    {
        var fake = new FakePhonemeRecognizer(["a"], "hash-1");
        var clip = PcmClip.FromSamples(new short[160]);

        Assert.Equal("hash-1", fake.ModelHash);
        Assert.Throws<InvalidOperationException>(() => fake.Recognize(clip));

        var canned = fake.FromPhones(["a"]);
        Assert.Same(fake, fake.Returns(canned));
        Assert.Same(canned, fake.Recognize(clip));
    }

    [Fact]
    public void SpanishFakeCoversTheWholeInventory()
    {
        Assert.Contains("ɹ", TestSupport.Fake.Vocabulary);
        Assert.Contains("ə", TestSupport.Fake.Vocabulary);
        Assert.Equal(CtcDecoder.BlankSymbol, TestSupport.Fake.Vocabulary[0]);
    }
}
