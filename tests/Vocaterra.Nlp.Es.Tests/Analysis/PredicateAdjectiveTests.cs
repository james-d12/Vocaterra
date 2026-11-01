namespace Vocaterra.Nlp.Es.Tests.Analysis;

using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Packs;
using Vocaterra.Nlp.Language.SlotGrammar;

/// <summary>
/// M62 (a), found by the game's V13: an adjective after ser or estar was not agreed with the copula's subject (la manzana es rojo, el pan es grandes gave nothing,
/// where the attributive quiero tres manzanas rojos gives gender.agree). It agrees now with the subject before or after the copula, past muy, más, menos or tan and in a
/// comparison; a name's gender is never guessed. Each mistake gave no error before M62; the clean neighbours are tests as well. Runs on the lexicon of the bundle.
/// </summary>
public class PredicateAdjectiveTests
{
    // The game's grammar of the intent describe (V13), which binds the adjective as [ADJ] at the lesson's prompts.
    private const string Describe = "(sí)? [ART]? [NOUN:object]? [VERB:ser] (muy)? [ADJ]";

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

    // ---- The brief's sentences ---------------------------------------------------------------------------------------------------------------------------

    // las manzanas son caro is the double slip of quiero tres manzanas rojo: gender.agree and noun.number, both with the one form that mends them.
    [Theory]
    [InlineData("la manzana es rojo", "gender.agree|rojo|roja")]
    [InlineData("el pan es grandes", "noun.number|grandes|grande")]
    [InlineData("las manzanas son caro", "gender.agree|caro|caras;noun.number|caro|caras")]
    [InlineData("la manzana es muy caro", "gender.agree|caro|cara")]
    [InlineData("la manzana es más barato que el pan", "gender.agree|barato|barata")]
    [InlineData("la sopa está frío", "gender.agree|frío|fría")]
    [InlineData("nosotros somos alto", "noun.number|alto|altos")]
    public void APredicateAdjectiveAgreesWithTheNounOrPronounBeforeTheCopula(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    // The attributive check gives the same two tags for the same double slip, so the predicate check matches it.
    [Theory]
    [InlineData("quiero tres manzanas rojo", "gender.agree|rojo|rojas;noun.number|rojo|rojas")]
    [InlineData("las manzanas son caro", "gender.agree|caro|caras;noun.number|caro|caras")]
    public void TheDoubleSlipGivesTheTagsOfTheAttributiveCheck(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    [Theory]
    [InlineData("la manzana es rojo", "gender.agree|rojo|roja")]
    [InlineData("el pan es grandes", "noun.number|grandes|grande")]
    [InlineData("la manzana es muy caro", "gender.agree|caro|cara")]
    [InlineData("el pan es muy cara", "gender.agree|cara|caro")]
    [InlineData("el pan es roja", "gender.agree|roja|rojo")]
    [InlineData("las manzanas son roja", "noun.number|roja|rojas")]
    public void TheGamesDescribeGrammarBindsTheAdjective(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence, Describe));
    }

    [Theory]
    [InlineData("la manzana es roja")]
    [InlineData("las manzanas son caras")]
    [InlineData("el pan es muy caro")]
    [InlineData("los panes son más baratos que las manzanas")]
    [InlineData("es caro")]
    [InlineData("es muy caro")]
    [InlineData("yo soy alto")]
    [InlineData("yo soy alta")]
    [InlineData("la casa es de luz")]
    [InlineData("el pan es para luz")]
    public void TheBriefsCleanNeighboursStayClean(string sentence)
    {
        Assert.Equal(string.Empty, Described(sentence));
    }

    [Fact]
    public void SerOfAPlaceIsStillSerEstar()
    {
        Assert.Equal("ser_estar|es|está", Described("es lejos"));
    }

    // ---- The subject: a pronoun with or without a gender, a name, a phrase after the adjective, the copula alone ------------------------------------------------

