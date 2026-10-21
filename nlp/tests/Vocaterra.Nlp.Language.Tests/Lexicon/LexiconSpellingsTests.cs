namespace Vocaterra.Nlp.Language.Tests.Lexicon;

using Vocaterra.Nlp.Language.Lexicon;

/// <summary>Tests for <see cref="Lexicon.SpellingsIgnoringAccents"/>, the accented spellings that fold to one key.</summary>
public class LexiconSpellingsTests
{
    private static readonly Lexicon Words = Lexicon.FromEntries(
    [
        new LexiconEntry("días", "día", "noun", new EntryFeatures(null, "pl", null, null, "m")),
        new LexiconEntry("se", "se", "pron", new EntryFeatures(null, null, null, null, null)),
        new LexiconEntry("sé", "saber", "verb", new EntryFeatures("1", "sg", "pres", "ind", null)),
        new LexiconEntry("mas", "mas", "conj", new EntryFeatures(null, null, null, null, null)),
        new LexiconEntry("más", "mucho", "adv", new EntryFeatures(null, null, null, null, null)),
        new LexiconEntry("sóló", "solo", "adv", new EntryFeatures(null, null, null, null, null)),
        new LexiconEntry("sólo", "solo", "adv", new EntryFeatures(null, null, null, null, null)),
    ]);

    [Fact]
    public void AnAcceptedFormFoldsToItsAccentedSpelling()
    {
        Assert.Equal(["días"], Words.SpellingsIgnoringAccents("dias"));
    }

    [Fact]
    public void AFormWithItsAccentsFoldsToItself()
    {
        Assert.Equal(["días"], Words.SpellingsIgnoringAccents("días"));
    }

    [Fact]
    public void AnAccentlessLexiconFormIsListedWithItsAccentedSiblings()
    {
        Assert.Equal(["mas", "más"], Words.SpellingsIgnoringAccents("mas"));
        Assert.Equal(["se", "sé"], Words.SpellingsIgnoringAccents("sé"));
    }

    [Fact]
    public void SeveralAccentedSpellingsAreListedInOrdinalOrder()
    {
        Assert.Equal(["sólo", "sóló"], Words.SpellingsIgnoringAccents("solo"));
    }

    [Fact]
    public void AWordWithNoAccentedFormHasNoSpellings()
    {
        Assert.Empty(Words.SpellingsIgnoringAccents("zanahoria"));
    }

    [Fact]
    public void AnAccentInsensitiveLookupIsFlaggedButAnExactOneIsNot()
    {
        Assert.True(Words.Lookup("dias").IsAccentInsensitive);
        Assert.False(Words.Lookup("se").IsAccentInsensitive);
        Assert.False(Words.Lookup("días").IsAccentInsensitive);
    }

    [Fact]
    public void RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => Words.SpellingsIgnoringAccents(null!));
    }
}
