using Vocaterra.Nlp.Core.Taxonomy;

namespace Vocaterra.Nlp.Core.Tests;

/// <summary>Tests for <see cref="ErrorTags"/>.</summary>
public class ErrorTagsTests
{
    [Fact]
    public void AllHasThirteenTagsInHldOrder()
    {
        string[] expected =
        [
            "vocab.wrong", "vocab.unknown", "conj.person", "conj.tense", "conj.irregular", "gender.agree", "noun.number",
            "ser_estar", "word_order", "pron.vowel", "pron.r", "pron.silent_h", "pron.stress",
        ];
        Assert.Equal(13, ErrorTags.All.Count);
        Assert.Equal(expected, ErrorTags.All);
    }

    [Fact]
    public void ConstantsMatchTheirIdentifiers()
    {
        Assert.Equal("vocab.wrong", ErrorTags.VocabWrong);
        Assert.Equal("vocab.unknown", ErrorTags.VocabUnknown);
        Assert.Equal("conj.person", ErrorTags.ConjPerson);
        Assert.Equal("conj.tense", ErrorTags.ConjTense);
        Assert.Equal("conj.irregular", ErrorTags.ConjIrregular);
        Assert.Equal("gender.agree", ErrorTags.GenderAgree);
        Assert.Equal("noun.number", ErrorTags.NounNumber);
        Assert.Equal("ser_estar", ErrorTags.SerEstar);
        Assert.Equal("word_order", ErrorTags.WordOrder);
        Assert.Equal("pron.vowel", ErrorTags.PronVowel);
        Assert.Equal("pron.r", ErrorTags.PronR);
        Assert.Equal("pron.silent_h", ErrorTags.PronSilentH);
        Assert.Equal("pron.stress", ErrorTags.PronStress);
    }

    [Fact]
    public void IsKnownIsExactAndOrdinal()
    {
        Assert.All(ErrorTags.All, tag => Assert.True(ErrorTags.IsKnown(tag)));
        Assert.False(ErrorTags.IsKnown("conj"));
        Assert.False(ErrorTags.IsKnown("CONJ.PERSON"));
        Assert.False(ErrorTags.IsKnown(string.Empty));
    }

    [Theory]
    [InlineData("conj.person", "conj")]
    [InlineData("pron.silent_h", "pron")]
    [InlineData("ser_estar", "ser_estar")]
    [InlineData("word_order", "word_order")]
    [InlineData("vocab.unknown", "vocab")]
    public void FamilyIsTextBeforeFirstDot(string tag, string family)
    {
        Assert.Equal(family, ErrorTags.Family(tag));
    }

    [Fact]
    public void IsPronunciationOnlyForPronTags()
    {
        string[] pron = [ErrorTags.PronVowel, ErrorTags.PronR, ErrorTags.PronSilentH, ErrorTags.PronStress];
        Assert.All(pron, tag => Assert.True(ErrorTags.IsPronunciation(tag)));
        Assert.All(ErrorTags.All.Except(pron), tag => Assert.False(ErrorTags.IsPronunciation(tag)));
        Assert.False(ErrorTags.IsPronunciation("pronoun"));
    }
}
