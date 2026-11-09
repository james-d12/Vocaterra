namespace Vocaterra.Nlp.De.Tests.Text;

/// <summary>Tests for <see cref="GermanRecastPolicy"/>: capitals from the resolved reading, umlauts from the lexicon, the Swiss ss left alone, and the typed-only capital slips.</summary>
public class GermanRecastPolicyTests
{
    private static readonly Lexicon Lexicon = TestSupport.Small(
        ("haus", "haus", "noun", null),
        ("hund", "hund", "noun", null),
        ("katze", "katze", "noun", null),
        ("brötchen", "brötchen", "noun", null),
        ("straße", "straße", "noun", null),
        ("schön", "schön", "adj", null),
        ("schon", "schon", "adv", null),
        ("ich", "ich", "pron", null),
        ("sie", "sie", "pron", null),
        ("sie", "sie", "pron", "formal"),
        ("ihnen", "ihnen", "pron", "formal"),
        ("habe", "haben", "verb", null),
        ("möchte", "mögen", "verb", null),
        ("essen", "essen", "verb", null),
        ("essen", "essen", "noun", null),
        ("ein", "ein", "art", null),
        ("einen", "ein", "art", null),
        ("und", "und", "conj", null),
        ("die", "die", "art", null),
        ("das", "das", "art", null),
        ("bitte", "bitte", "interj", null),
        ("fuß", "fuß", "noun", null),
        ("grüße", "gruß", "noun", null),
        ("für", "für", "prep", null));

    private static (NormalizedText Text, AnalysisResult Analysis) Analyse(string input, params (string Word, string? Pos)[] chosen)
    {
        var text = GermanNormalizer.Instance.Normalize(input, new NormalizationOptions(NumbersToDigits: false));
        var tokens = text.Tokens.Select(t => new AnalyzedToken(t, [.. Lexicon.Lookup(t.Text)])).ToList();
        var words = new List<ResolvedWord>();
        for (var i = 0; i < text.Tokens.Count; i++)
        {
            var token = text.Tokens[i];
            var pos = chosen.FirstOrDefault(c => c.Word == token.Text).Pos;
            var readings = tokens[i].Readings;
            var reading = pos is null ? readings.Count > 0 ? readings[0] : null : readings.FirstOrDefault(r => r.Pos == pos);
            words.Add(new ResolvedWord(token, reading is null ? WordRole.Unknown : WordRole.Word, reading));
        }

        return (text, new AnalysisResult(tokens, [], [], null, 1.0, words));
    }

    private static string Recast(string input, params (string Word, string? Pos)[] chosen)
    {
        var (text, analysis) = Analyse(input, chosen);
        var spellings = GermanRecastPolicy.Instance.RestoredSpellings(text, analysis, Lexicon);
        var capitals = GermanRecastPolicy.Instance.RestoredCapitals(text, analysis, Lexicon);
        var result = text.Text;
        foreach (var token in text.Tokens.OrderByDescending(static t => t.Span.Start))
        {
            var replacement = spellings.TryGetValue(token.Span.Start, out var s) ? s : capitals.GetValueOrDefault(token.Span.Start);
            if (replacement is not null)
            {
                result = string.Concat(result.AsSpan(0, token.Span.Start), replacement, result.AsSpan(token.Span.End));
            }
        }

        return result;
    }

    [Theory]
    [InlineData("ich habe einen hund", "Ich habe einen Hund")]
    [InlineData("ich habe ein haus", "Ich habe ein Haus")]
    [InlineData("die katze und der hund", "Die Katze und der Hund")]
    [InlineData("haus", "Haus")]
    [InlineData("das haus", "Das Haus")]
    [InlineData("hund und katze", "Hund und Katze")]
    [InlineData("ich möchte ein brötchen", "Ich möchte ein Brötchen")]
    [InlineData("ich möchte bitte ein brötchen", "Ich möchte bitte ein Brötchen")]
    [InlineData("das ist ein haus", "Das ist ein Haus")]
    [InlineData("ich", "Ich")]
    [InlineData("katze hund haus", "Katze Hund Haus")]
    [InlineData("ich habe eine katze", "Ich habe eine Katze")]
    [InlineData("die straße", "Die Straße")]
    [InlineData("der fuß", "Der Fuß")]
    [InlineData("bitte", "Bitte")]
    [InlineData("und", "Und")]
    public void RestoresTheCapitalsOfNounsAndTheSentenceStart(string typed, string expected)
    {
        Assert.Equal(expected, Recast(typed));
    }

