namespace Vocaterra.Nlp.De.Tests.Normalization;

/// <summary>Tests for <see cref="GermanFolding"/>: umlauts and the sharp s as two-letter spellings, and the lexicon reading of folded spellings.</summary>
public class GermanFoldingTests
{
    private static readonly ILexiconFolding Folding = GermanFolding.Instance;

    public static TheoryData<string, string> LenientCases => new()
    {
        { "schön", "schoen" },
        { "Schön", "schoen" },
        { "SCHÖN", "schoen" },
        { "schoen", "schoen" },
        { "Straße", "strasse" },
        { "strasse", "strasse" },
        { "Strasse", "strasse" },
        { "STRAẞE", "strasse" },
        { "Grüße", "gruesse" },
        { "Grüsse", "gruesse" },
        { "grüße", "gruesse" },
        { "für", "fuer" },
        { "fuer", "fuer" },
        { "über", "ueber" },
        { "Äpfel", "aepfel" },
        { "Öl", "oel" },
        { "Mädchen", "maedchen" },
        { "Mädel", "maedel" },
        { "können", "koennen" },
        { "müde", "muede" },
        { "Bär", "baer" },
        { "Bar", "bar" },
        { "schon", "schon" },
        { "Haus", "haus" },
        { "Fuß", "fuss" },
        { "Fuss", "fuss" },
        { "größer", "groesser" },
        { "Käse", "kaese" },
        { "Brötchen", "broetchen" },
        { "Zürich", "zuerich" },
        { "Österreich", "oesterreich" },
        { "", string.Empty },
        { "über", "ueber" },
    };

    [Theory]
    [MemberData(nameof(LenientCases))]
    public void LenientKeyWritesUmlautsAndSharpSAsTwoLetters(string text, string expected)
    {
        Assert.Equal(expected, Folding.LenientKey(text));
    }

    [Theory]
    [InlineData("Schön", "schön")]
    [InlineData("STRASSE", "strasse")]
    [InlineData("Straße", "straße")]
    [InlineData("ÜBER", "über")]
    [InlineData("über", "über")]
    [InlineData("", "")]
    public void KeyKeepsTheUmlautsAndTheSharpS(string text, string expected)
    {
        Assert.Equal(expected, Folding.Key(text));
    }

    [Theory]
    [InlineData("Schön", "Schoen")]
    [InlineData("Straße", "Strasse")]
    [InlineData("Äpfel", "Aepfel")]
    [InlineData("ÖL", "OeL")]
    [InlineData("Übung", "Uebung")]
    [InlineData("grüßen", "gruessen")]
    [InlineData("Haus", "Haus")]
    [InlineData("schoen", "schoen")]
    public void MatchingKeyWritesTheTwoLetterSpellingsAndKeepsTheCase(string text, string expected)
    {
        Assert.Equal(expected, Folding.MatchingKey(text));
    }

    [Theory]
    [InlineData("Schön", "Schoen")]
    [InlineData("Schoen", "Schön")]
    [InlineData("Strasse", "Straße")]
    [InlineData("Grüsse", "Grüße")]
    [InlineData("Köln", "Koeln")]
    public void TwoSpellingsOfOneWordShareAMatchingKeyOnceSpelledOut(string left, string right)
    {
        Assert.Equal(Folding.MatchingKey(left), Folding.MatchingKey(right));
    }

    [Fact]
    public void MatchingKeyReadsALoneSurrogateAsTheReplacementCharacter()
    {
        Assert.Equal("a�b", Folding.MatchingKey("a\uD800b"));
    }

    [Theory]
    [InlineData("für", "fur")]
    [InlineData("schön", "schon")]
    [InlineData("größer", "grosser")]
    [InlineData("können", "konnen")]
    [InlineData("über", "uber")]
    [InlineData("Mädchen", "madchen")]
    public void UnmarkedSpellingIsTheUmlautAsItsBareVowel(string form, string expected)
    {
        Assert.Equal([expected], Folding.UnmarkedSpellings(form.ToLowerInvariant()).Select(s => form[0] == char.ToUpperInvariant(form[0]) ? s.ToLowerInvariant() : s));
    }

    [Theory]
    [InlineData("haus")]
    [InlineData("straße")]
    [InlineData("")]
    public void AFormWithoutAnUmlautHasNoUnmarkedSpelling(string form)
    {
        Assert.Empty(Folding.UnmarkedSpellings(form));
    }

    [Fact]
    public void ASpellingThatIsAWordKeepsItsOwnReading()
    {
        var lexicon = TestSupport.Small(("schon", "schon", "adv", null), ("schön", "schön", "adj", null), ("bar", "bar", "adj", null), ("bär", "bär", "noun", null));

        var schon = lexicon.Lookup("schon");
        var schoen = lexicon.Lookup("schoen");
        var bar = lexicon.Lookup("Bar");

        Assert.False(schon.IsAccentInsensitive);
        Assert.Equal("schon", Assert.Single(schon).Lemma);
        Assert.True(schoen.IsAccentInsensitive);
        Assert.Equal("schön", Assert.Single(schoen).Lemma);
        Assert.Equal("bar", Assert.Single(bar).Lemma);
    }

    [Fact]
    public void ABareVowelSpellingReachesTheUmlautWordWhenNothingElseIsThatWord()
    {
        var lexicon = TestSupport.Small(("für", "für", "prep", null), ("schön", "schön", "adj", null), ("schon", "schon", "adv", null));

        Assert.Equal("für", Assert.Single(lexicon.Lookup("fur")).Lemma);
        Assert.Equal("schon", Assert.Single(lexicon.Lookup("schon")).Lemma);
    }

    [Fact]
    public void TheTwoLetterSpellingOfTheSharpSReachesTheWordFromTheFullLexicon()
    {
        var lexicon = TestSupport.FullLexicon;

        foreach (var typed in new[] { "Strasse", "strasse", "Straße" })
        {
            var reading = lexicon.Lookup(typed);
            Assert.True(reading.Count > 0, typed);
            Assert.Equal("straße", reading[0].Lemma);
        }

        Assert.True(lexicon.Lookup("Grüsse").Count > 0);
        Assert.True(lexicon.Lookup("schoen").Count > 0);
        Assert.Equal("schön", lexicon.Lookup("schoen")[0].Lemma);
        Assert.False(lexicon.Lookup("schon").IsAccentInsensitive);
    }

    [Fact]
    public void NullIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => Folding.MatchingKey(null!));
        Assert.Throws<ArgumentNullException>(() => Folding.UnmarkedSpellings(null!));
    }
}