    // A pronoun of the third person or the plural carries its gender; yo, tú, usted and ustedes do not, so only their number is checked.
    [Theory]
    [InlineData("ella es alto", "gender.agree|alto|alta")]
    [InlineData("él es bonita", "gender.agree|bonita|bonito")]
    [InlineData("ellas son altos", "gender.agree|altos|altas")]
    [InlineData("ellos son alto", "noun.number|alto|altos")]
    [InlineData("nosotras somos altos", "gender.agree|altos|altas")]
    [InlineData("yo soy altos", "noun.number|altos|alto")]
    [InlineData("tú eres bonitas", "noun.number|bonitas|bonita")]
    [InlineData("ustedes son alto", "noun.number|alto|altos")]
    [InlineData("ella no es alto", "gender.agree|alto|alta")]
    public void APronounSubjectGivesItsGenderWhenItHasOne(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    [Theory]
    [InlineData("ella es alta")]
    [InlineData("él es alto")]
    [InlineData("nosotras somos altas")]
    [InlineData("tú eres alto")]
    [InlineData("tú eres alta")]
    [InlineData("usted es alto")]
    [InlineData("usted es alta")]
    [InlineData("ustedes son altas")]
    [InlineData("ustedes son altos")]
    [InlineData("estoy cansada")]
    [InlineData("estamos cansados")]
    public void APronounSubjectThatAgreesIsClean(string sentence)
    {
        Assert.Equal(string.Empty, Described(sentence));
    }

    // A name may be the person spoken to, and the core does not know a name's gender (Sol may be a woman): only the copula's number is checked after it. The game's
    // names that are also nouns carry their person's gender (VILLAGE_PLAN 5), so its correct lines are clean either way.
    [Theory]
    [InlineData("luz es alta")]
    [InlineData("luz es alto")]
    [InlineData("domingo es alto")]
    [InlineData("león es alto")]
    [InlineData("mar es alto")]
    [InlineData("sol es alta")]
    [InlineData("luz y domingo eran altos")]
    [InlineData("luz y mar eran altas")]
    public void ANameGivesNoGender(string sentence)
    {
        Assert.Equal(string.Empty, Described(sentence));
    }

    [Theory]
    [InlineData("luz es altos", "noun.number|altos|alto")]
    [InlineData("luz y domingo eran alto", "noun.number|alto|altos")]
    public void ANameStillGivesTheCopulasNumber(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    [Theory]
    [InlineData("es caro la manzana", "gender.agree|caro|cara")]
    [InlineData("es muy caro la manzana", "gender.agree|caro|cara")]
    [InlineData("está frío la sopa", "gender.agree|frío|fría")]
    [InlineData("es alto ella", "gender.agree|alto|alta")]
    public void ASubjectAfterTheAdjectiveIsItsSubject(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    [Theory]
    [InlineData("es cara la manzana")]
    [InlineData("está fría la sopa")]
    [InlineData("es caro el lunes")]
    public void ASubjectAfterTheAdjectiveThatAgreesIsClean(string sentence)
    {
        Assert.Equal(string.Empty, Described(sentence));
    }

    // With no subject the copula's own number is the subject's: es caro is clean, es caros is not; the gender is never guessed.
    [Theory]
    [InlineData("es caros", "noun.number|caros|caro")]
    [InlineData("somos alto", "noun.number|alto|altos")]
    [InlineData("son caro", "")]
    public void WithoutASubjectTheCopulasNumberIsChecked(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    // A predicate word that is an adjective and also a noun (alta, roja, cara) is read charitably as the noun when nothing else decides, as the reading resolver does
    // everywhere: él es alta is clean with no grammar. A grammar that binds the word as [ADJ] (the game's describe) or muy or tan before it makes it the adjective.
    [Theory]
    [InlineData("él es alta", "")]
    [InlineData("las manzanas son roja", "")]
    [InlineData("el pan es muy cara", "gender.agree|cara|caro")]
    [InlineData("él es tan alta", "gender.agree|alta|alto")]
    public void AnAdjectiveThatIsAlsoANounIsTheAdjectiveWhereSomethingSaysSo(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    [Theory]
    [InlineData("él es alta", "gender.agree|alta|alto")]
    [InlineData("tú eres altas", "noun.number|altas|alta")]
    public void APronounGrammarBindsTheAdjective(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence, "[PRON] [VERB:ser] (muy)? [ADJ]"));
    }

    // ---- More of the noun phrase subject -----------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("la manzana no es rojo", "gender.agree|rojo|roja")]
    [InlineData("los panes son caro", "noun.number|caro|caros")]
    [InlineData("las manzanas pequeñas son caro", "gender.agree|caro|caras;noun.number|caro|caras")]
    [InlineData("las manzanas rojas son caro", "gender.agree|caro|caras;noun.number|caro|caras")]
    [InlineData("las manzanas rojas es barata", "conj.person|es|son;noun.number|barata|baratas")]
    [InlineData("el agua está frío", "gender.agree|frío|fría")]
    [InlineData("la manzana era rojo", "gender.agree|rojo|roja")]
    [InlineData("la manzana es tan caro como el pan", "gender.agree|caro|cara")]
    [InlineData("la manzana es menos caro que el pan", "gender.agree|caro|cara")]
    [InlineData("la manzana es bonita y grandes", "noun.number|grandes|grande")]
    [InlineData("el pan y la manzana son baratas", "gender.agree|baratas|baratos")]
    public void ANounPhraseSubjectGivesItsGenderAndNumber(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    // M59 takes the phrase for the subject when the copula disagrees with it, so the adjective agrees with the phrase too (las manzanas son rojas).
    [Theory]
    [InlineData("las manzanas es roja", "conj.person|es|son;noun.number|roja|rojas")]
    [InlineData("la manzana son roja", "conj.person|son|es")]
    [InlineData("el manzana es roja", "gender.agree|el|la")]
    [InlineData("la pan es grande", "gender.agree|la|el")]
    public void AnotherSlipInTheSentenceIsNotDoubled(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence, Describe));
    }

    [Theory]
    [InlineData("la casa es grande")]
    [InlineData("el pan está rico")]
    [InlineData("es muy bonito")]
    [InlineData("las manzanas rojas son baratas")]
    [InlineData("el pan es más caro que la fruta")]
    [InlineData("la manzana es tan cara como el pan")]
    [InlineData("el agua está fría")]
    [InlineData("la manzana es roja y grande")]
    [InlineData("el pan y la manzana son baratos")]
    [InlineData("la manzana y la sopa son baratas")]
    [InlineData("la casa era grande")]
    [InlineData("es una manzana roja")]
    [InlineData("es buena gente")]
    [InlineData("el pan es comida")]
    [InlineData("el pan de luz es caro")]
    [InlineData("la mayoría de las manzanas son rojas")]
    [InlineData("los niños somos altos")]
    [InlineData("la fiesta es pasado mañana")]
    [InlineData("la casa es mía")]
    [InlineData("es mucho")]
    public void CorrectSentencesStayClean(string sentence)
    {
        Assert.Equal(string.Empty, Described(sentence));
    }
}
