namespace Vocaterra.Nlp.Language.Tests.Analysis;

using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Packs;

/// <summary>
/// M29j: the reading that <c>result.Words</c> reports. The mood of a form that is both imperative and indicative follows its position, <c>gracias</c> is the
/// interjection unless an article or a numeral precedes it, and <c>por favor</c> is one fixed expression. The agreement checks (M29c) are unchanged. The tests run
/// on the lexicon of the bundle, because <c>está</c> is imperative only in the imported entries and <c>gracias</c> is a plural noun only there.
/// </summary>
public class ReportedReadingsTests
{
    private static readonly Lazy<LinguisticAnalyzer> Analyzer = new(BuildAnalyzer);

    private static LinguisticAnalyzer BuildAnalyzer()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "content", "lexicon");
        var entries = new List<LexiconEntry>();
        foreach (var path in Directory.GetFiles(folder, "*.json").Order(StringComparer.Ordinal))
        {
            entries.AddRange(Lexicon.ReadFile(File.ReadAllBytes(path)).Entries);
        }

        return new LinguisticAnalyzer(Lexicon.FromEntries(entries), SpanishLanguagePack.Instance);
    }

    private static IReadOnlyList<ResolvedWord> Words(string sentence)
    {
        return Analyzer.Value.Analyze(TestTokens.From(sentence), []).Words!;
    }

    private static string Described(string sentence, int index)
    {
        var reading = Words(sentence)[index].Reading!;
        return $"{reading.Lemma}|{reading.Pos}|{reading.Person}|{reading.Number}|{reading.Tense}|{reading.Mood}";
    }

    [Theory]
    [InlineData("donde está el río", 1)]
    [InlineData("dónde está el río", 1)]
    [InlineData("el río está aquí", 2)]
    [InlineData("está el río", 0)]
    [InlineData("está la puerta", 0)]
    [InlineData("está él", 0)]
    [InlineData("yo digo que está bien", 3)]
    public void AFormThatIsNotAClauseInitialImperativeIsTheIndicative(string sentence, int index)
    {
        Assert.Equal("estar|verb|3|sg|pres|ind", Described(sentence, index));
    }

    [Theory]
    [InlineData("está quieto")]
    [InlineData("está en casa")]
    [InlineData("por favor está quieto")]
    public void AClauseInitialImperativeKeepsTheImperative(string sentence)
    {
        var reading = Words(sentence).Select(static w => w.Reading!).First(static r => r.Lemma == "estar");
        Assert.Equal("imp", reading.Mood);
        Assert.Equal("2", reading.Person);
    }

    [Fact]
    public void AClauseInitialImperativeOfAnotherVerbBeforeAnObjectStaysTheImperative()
    {
        var reading = Words("abre la puerta")[0].Reading!;
        Assert.Equal("abrir", reading.Lemma);
        Assert.Equal("imp", reading.Mood);
        Assert.Equal("ind", Words("ella abre la puerta")[1].Reading!.Mood);
    }

    [Theory]
    [InlineData("gracias")]
    [InlineData("muchas gracias")]
    [InlineData("mucho gracias")]
    [InlineData("no mucho gracias")]
    public void GraciasIsTheInterjection(string sentence)
    {
        var word = Words(sentence).Last();
        Assert.Equal("gracias", word.Reading!.Lemma);
        Assert.Equal("interj", word.Reading.Pos);
    }

    [Theory]
    [InlineData("las gracias")]
    [InlineData("dos gracias")]
    public void GraciasAfterAnArticleOrANumeralIsTheNoun(string sentence)
    {
        var reading = Words(sentence).Last().Reading!;
        Assert.Equal("gracia", reading.Lemma);
        Assert.Equal("noun", reading.Pos);
    }

    [Theory]
    [InlineData("mucho gracias", new[] { "gender.agree|mucho|muchas", "noun.number|mucho|muchas" })]
    [InlineData("no mucho gracias", new[] { "gender.agree|mucho|muchas", "noun.number|mucho|muchas" })]
    [InlineData("muchas gracias", new string[0])]
    public void TheAgreementChecksStillReadTheNounInGracias(string sentence, string[] expected)
    {
        var errors = Analyzer.Value.Analyze(TestTokens.From(sentence), []).Errors.Select(static e => $"{e.Tag}|{e.Actual}|{e.Expected}");
        Assert.Equal(expected, errors);
    }

    [Fact]
    public void PorFavorIsOneFixedExpressionWithEachTokenKeepingItsSpan()
    {
        var words = Words("quiero agua por favor");
        Assert.Equal(4, words.Count);
        for (var i = 2; i < 4; i++)
        {
            Assert.Equal("por favor", words[i].Reading!.Lemma);
            Assert.Equal("interj", words[i].Reading!.Pos);
            Assert.Equal(WordRole.Word, words[i].Role);
        }

        Assert.Equal("por", words[2].Token.Text);
        Assert.Equal("favor", words[3].Token.Text);
        Assert.Equal(12, words[2].Token.Span.Start);
        Assert.Equal(16, words[3].Token.Span.Start);
        Assert.Equal("agua", words[1].Reading!.Lemma);
    }

    [Fact]
    public void PorFavorAtTheStartIsOneExpressionAndTheImperativeFollows()
    {
        var words = Words("por favor abre la puerta");
        Assert.All(words.Take(2), static w => Assert.Equal("por favor", w.Reading!.Lemma));
        Assert.Equal("imp", words[2].Reading!.Mood);
    }

    [Fact]
    public void FavorAndPorOutsideTheExpressionAreReadAsTheLexiconReadsThem()
    {
        Assert.Equal("favor", Words("el favor")[1].Reading!.Lemma);
        Assert.Equal("por", Words("por la casa")[0].Reading!.Lemma);
    }
}
