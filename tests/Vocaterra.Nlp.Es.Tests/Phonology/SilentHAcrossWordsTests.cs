using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Pronunciation;

namespace Vocaterra.Nlp.Es.Tests.Phonology;

/// <summary>
/// M92: one heard <c>h</c> is blamed on at most one silent-h letter. When two silent h letters meet across a word boundary
/// ("ah humana"), inserting an h at either gives the same phone sequence, so the scorer cannot tell them apart and must report one.
/// </summary>
public class SilentHAcrossWordsTests
{
    private static IReadOnlyList<ErrorTag> ScoreHeard(string text, IReadOnlyList<string> heard, ScoringMode mode)
    {
        var variants = SpanishG2p.PhonemizeAll(text);
        var predicted = PronunciationScorer.PredictedErrorsFor(variants);
        var posteriors = FakePhonemeRecognizer.ForPhoneInventory().FromPhones(heard, 3, 1, edgeBlankFrames: 0);
        return new PronunciationScorer().Score(posteriors, variants, predicted, Difficulty.Advanced, mode, "m92").Errors;
    }

    private static List<string> WithH(string text, int index)
    {
        var phones = SpanishG2p.PhonemizeAll(text)[0].Phones.ToList();
        phones.Insert(index, PhoneInventory.H);
        return phones;
    }

    [Theory]
    [InlineData(ScoringMode.Greedy)]
    [InlineData(ScoringMode.Gop)]
    public void AhHumanaWithOneHeardHIsBlamedOnOneLetter(ScoringMode mode)
    {
        var errors = ScoreHeard("ah humana", WithH("ah humana", 1), mode);

        var error = Assert.Single(errors);
        Assert.Equal(ErrorTags.PronSilentH, error.Tag);
        Assert.Equal(new TextSpan(1, 1), error.Span);
    }

    [Theory]
    [InlineData(ScoringMode.Greedy)]
    [InlineData(ScoringMode.Gop)]
    public void AhHumanaSpokenCorrectlyHasNoError(ScoringMode mode)
    {
        Assert.Empty(ScoreHeard("ah humana", SpanishG2p.PhonemizeAll("ah humana")[0].Phones, mode));
    }

    [Fact]
    public void BothVariantsOfAhHumanaAreOneVariant()
    {
        var variants = PredictedErrorVariants.Generate(SpanishG2p.PhonemizeAll("ah humana")[0]).Where(v => v.ErrorTag == ErrorTags.PronSilentH).ToList();

        var variant = Assert.Single(variants);
        Assert.Equal(new TextSpan(1, 1), variant.GraphemeSpan);
    }

    [Theory]
    [InlineData(ScoringMode.Greedy)]
    [InlineData(ScoringMode.Gop)]
    public void AhAloneKeepsItsSilentH(ScoringMode mode)
    {
        var error = Assert.Single(ScoreHeard("ah", WithH("ah", 1), mode));
        Assert.Equal(new TextSpan(1, 1), error.Span);
    }

    [Theory]
    [InlineData(ScoringMode.Greedy)]
    [InlineData(ScoringMode.Gop)]
    public void HumanaAloneKeepsItsSilentH(ScoringMode mode)
    {
        var error = Assert.Single(ScoreHeard("humana", WithH("humana", 0), mode));
        Assert.Equal(new TextSpan(0, 1), error.Span);
    }

    [Theory]
    [InlineData(ScoringMode.Greedy)]
    [InlineData(ScoringMode.Gop)]
    public void HAfterAnUnrelatedWordStillBelongsToTheNextWord(ScoringMode mode)
    {
        var error = Assert.Single(ScoreHeard("la hora", WithH("la hora", 2), mode));
        Assert.Equal(new TextSpan(3, 1), error.Span);
    }

    [Theory]
    [InlineData(ScoringMode.Greedy)]
    [InlineData(ScoringMode.Gop)]
    public void WordInternalPairOfSilentHsAreSeparateSites(ScoringMode mode)
    {
        // "ah ahora": the h of "ah" and the h of "ahora" are separated by phones, so each is its own site.
        var errors = ScoreHeard("ah ahora", WithH("ah ahora", 1), mode);
        var error = Assert.Single(errors);
        Assert.Equal(new TextSpan(1, 1), error.Span);
    }
}
