namespace Vocaterra.Nlp.Response.Tests.Templates;

using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Nlp.Lexicon;
using Vocaterra.Nlp.Nlp.Normalization;
using Vocaterra.Nlp.Response.Templates;

/// <summary>
/// M17c: a recast restores the lexicon's accents for a word the player left unaccented (<c>dias</c> gives <c>días</c>), and keeps a word
/// that is another valid word as spelled (<c>se</c> is not <c>sé</c>).
/// </summary>
public class RecastAccentTests
{
    private static readonly Lexicon Words = Lexicon.FromEntries(
    [
        new LexiconEntry("buenos", "bueno", "adj", new EntryFeatures(null, "pl", null, null, "m")),
        new LexiconEntry("días", "día", "noun", new EntryFeatures(null, "pl", null, null, "m")),
        new LexiconEntry("se", "se", "pron", new EntryFeatures(null, null, null, null, null)),
        new LexiconEntry("sé", "saber", "verb", new EntryFeatures("1", "sg", "pres", "ind", null)),
        new LexiconEntry("mas", "mas", "conj", new EntryFeatures(null, null, null, null, null)),
        new LexiconEntry("más", "mucho", "adv", new EntryFeatures(null, null, null, null, null)),
        new LexiconEntry("sóló", "solo", "adv", new EntryFeatures(null, null, null, null, null)),
        new LexiconEntry("sólo", "solo", "adv", new EntryFeatures(null, null, null, null, null)),
    ]);

    private static NormalizedText Plain(string text)
    {
        return TextNormalizer.Normalize(text, new NormalizationOptions { NumbersToDigits = false, ConvertIndefiniteArticles = false });
    }

    private static AnalysisResult NoErrors()
    {
        return new AnalysisResult([], [], [], null, 1.0);
    }

    [Fact]
    public void AnAccentlessWordTakesTheLexiconsAccentedSpelling()
    {
        Assert.Equal("buenos días", RecastBuilder.Build(Plain("buenos dias"), NoErrors(), Words));
    }

    [Fact]
    public void AnErrorCorrectionAndTheAccentRestorationApplyInTheSameSentence()
    {
        // The gender error on buenas comes from the analysis; the accentless dias comes from the lexicon.
        var analysis = new AnalysisResult(
            [],
            [],
            [new ErrorTag(ErrorTags.GenderAgree, new TextSpan(0, 6), "buenos", "buenas")],
            null,
            1.0);

        Assert.Equal("buenos días", RecastBuilder.Build(Plain("buenas dias"), analysis, Words));
    }

    [Fact]
    public void AWordThatIsAnotherValidWordAsSpelledIsNotCorrected()
    {
        // se is a lexicon form (the pronoun), so it stays even though sé (the verb) folds to the same letters.
        Assert.Equal("yo se", RecastBuilder.Build(Plain("yo se"), NoErrors(), Words));
        Assert.Equal("mas", RecastBuilder.Build(Plain("mas"), NoErrors(), Words));
    }

    [Fact]
    public void WhenSeveralAccentedSpellingsFoldAlikeTheFirstInOrdinalOrderIsUsed()
    {
        // sólo and sóló fold to solo; the ordinal order puts sólo first.
        Assert.Equal("sólo", RecastBuilder.Build(Plain("solo"), NoErrors(), Words));
    }

    [Fact]
    public void AWordTheLexiconDoesNotKnowIsLeftAsSpokenWhileItsNeighbourIsRestored()
    {
        Assert.Equal("zanahoria días", RecastBuilder.Build(Plain("zanahoria dias"), NoErrors(), Words));
    }

    [Fact]
    public void WithoutALexiconNoAccentIsRestored()
    {
        Assert.Equal("buenos dias", RecastBuilder.Build(Plain("buenos dias"), NoErrors()));
    }
}
