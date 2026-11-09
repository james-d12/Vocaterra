namespace Vocaterra.Nlp.De.Tests.Normalization;

/// <summary>Tests for <see cref="GermanNormalizer"/>: lower-casing with the typed form kept, punctuation and quotes, contractions with their case, numbers, final marks.</summary>
public class GermanNormalizerTests
{
    private static readonly NormalizationOptions WordsKept = new(NumbersToDigits: false);

    private static readonly NormalizationOptions Expanded = new(ExpandContractions: true, NumbersToDigits: false);

    private static readonly NormalizationOptions Marked = new(NumbersToDigits: false, CaptureFinalMark: true);

    public static TheoryData<string, string> TextCases => new()
    {
        { "Hallo", "hallo" },
        { "Guten Morgen", "guten morgen" },
        { "Guten Morgen!", "guten morgen" },
        { "Wie geht es Ihnen?", "wie geht es ihnen" },
        { "ICH BIN MÜDE", "ich bin müde" },
        { "Ich bin müde.", "ich bin müde" },
        { "Das Mädchen", "das mädchen" },
        { "Österreich", "österreich" },
        { "Über den Wolken", "über den wolken" },
        { "Straße", "straße" },
        { "STRASSE", "strasse" },
        { "  viel   Platz  ", "viel platz" },
        { "viel\tPlatz\nhier", "viel platz hier" },
        { "„Guten Tag“, sagte er.", "guten tag sagte er" },
        { "»Guten Tag«, sagte er.", "guten tag sagte er" },
        { "„Wie viel kostet das?“", "wie viel kostet das" },
        { "«Hallo!»", "hallo" },
        { "‚Ja‘ und ‚nein‘", "ja und nein" },
        { "Ja, nein; vielleicht: später…", "ja nein vielleicht später" },
        { "Ich möchte (bitte) zahlen", "ich möchte bitte zahlen" },
        { "E-Mail", "e-mail" },
        { "Baden-Württemberg", "baden-württemberg" },
        { "Haus - Hof", "haus hof" },
        { "ein Haus", "ein haus" },
        { "Eine Katze", "eine katze" },
        { "Einen Hund", "einen hund" },
        { "Ich habe einen Hund und eine Katze", "ich habe einen hund und eine katze" },
        { "Brötchen mit Käse", "brötchen mit käse" },
        { "Fünf Euro", "fünf euro" },
        { "Schön", "schön" },
        { "schoen", "schoen" },
        { "Nähe", "nähe" },
        { "Köln", "köln" },
        { "Zürich", "zürich" },
        { "Grüezi!", "grüezi" },
        { "Servus", "servus" },
        { "Grüß Gott", "grüß gott" },
        { "", string.Empty },
        { "   ", string.Empty },
        { "?!", string.Empty },
        { "Ja!", "ja" },
        { "Nein?", "nein" },
        { "Bitte schön", "bitte schön" },
        { "Danke sehr", "danke sehr" },
    };

    [Theory]
    [MemberData(nameof(TextCases))]
    public void NormalizesTheText(string input, string expected)
    {
        var result = GermanNormalizer.Instance.Normalize(input, WordsKept);

        Assert.Equal(expected, result.Text);
    }

    [Theory]
    [MemberData(nameof(TextCases))]
    public void TokenSpansIndexIntoTheText(string input, string expected)
    {
        var result = GermanNormalizer.Instance.Normalize(input, WordsKept);

        Assert.Equal(expected, string.Join(' ', result.Tokens.Select(static t => t.Text)));
        foreach (var token in result.Tokens)
        {
            Assert.Equal(token.Text, result.Text.Substring(token.Span.Start, token.Span.Length));
        }
    }

    [Theory]
    [MemberData(nameof(TextCases))]
    public void NormalizingTwiceChangesNothing(string input, string expected)
    {
        var first = GermanNormalizer.Instance.Normalize(input, WordsKept);
        var second = GermanNormalizer.Instance.Normalize(first.Text, WordsKept);

        Assert.Equal(expected, second.Text);
        Assert.Equal(first.Tokens.Select(static t => (t.Text, t.Span)), second.Tokens.Select(static t => (t.Text, t.Span)));
    }

    [Fact]
    public void KeepsTheTypedFormInOriginal()
    {
        var result = GermanNormalizer.Instance.Normalize("Ich möchte Ihnen danken", WordsKept);

        Assert.Equal(["Ich", "möchte", "Ihnen", "danken"], result.Tokens.Select(static t => t.Original));
        Assert.Equal(new Token("ihnen", new TextSpan(11, 5), "Ihnen"), result.Tokens[2]);
    }

    [Fact]
    public void QuotesAreNotTokens()
    {
        var result = GermanNormalizer.Instance.Normalize("„Hallo“ und »Tschüss«", WordsKept);

        Assert.Equal(["hallo", "und", "tschüss"], result.Tokens.Select(static t => t.Text));
        Assert.Equal(["Hallo", "und", "Tschüss"], result.Tokens.Select(static t => t.Original));
    }

