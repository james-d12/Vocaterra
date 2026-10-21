namespace Vocaterra.Nlp.Language.Tests.Lexicon;

using Vocaterra.Nlp.Language.Lexicon;

/// <summary>Tests for <see cref="Lexicon.LookupWithAccentVariants"/>: the readings of the accented spellings that a plain lexicon form hides (M26b).</summary>
public class LexiconAccentVariantsTests
{
    private static readonly Lexicon Words = Lexicon.FromEntries(
    [
        new LexiconEntry("esta", "este", "adj", new EntryFeatures(null, "sg", null, null, "f")),
        new LexiconEntry("está", "estar", "verb", new EntryFeatures("3", "sg", "pres", "ind", null)),
        new LexiconEntry("el", "el", "art", new EntryFeatures(null, "sg", null, null, "m")),
        new LexiconEntry("él", "él", "pron", new EntryFeatures("3", "sg", null, null, "m")),
        new LexiconEntry("se", "se", "pron", new EntryFeatures(null, null, null, null, null)),
        new LexiconEntry("sé", "saber", "verb", new EntryFeatures("1", "sg", "pres", "ind", null)),
        new LexiconEntry("sé", "ser", "verb", new EntryFeatures("2", "sg", "pres", "imp", null)),
        new LexiconEntry("casa", "casa", "noun", new EntryFeatures(null, "sg", null, null, "f")),
        new LexiconEntry("árbol", "árbol", "noun", new EntryFeatures(null, "sg", null, null, "m")),
    ]);

    [Fact]
    public void APlainLexiconFormOffersTheReadingsOfItsAccentedSpellings()
    {
        var lookup = Words.LookupWithAccentVariants("esta");

        Assert.Equal(["este"], lookup.Exact.Select(r => r.Lemma).ToArray());
        Assert.False(lookup.Exact.IsAccentInsensitive);
        var variant = Assert.Single(lookup.Variants);
        Assert.Equal("está", variant.Form);
        Assert.Equal("estar", variant.Reading.Lemma);
    }

    [Fact]
    public void TheExactReadingsAreWhatLookupReturns()
    {
        Assert.Equal(Words.Lookup("el").ToArray(), Words.LookupWithAccentVariants("el").Exact.ToArray());
        Assert.Equal(Words.Lookup("sé").ToArray(), Words.LookupWithAccentVariants("sé").Exact.ToArray());
        Assert.Equal(Words.Lookup("arbol").ToArray(), Words.LookupWithAccentVariants("arbol").Exact.ToArray());
    }

    [Fact]
    public void LookupItselfStillHidesTheAccentedReadings()
    {
        var lookup = Words.Lookup("esta");

        Assert.False(lookup.IsAccentInsensitive);
        Assert.Equal("este", Assert.Single(lookup).Lemma);
    }

    [Fact]
    public void AllReadingsOfAnAccentedSpellingAreVariantsInEntryOrder()
    {
        var lookup = Words.LookupWithAccentVariants("se");

        Assert.Equal(["saber", "ser"], lookup.Variants.Select(v => v.Reading.Lemma).ToArray());
        Assert.All(lookup.Variants, v => Assert.Equal("sé", v.Form));
    }

    [Fact]
    public void AnAccentedFormHasNoVariants()
    {
        Assert.Empty(Words.LookupWithAccentVariants("está").Variants);
        Assert.Empty(Words.LookupWithAccentVariants("él").Variants);
        Assert.Empty(Words.LookupWithAccentVariants("ESTÁ").Variants);
    }

    [Fact]
    public void AFormTheLexiconLacksHasNoVariantsBecauseLookupAlreadyFolds()
    {
        var lookup = Words.LookupWithAccentVariants("arbol");

        Assert.True(lookup.Exact.IsAccentInsensitive);
        Assert.Equal("árbol", Assert.Single(lookup.Exact).Lemma);
        Assert.Empty(lookup.Variants);
    }

    [Fact]
    public void AWordWithNoAccentedSiblingOrAnUnknownWordHasNoVariants()
    {
        Assert.Empty(Words.LookupWithAccentVariants("casa").Variants);
        Assert.Empty(Words.LookupWithAccentVariants("xyzzy").Variants);
        Assert.Empty(Words.LookupWithAccentVariants("xyzzy").Exact);
    }

    [Fact]
    public void ANormalizedCaseInsensitiveFormFindsTheVariants()
    {
        Assert.Single(Words.LookupWithAccentVariants("EL").Variants);
    }
}
