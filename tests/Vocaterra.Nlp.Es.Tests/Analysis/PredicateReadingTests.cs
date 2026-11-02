namespace Vocaterra.Nlp.Es.Tests.Analysis;

using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Packs;
using Vocaterra.Nlp.Language.SlotGrammar;

/// <summary>
/// M69, M62's left points: (a) an adjective that is also a noun after the copula, with no grammar and no <c>muy</c>, was read as the noun (<c>él es alta</c>: the
/// discharge; <c>la casa es bonito</c>: the tuna), so its agreement was not checked; (b) <c>son</c> after a lone name was read as the noun (<c>luz son alta</c>); (c) a
/// subject with <c>de …</c> gave the predicate the copula's number only (<c>la casa de los niños es pequeño</c>). Each mistake gave no error before M69; the clean
/// neighbours are tests as well. Runs on the lexicon of the bundle.
/// </summary>
public class PredicateReadingTests
{
    // The game's describe grammar, under which M62 already read the word as the adjective.
    private const string DescribeGrammar = "[PRON] [VERB:ser] [ADJ]";

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

    // ---- (a) an adjective that is also a noun after the copula ---------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("él es alta", "gender.agree|alta|alto")]
    [InlineData("ella es bonito", "gender.agree|bonito|bonita")]
    [InlineData("la casa es bonito", "gender.agree|bonito|bonita")]
    [InlineData("el pan es cara", "gender.agree|cara|caro")]
    [InlineData("ellos son alta", "gender.agree|alta|altos;noun.number|alta|altos")]
    [InlineData("él es rica", "gender.agree|rica|rico")]
    [InlineData("el pan es nueva", "gender.agree|nueva|nuevo")]
    [InlineData("él no es alta", "gender.agree|alta|alto")]
    [InlineData("la casa grande es bonito", "gender.agree|bonito|bonita")]
    [InlineData("él está lista", "gender.agree|lista|listo")]
    [InlineData("es bonitos", "noun.number|bonitos|bonito")]
    public void AnAdjectiveThatIsAlsoANounAfterTheCopulaAgreesWithTheSubject(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    // A noun that agrees with the subject may be a predicate noun; a participle's noun may be one too (food, drink); a preposition after the word makes it a noun with
    // its complement; an adjective outside the pack's list keeps the charitable reading; and no subject gives no gender.
    [Theory]
    [InlineData("él es alto")]
    [InlineData("ella es alta")]
    [InlineData("ella es bonita")]
    [InlineData("la casa es bonita")]
    [InlineData("el pan es caro")]
    [InlineData("ella es médico")]
    [InlineData("él es médico")]
    [InlineData("el pan es comida")]
    [InlineData("el vino es bebida")]
    [InlineData("ellos son pareja")]
    [InlineData("es falta de respeto")]
    [InlineData("es alta")]
    [InlineData("es bonito")]
    [InlineData("yo soy alta")]
    [InlineData("tú eres alto")]
    [InlineData("es cara la manzana")]
    [InlineData("la manzana es cara")]
    [InlineData("ellas son altas")]
    public void AnAdjectiveOrANounAfterTheCopulaThatStaysClean(string sentence)
    {
        Assert.Equal(string.Empty, Described(sentence));
    }

    [Theory]
    [InlineData("él es alta", "gender.agree|alta|alto")]
    [InlineData("ella es alta", "")]
    public void UnderTheDescribeGrammarTheResultIsTheSame(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence, DescribeGrammar));
    }

    // ---- (b) son after a lone name -------------------------------------------------------------------------------------------------------------------------

    // A lone name may be the person spoken to (Luz, son altas), so its verb is not checked in general; a singular predicate that agrees with the name shows that the name is
    // the subject, and then the plural copula is the slip.
    [Theory]
    [InlineData("luz son alta", "conj.person|son|es")]
    [InlineData("pan son caro", "conj.person|son|es")]
    [InlineData("luz son muy alta", "conj.person|son|es")]
    [InlineData("luz eran alta", "conj.person|eran|era")]
    public void SonAfterALoneNameBeforeAPredicateOfTheNamesNumber(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    [Theory]
    [InlineData("luz son altas")]
    [InlineData("luz es alta")]
    [InlineData("luz es alto")]
    [InlineData("domingo es alto")]
    [InlineData("luz son las tres")]
    [InlineData("manzanas son caras")]
    public void SonAfterALoneNameThatStaysClean(string sentence)
    {
        Assert.Equal(string.Empty, Described(sentence));
    }

    [Fact]
    public void ALoneNameBeforeSingularSerKeepsTheNumberCheck()
    {
        Assert.Equal("noun.number|altos|alto", Described("luz es altos"));
    }

    // ---- (c) a subject with de -----------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("la casa de los niños es bonito", "gender.agree|bonito|bonita")]
    [InlineData("la casa de los niños es pequeño", "gender.agree|pequeño|pequeña")]
    [InlineData("la casa de luz es bonito", "gender.agree|bonito|bonita")]
    [InlineData("el pan de la tienda es buena", "gender.agree|buena|bueno")]
    [InlineData("el pan del pueblo es buena", "gender.agree|buena|bueno")]
    [InlineData("las casas de luz son bonitos", "gender.agree|bonitos|bonitas")]
    [InlineData("la casa de la hermana de luz es pequeño", "gender.agree|pequeño|pequeña")]
    [InlineData("la casa de los niños no es muy pequeño", "gender.agree|pequeño|pequeña")]
    [InlineData("la casa de los niños es pequeños", "gender.agree|pequeños|pequeña;noun.number|pequeños|pequeña")]
    public void ASubjectWithDeGivesThePredicateItsGender(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    // A noun of part or quantity agrees with the phrase after de as well; a copula of another number than the first phrase leaves the subject untold, and its own number
    // decides as before; the correct sentences stay clean.
    [Theory]
    [InlineData("la casa de los niños es bonita")]
    [InlineData("el pan de la tienda es bueno")]
    [InlineData("las casas de luz son bonitas")]
    [InlineData("el hermano de luz es alto")]
    [InlineData("la mayoría de los niños son altos")]
    [InlineData("la mitad de las manzanas son rojas")]
    [InlineData("las casas de luz es bonita")]
    [InlineData("la casa de luz son bonitas")]
    [InlineData("el pan de luz es caro")]
    public void ASubjectWithDeThatStaysClean(string sentence)
    {
        Assert.Equal(string.Empty, Described(sentence));
    }
}