    [Fact]
    public void ANounRestoredWithItsUmlautsIsCapitalizedTooAndNeverTwice()
    {
        var (text, analysis) = Analyse("ich habe brotchen");
        var spellings = GermanRecastPolicy.Instance.RestoredSpellings(text, analysis, Lexicon);
        var capitals = GermanRecastPolicy.Instance.RestoredCapitals(text, analysis, Lexicon);

        var brotchen = text.Tokens[2].Span.Start;
        Assert.Equal("Brötchen", spellings[brotchen]);
        Assert.DoesNotContain(brotchen, capitals.Keys);
    }

    [Fact]
    public void AWordTheResolverReadAsAVerbGetsNoCapital()
    {
        Assert.Equal("Wir essen", Recast("wir essen", ("essen", "verb")));
    }

    [Fact]
    public void TheSameWordReadAsANounGetsItsCapital()
    {
        Assert.Equal("Das Essen", Recast("das essen", ("essen", "noun")));
    }

    [Fact]
    public void AWordWithNounAndVerbReadingsAndNoResolverChoiceGetsNoCapital()
    {
        var text = GermanNormalizer.Instance.Normalize("wir essen");
        var analysis = new AnalysisResult([.. text.Tokens.Select(t => new AnalyzedToken(t, [.. Lexicon.Lookup(t.Text)]))], [], [], null, 1.0);

        var capitals = GermanRecastPolicy.Instance.RestoredCapitals(text, analysis, Lexicon);

        Assert.DoesNotContain(text.Tokens[1].Span.Start, capitals.Keys);
    }

    [Fact]
    public void TheFormalSieIsCapitalizedInTheMiddleOfASentence()
    {
        var formal = Lexicon.Lookup("sie").First(static r => r.Register == "formal");
        var sieText = GermanNormalizer.Instance.Normalize("haben sie hunger");
        var sieAnalysis = new AnalysisResult(
            [.. sieText.Tokens.Select(t => new AnalyzedToken(t, []))],
            [],
            [],
            null,
            1.0,
            [new ResolvedWord(sieText.Tokens[0], WordRole.Unknown, null), new ResolvedWord(sieText.Tokens[1], WordRole.Word, formal), new ResolvedWord(sieText.Tokens[2], WordRole.Unknown, null)]);

        var capitals = GermanRecastPolicy.Instance.RestoredCapitals(sieText, sieAnalysis, Lexicon);

        Assert.Equal("Sie", capitals[sieText.Tokens[1].Span.Start]);
        Assert.Equal("Haben", capitals[0]);
        Assert.Equal(2, capitals.Count);
    }

    [Fact]
    public void TheInformalSieReadingGetsNoCapitalInTheMiddle()
    {
        var plain = Lexicon.Lookup("sie").First(static r => r.Register is null);
        var text = GermanNormalizer.Instance.Normalize("dann sieht sie");
        var analysis = new AnalysisResult(
            [.. text.Tokens.Select(t => new AnalyzedToken(t, []))],
            [],
            [],
            null,
            1.0,
            [new ResolvedWord(text.Tokens[0], WordRole.Unknown, null), new ResolvedWord(text.Tokens[1], WordRole.Unknown, null), new ResolvedWord(text.Tokens[2], WordRole.Word, plain)]);

        var capitals = GermanRecastPolicy.Instance.RestoredCapitals(text, analysis, Lexicon);

        Assert.Equal(["Dann"], capitals.Values);
    }

