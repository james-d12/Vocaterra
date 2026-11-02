namespace Vocaterra.Nlp.Es.Tests.Analysis;

using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Packs;
using Vocaterra.Nlp.Language.SlotGrammar;

/// <summary>
/// M69, M58's left point: a word of another class that is also an irregular verb's form with the stem change missed (<c>podo</c>, <c>pode</c>, <c>podes</c>, forms of
/// <c>podar</c>; the pronoun <c>esto</c>) was read as the slip only under a slot grammar that names the verb, so <c>yo podo ir al río</c> and <c>yo esto cansado</c> were
/// clean. Where no reading of the word as written fits its neighbours, it is now the slip without a grammar (<c>conj.irregular</c>); the demonstrative and <c>podar</c>
/// keep their readings wherever they have one. Each mistake gave no error before M69; the clean neighbours are tests as well. Runs on the lexicon of the bundle.
/// </summary>
public class SlipWithoutGrammarTests
{
    private const string HelpGrammar = "me [VERB:poder] [VERB:ayudar]";

    private static readonly Lazy<LinguisticAnalyzer> Analyzer = new(BuildAnalyzer);

    private static LinguisticAnalyzer BuildAnalyzer()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "content", "lexicon");
        var entries = new List<LexiconEntry>();
        foreach (var path in Directory.GetFiles(folder, "*.json").Order(StringComparer.Ordinal))
        {
            entries.AddRange(Lexicon.ReadFile(File.ReadAllBytes(path)).Entries);
        }

        return new LinguisticAnalyzer(SpanishLexicon.FromEntries(entries), SpanishLanguagePack.Instance);
    }

    private static string Described(string sentence, params string[] grammars)
    {
        var parsed = grammars.Select(static g => SlotGrammar.Parse(g)).ToList();
        return string.Join(';', Analyzer.Value.Analyze(TestTokens.From(sentence), parsed).Errors.Select(static e => $"{e.Tag}|{e.Actual}|{e.Expected}"));
    }

    // podar takes no infinitive, so a form of it right before one is poder's slip.
    [Theory]
    [InlineData("yo podo ir al río", "conj.irregular|podo|puedo")]
    [InlineData("no podo ir", "conj.irregular|podo|puedo")]
    [InlineData("podo comprar pan", "conj.irregular|podo|puedo")]
    [InlineData("tú podes venir", "conj.irregular|podes|puedes")]
    [InlineData("él pode venir mañana", "conj.irregular|pode|puede")]
    [InlineData("me pode ayudar", "conj.irregular|pode|puede")]
    public void AFormOfPodarBeforeAnInfinitiveIsPoder(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    [Theory]
    [InlineData("yo podo el árbol")]
    [InlineData("podo los árboles")]
    [InlineData("yo podo")]
    [InlineData("yo puedo ir al río")]
    [InlineData("no puedo ir")]
    [InlineData("tú puedes venir")]
    [InlineData("yo solo quiero pan")]
    [InlineData("solo comer pan")]
    public void PodarAndItsNeighboursThatStayClean(string sentence)
    {
        Assert.Equal(string.Empty, Described(sentence));
    }

    // The grammar of M58 still reads it as before, and the slip is reported once.
    [Fact]
    public void UnderTheHelpGrammarTheSlipIsReportedOnce()
    {
        Assert.Equal("conj.irregular|pode|puede", Described("me pode ayudar", HelpGrammar));
    }

    // esto for estoy: after yo before an adjective, bien, mal, an adverb of place or en; at the start before an adjective that takes estar; no finite verb after it.
    [Theory]
    [InlineData("yo esto cansado", "conj.irregular|esto|estoy")]
    [InlineData("yo esto cansada", "conj.irregular|esto|estoy")]
    [InlineData("yo no esto cansado", "conj.irregular|esto|estoy")]
    [InlineData("yo esto bien", "conj.irregular|esto|estoy")]
    [InlineData("yo esto muy bien", "conj.irregular|esto|estoy")]
    [InlineData("yo esto en casa", "conj.irregular|esto|estoy")]
    [InlineData("yo esto aquí", "conj.irregular|esto|estoy")]
    [InlineData("hola yo esto bien", "conj.irregular|esto|estoy")]
    [InlineData("esto cansado", "conj.irregular|esto|estoy")]
    [InlineData("esto muy contento", "conj.irregular|esto|estoy")]
    [InlineData("esto enfermo", "conj.irregular|esto|estoy")]
    public void EstoWhereOnlyEstoyFitsIsTheSlip(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    [Theory]
    [InlineData("esto es pan")]
    [InlineData("esto es caro")]
    [InlineData("quiero esto")]
    [InlineData("qué es esto")]
    [InlineData("esto no me gusta")]
    [InlineData("yo esto no lo sé")]
    [InlineData("esto también")]
    [InlineData("esto de aquí es pan")]
    [InlineData("esto bien")]
    [InlineData("esto aquí")]
    [InlineData("esto rojo")]
    [InlineData("y esto")]
    [InlineData("para mí esto es caro")]
    [InlineData("yo estoy cansado")]
    [InlineData("estoy bien")]
    [InlineData("yo esto en casa lo hago")]
    public void EstoAndItsNeighboursThatStayClean(string sentence)
    {
        Assert.Equal(string.Empty, Described(sentence));
    }
}
