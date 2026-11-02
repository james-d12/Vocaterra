namespace Vocaterra.Nlp.Es.Tests.Lexicon;

using Vocaterra.Nlp.Es.Normalization;
using Vocaterra.Nlp.Es.Tests.Analysis;
using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Lexicon;

/// <summary>
/// M70, the owner's answer of 2026-11-01 (D-game-22, item 2; QA-language-7): a word typed with <c>n</c> for <c>ñ</c> is read as the word, as one typed without its
/// accent is (M26b), only when the spelling with <c>n</c> is no word of its own. The lexicon is asked first (a spelling that is a lexicon form, accents ignored,
/// keeps its readings: <c>una</c>, <c>pena</c>, <c>mono</c>, <c>suena</c>), and the Spanish pack names the words a bundle's lexicon may lack (<c>ano</c>,
/// <c>cana</c>). Before M70 every slip gave no reading (<c>vocab.unknown</c>).
/// </summary>
public class UnmarkedSpellingTests
{
    private static readonly Lexicon Bundle = TestSupport.Bundle.Lexicon;

    private static readonly LinguisticAnalyzer Analyzer = new(Bundle, SpanishLanguagePack.Instance);

    private static string Described(string sentence)
    {
        return string.Join(';', Analyzer.Analyze(TestTokens.From(sentence)).Errors.Select(static e => $"{e.Tag}|{e.Actual}|{e.Expected}"));
    }

    // The sentences of the QA pass and their neighbours: the slip is no error (before M70: vocab.unknown on it), and a word of its own is graded as before.
    [Theory]
    [InlineData("manana compraré pan", "")]
    [InlineData("manana venderé fruta", "")]
    [InlineData("el nino es pequeno", "")]
    [InlineData("buenos días senor", "")]
    [InlineData("hablo espanol", "")]
    [InlineData("la nina pequena", "")]
    [InlineData("el nino pequena", "gender.agree|pequena|pequeño")]
    [InlineData("tengo diez anos", "vocab.unknown|anos|")]
    [InlineData("tengo una pena", "")]
    [InlineData("el mono come fruta", "")]
    public void TheSlipIsAnalyzedAsTheWord(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    [Theory]
    [InlineData("manana", "mañana")]
    [InlineData("pequeno", "pequeño")]
    [InlineData("pequenas", "pequeño")]
    [InlineData("senor", "señor")]
    [InlineData("espanol", "español")]
    [InlineData("nino", "niño")]
    [InlineData("ninos", "niño")]
    [InlineData("Manana", "mañana")]
    [InlineData("anadio", "añadir")]
    public void AWordTypedWithNForNTildeIsReadAsTheWord(string typed, string lemma)
    {
        var found = Bundle.Lookup(typed);

        Assert.NotEmpty(found);
        Assert.True(found.IsAccentInsensitive);
        Assert.Contains(found, r => r.Lemma == lemma);
    }

    // The plain spelling is a word: graded as before M70 (a lexicon word keeps its readings; a word of the pack's list that the lexicon lacks stays unknown).
    [Theory]
    [InlineData("una", "uña")]
    [InlineData("unas", "uña")]
    [InlineData("pena", "peña")]
    [InlineData("mono", "moño")]
    [InlineData("suena", "soñar")]
    [InlineData("sonar", "soñar")]
    [InlineData("campana", "campaña")]
    [InlineData("dona", "doña")]
    public void ASpellingThatIsALexiconWordKeepsItsOwnReadings(string typed, string notLemma)
    {
        var found = Bundle.Lookup(typed);

        Assert.NotEmpty(found);
        Assert.False(found.IsAccentInsensitive);
        Assert.DoesNotContain(found, r => r.Lemma == notLemma);
    }

    [Theory]
    [InlineData("ano")]
    [InlineData("anos")]
    [InlineData("cana")]
    [InlineData("conos")]
    public void ASpanishWordTheLexiconLacksStaysUnknown(string typed)
    {
        Assert.Empty(Bundle.Lookup(typed));
        Assert.Empty(Bundle.SpellingsIgnoringAccents(typed));
    }

    [Fact]
    public void TheCorrectSpellingsAreUnchanged()
    {
        Assert.False(Bundle.Lookup("mañana").IsAccentInsensitive);
        Assert.False(Bundle.Lookup("año").IsAccentInsensitive);
        Assert.Contains(Bundle.Lookup("años"), static r => r.Lemma == "año");
    }

    [Fact]
    public void TheRecastSpellingOfTheSlipIsTheWord()
    {
        Assert.Equal(["mañana"], Bundle.SpellingsIgnoringAccents("manana"));
        Assert.Equal(["español"], Bundle.SpellingsIgnoringAccents("espanol"));
        Assert.Equal(["una"], Bundle.SpellingsIgnoringAccents("una"));
    }

    [Fact]
    public void AHandMadeLexiconFollowsTheSameRule()
    {
        var words = SpanishLexicon.FromEntries(
        [
            new LexiconEntry("niño", "niño", "noun", new EntryFeatures(null, "sg", null, null, "m")),
            new LexiconEntry("uña", "uña", "noun", new EntryFeatures(null, "sg", null, null, "f")),
            new LexiconEntry("una", "uno", "art", new EntryFeatures(null, "sg", null, null, "f")),
            new LexiconEntry("año", "año", "noun", new EntryFeatures(null, "sg", null, null, "m")),
            new LexiconEntry("peña", "peña", "noun", new EntryFeatures(null, "sg", null, null, "f")),
        ]);

        Assert.Equal("niño", Assert.Single(words.Lookup("nino")).Lemma);
        Assert.Equal("uno", Assert.Single(words.Lookup("una")).Lemma);
        Assert.Empty(words.Lookup("ano"));
        Assert.Empty(words.Lookup("pena"));
        Assert.Empty(words.Lookup("xyz"));
    }

    [Theory]
    [InlineData("mañana", "manana")]
    [InlineData("pequeño", "pequeno")]
    [InlineData("compañía", "compania")]
    [InlineData("año", null)]
    [InlineData("años", null)]
    [InlineData("caña", null)]
    [InlineData("uña", null)]
    [InlineData("casa", null)]
    [InlineData("está", null)]
    public void TheUnmarkedSpellingIsTheNTildeTypedAsN(string form, string? expected)
    {
        Assert.Equal(expected, TextFolding.UnmarkedSpelling(form));
    }
}
