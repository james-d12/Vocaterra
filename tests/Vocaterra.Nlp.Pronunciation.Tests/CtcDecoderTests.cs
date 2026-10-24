using Vocaterra.Nlp.Core.Stages;

namespace Vocaterra.Nlp.Pronunciation.Tests;

/// <summary>Tests for greedy CTC decoding.</summary>
public class CtcDecoderTests
{
    private static readonly string[] Vocabulary = [CtcDecoder.BlankSymbol, "a", "b"];

    [Fact]
    public void GreedyCollapsesRepeatsAndDropsBlanks()
    {
        // blank a a blank a b b -> a (run of two collapses) then a (after a blank) then b
        var posteriors = TestSupport.Hot(Vocabulary, [0, 1, 1, 0, 1, 2, 2]);

        Assert.Equal(["a", "a", "b"], CtcDecoder.Greedy(posteriors));
    }

    [Fact]
    public void GreedyOfOnlyBlanksIsEmpty()
    {
        Assert.Empty(CtcDecoder.Greedy(TestSupport.Hot(Vocabulary, [0, 0, 0])));
    }

    [Fact]
    public void GreedyOfNoFramesIsEmpty()
    {
        Assert.Empty(CtcDecoder.Greedy(TestSupport.Hot(Vocabulary, [])));
    }

    [Fact]
    public void RepeatedPhoneWithoutBlankCollapsesToOne()
    {
        Assert.Equal(["a"], CtcDecoder.Greedy(TestSupport.Hot(Vocabulary, [1, 1, 1, 1])));
        Assert.Equal(["a", "a"], CtcDecoder.Greedy(TestSupport.Hot(Vocabulary, [1, 0, 1])));
    }

    [Fact]
    public void GreedyTakesTheLowestIndexOnAnExactTie()
    {
        var tie = new PhonePosteriors(Vocabulary, [-1f, -1f, -1f], 1, 0.02);

        Assert.Empty(CtcDecoder.Greedy(tie));
    }

    [Fact]
    public void GreedyUsesTheBlankSymbolWhereverItSits()
    {
        string[] vocabulary = ["a", CtcDecoder.BlankSymbol, "b"];

        Assert.Equal(["b"], CtcDecoder.Greedy(TestSupport.Hot(vocabulary, [1, 2, 1])));
    }

    [Fact]
    public void GreedyRejectsAVocabularyWithoutBlank()
    {
        var posteriors = new PhonePosteriors(["a", "b"], [0f, -1f], 1, 0.02);

        Assert.Throws<ArgumentException>(() => CtcDecoder.Greedy(posteriors));
    }

    [Fact]
    public void FakePerfectRenditionDecodesToItsPhones()
    {
        var phones = TestSupport.Ipa("pero");

        Assert.Equal(phones, CtcDecoder.Greedy(TestSupport.Fake.FromPhones(phones)));
    }

    [Fact]
    public void FakeRepeatedPhonesSurviveWithABlankBetween()
    {
        string[] phones = ["l", "e", "e", "ɾ"];

        Assert.Equal(phones, CtcDecoder.Greedy(TestSupport.Fake.FromPhones(phones)));
        Assert.Equal(["l", "e", "ɾ"], CtcDecoder.Greedy(TestSupport.Fake.FromPhones(phones, blankFramesBetween: 0)));
    }
}