    [Fact]
    public void ANameTheGrammarBoundIsCapitalized()
    {
        var text = GermanNormalizer.Instance.Normalize("ich heiße anna");
        var analysis = new AnalysisResult(
            [.. text.Tokens.Select(t => new AnalyzedToken(t, []))],
            [],
            [],
            null,
            1.0,
            [new ResolvedWord(text.Tokens[0], WordRole.Unknown, null), new ResolvedWord(text.Tokens[1], WordRole.Unknown, null), new ResolvedWord(text.Tokens[2], WordRole.Name, null)]);

        var capitals = GermanRecastPolicy.Instance.RestoredCapitals(text, analysis, Lexicon);

        Assert.Equal("Anna", capitals[text.Tokens[2].Span.Start]);
    }

    [Theory]
    [InlineData("das ist schoen", "Das ist schön")]
    [InlineData("das ist schön", "Das ist schön")]
    [InlineData("das ist schon", "Das ist schon")]
    [InlineData("ich habe fuer sie", "Ich habe für sie")]
    [InlineData("ich habe fur sie", "Ich habe für sie")]
    public void RestoresTheUmlautsOfWordsTypedWithTwoLettersOrABareVowel(string typed, string expected)
    {
        Assert.Equal(expected, Recast(typed));
    }

    [Theory]
    [InlineData("die strasse", "Die Strasse")]
    [InlineData("der fuss", "Der Fuss")]
    public void TheSwissSsIsNotTurnedIntoASharpS(string typed, string expected)
    {
        // The lexicon holds the ß form only; the Swiss spelling is no error, so the ss stays. The noun still gets its capital.
        Assert.Equal(expected, Recast(typed));
    }

    [Fact]
    public void AGruessTypedWithSsKeepsTheSsAndGainsTheUmlaut()
    {
        Assert.Equal("Die Grüsse", Recast("die gruesse"));
    }

    [Fact]
    public void AGruessTypedWithSharpSKeepsIt()
    {
        Assert.Equal("Die Grüße", Recast("die grüße"));
    }

    [Fact]
    public void MissingCapitalsListsOnlyWhatTheWriterTypedWithoutACapital()
    {
        var (text, analysis) = Analyse("Ich habe einen hund und eine Katze");

        var missing = GermanRecastPolicy.MissingCapitals(text, analysis, Lexicon);

        Assert.Equal([(3, "Hund")], missing);
    }

    [Fact]
    public void MissingCapitalsIsEmptyForCorrectlyCapitalizedText()
    {
        var (text, analysis) = Analyse("Ich habe einen Hund");

        Assert.Empty(GermanRecastPolicy.MissingCapitals(text, analysis, Lexicon));
    }

    [Fact]
    public void MissingCapitalsIncludesTheSentenceStart()
    {
        var (text, analysis) = Analyse("ich habe einen Hund");

        Assert.Equal([(0, "Ich")], GermanRecastPolicy.MissingCapitals(text, analysis, Lexicon));
    }

    [Fact]
    public void ATokenThatIsNotAtItsSpanIsSkipped()
    {
        var text = new NormalizedText("ich habe hund", [new Token("hund", new TextSpan(0, 4), "Hund")]);
        var analysis = new AnalysisResult([new AnalyzedToken(text.Tokens[0], [])], [], [], null, 1.0);

        Assert.Empty(GermanRecastPolicy.Instance.RestoredCapitals(text, analysis, Lexicon));
        Assert.Empty(GermanRecastPolicy.Instance.RestoredSpellings(text, analysis, Lexicon));
    }

    [Fact]
    public void DigitsAndEmptyTextGetNoCapital()
    {
        Assert.Equal("3 katzen", Recast("3 katzen").ToLowerInvariant());
        var (text, analysis) = Analyse(string.Empty);

        Assert.Empty(GermanRecastPolicy.Instance.RestoredCapitals(text, analysis, Lexicon));
    }

    [Fact]
    public void TheRecastBuilderAppliesTheCapitalsToAPlayerLine()
    {
        var pack = GermanLanguagePack.Instance;
        var (text, analysis) = Analyse("ich habe einen hund");

        var recast = Vocaterra.Nlp.Response.Templates.RecastBuilder.Build(text, analysis, Lexicon, pack);

        Assert.Equal("Ich habe einen Hund", recast);
    }
}