    [Theory]
    [InlineData("Wir treffen uns am Bahnhof", "wir treffen uns an dem bahnhof")]
    [InlineData("Ich wohne im Haus", "ich wohne in dem haus")]
    [InlineData("Ich gehe zum Arzt", "ich gehe zu dem arzt")]
    [InlineData("Ich gehe zur Schule", "ich gehe zu der schule")]
    [InlineData("Er ist beim Bäcker", "er ist bei dem bäcker")]
    [InlineData("Das ist vom Chef", "das ist von dem chef")]
    [InlineData("Wir gehen ins Kino", "wir gehen in das kino")]
    [InlineData("Ich gehe ans Fenster", "ich gehe an das fenster")]
    [InlineData("Er legt es aufs Bett", "er legt es auf das bett")]
    [InlineData("Das ist fürs Kind", "das ist für das kind")]
    [InlineData("Sie geht übers Eis", "sie geht über das eis")]
    public void ExpandsTheContractionsWithTheirCase(string input, string expected)
    {
        var result = GermanNormalizer.Instance.Normalize(input, Expanded);

        Assert.Equal(expected, result.Text);
    }

    [Fact]
    public void BothTokensOfAContractionCarryTheContractionAsOriginal()
    {
        var result = GermanNormalizer.Instance.Normalize("am Bahnhof", Expanded);

        Assert.Equal(["an", "dem", "bahnhof"], result.Tokens.Select(static t => t.Text));
        Assert.Equal(["am", "am", "Bahnhof"], result.Tokens.Select(static t => t.Original));
    }

    [Fact]
    public void ContractionsStayWhenNotExpanded()
    {
        var result = GermanNormalizer.Instance.Normalize("am Bahnhof im Haus", WordsKept);

        Assert.Equal("am bahnhof im haus", result.Text);
    }

    [Theory]
    [InlineData("Wie viel kostet das?", "?")]
    [InlineData("Hallo!", "!")]
    [InlineData("„Wie viel kostet das?“", "?")]
    [InlineData("»Hallo!«", "!")]
    [InlineData("Er sagte: „Wie spät ist es?“", "?")]
    [InlineData("Wie spät ist es?  ", "?")]
    [InlineData("Ich bin müde.", null)]
    [InlineData("Ich bin müde", null)]
    public void CapturesTheFinalMarkBehindClosingQuotes(string input, string? mark)
    {
        var result = GermanNormalizer.Instance.Normalize(input, Marked);

        Assert.Equal(mark, result.FinalMark);
    }

    [Fact]
    public void FinalMarkIsOffByDefault()
    {
        Assert.Null(GermanNormalizer.Instance.Normalize("Hallo!").FinalMark);
    }

    [Theory]
    [InlineData("einundzwanzig Euro", "21 euro")]
    [InlineData("zweitausenddreihundert Meter", "2300 meter")]
    [InlineData("hundertvierzig", "140")]
    [InlineData("zwei Brötchen", "2 brötchen")]
    [InlineData("drei hundert", "300")]
    [InlineData("fünf und zwanzig", "5 und 20")]
    [InlineData("eins zwei drei", "1 2 3")]
    public void ConvertsNumberWordsToDigits(string input, string expected)
    {
        var result = GermanNormalizer.Instance.Normalize(input);

        Assert.Equal(expected, result.Text);
    }

    [Theory]
    [InlineData("Ich habe einen Hund", "ich habe einen hund")]
    [InlineData("ein Mann", "ein mann")]
    [InlineData("Eine Frau", "eine frau")]
    [InlineData("mit einem Kind", "mit einem kind")]
    [InlineData("Das Haus einer Freundin", "das haus einer freundin")]
    public void NeverTurnsAnArticleFormIntoTheDigitOne(string input, string expected)
    {
        var result = GermanNormalizer.Instance.Normalize(input);

        Assert.Equal(expected, result.Text);
    }

    [Fact]
    public void ArticleFormsStayEvenWhenIndefiniteArticlesAreConverted()
    {
        var result = GermanNormalizer.Instance.Normalize("ein Hund", new NormalizationOptions(ConvertIndefiniteArticles: true));

        Assert.Equal("ein hund", result.Text);
    }

    [Fact]
    public void ANumberTokenKeepsItsWordsAsOriginal()
    {
        var result = GermanNormalizer.Instance.Normalize("zweihundert Euro");

        Assert.Equal(new Token("200", new TextSpan(0, 3), "zweihundert"), result.Tokens[0]);
    }

    [Fact]
    public void ANumberFromSeveralTokensJoinsTheirOriginals()
    {
        var result = GermanNormalizer.Instance.Normalize("Zwei Hundert vierzig");

        Assert.Equal(new Token("240", new TextSpan(0, 3), "Zwei Hundert vierzig"), Assert.Single(result.Tokens));
    }

    [Fact]
    public void DigitsStayDigits()
    {
        Assert.Equal("ich habe 3 katzen", GermanNormalizer.Instance.Normalize("Ich habe 3 Katzen").Text);
    }

    [Fact]
    public void ALoneSurrogateDoesNotThrow()
    {
        var result = GermanNormalizer.Instance.Normalize("Hallo \uD800 Welt");

        Assert.Contains("hallo", result.Text, StringComparison.Ordinal);
        Assert.Contains("welt", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void NullInputIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => GermanNormalizer.Instance.Normalize(null!));
    }

    [Fact]
    public void UpperCaseSharpSAndDecomposedUmlautsAreNormalizedToTheComposedForm()
    {
        var result = GermanNormalizer.Instance.Normalize("ÜBER", WordsKept);

        Assert.Equal("über", result.Text);
    }
}
