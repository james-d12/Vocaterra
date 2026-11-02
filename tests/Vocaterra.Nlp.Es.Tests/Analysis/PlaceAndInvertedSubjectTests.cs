namespace Vocaterra.Nlp.Es.Tests.Analysis;

using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Packs;
using Vocaterra.Nlp.Language.SlotGrammar;

/// <summary>
/// M69, M56's left points: (a) <c>son</c> before <c>lejos</c> or <c>cerca</c> with no subject before it was read as the noun (<c>son cerca</c> was clean); (b) <c>ser</c> with
/// <c>en</c> and a place was not checked (<c>la tienda es en el pueblo</c>); (c) the first and second persons after the verb: a subject pronoun after the verb of a
/// question that disagrees with it (<c>¿dónde estás yo?</c>), and <c>estar</c> or a verb of price in the first or second person singular with the phrase that is its
/// subject after it (<c>¿dónde estás la herrería?</c>). Each mistake gave no error before M69; the clean neighbours are tests as well. Runs on the lexicon of the bundle.
/// </summary>
public class PlaceAndInvertedSubjectTests
{
    // The game's directions and price grammars, so the words are bound as at the villager's and the farmer's prompts.
    private const string DirectionsGrammar = "dónde [VERB:estar] [ART]? [NOUN:place]";
    private const string PriceGrammar = "cuánto [VERB:costar] [ART]? [NUM:qty]? [NOUN:item]?";

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

    // ---- (a) son before lejos or cerca -------------------------------------------------------------------------------------------------------------------

    // Before M69 the reading resolver took the noun son (a sound) where no subject stood before it, so each was clean.
    [Theory]
    [InlineData("son cerca", "ser_estar|son|están")]
    [InlineData("son lejos", "ser_estar|son|están")]
    [InlineData("son muy lejos", "ser_estar|son|están")]
    [InlineData("no son lejos", "ser_estar|son|están")]
    [InlineData("son cerca del río", "ser_estar|son|están")]
    [InlineData("era lejos", "ser_estar|era|estaba")]
    public void SonOrEraBeforeLejosOrCercaIsTheCopula(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    // A determiner before it makes it the noun; an hour after cerca de takes ser; an event as the subject takes ser.
    [Theory]
    [InlineData("el son cerca de la plaza")]
    [InlineData("son cerca de las tres")]
    [InlineData("la fiesta es cerca")]
    [InlineData("están cerca")]
    [InlineData("estamos lejos")]
    [InlineData("las casas están lejos")]
    public void SonAndItsNeighboursThatStayClean(string sentence)
    {
        Assert.Equal(string.Empty, Described(sentence));
    }

    // ---- (b) ser with en and a place -----------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("la tienda es en el pueblo", "ser_estar|es|está")]
    [InlineData("la herrería es en la plaza", "ser_estar|es|está")]
    [InlineData("mi casa es en el pueblo", "ser_estar|es|está")]
    [InlineData("el pan es en la tienda", "ser_estar|es|está")]
    [InlineData("la tienda no es en el pueblo", "ser_estar|es|está")]
    [InlineData("la tienda era en el pueblo", "ser_estar|era|estaba")]
    [InlineData("las tiendas son en el pueblo", "ser_estar|son|están")]
    [InlineData("la tienda y la herrería son en el pueblo", "ser_estar|son|están")]
    [InlineData("la casa grande es en la plaza", "ser_estar|es|está")]
    [InlineData("yo soy en casa", "ser_estar|soy|estoy")]
    [InlineData("ella es en la tienda", "ser_estar|es|está")]
    [InlineData("nosotros somos en el río", "ser_estar|somos|estamos")]
    [InlineData("tú eres en una tienda", "ser_estar|eres|estás")]
    [InlineData("los niños son en el río", "ser_estar|son|están")]
    public void SerWithEnAndAPlaceExpectsEstar(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    // An event's place takes ser (it says where the event takes place); a lone name may be the person spoken to; a sentence with no subject may be about an event; a time,
    // a manner or an adverbial phrase after en is no place; fue is also ir; estar is right; de is no en.
    [Theory]
    [InlineData("la fiesta es en la plaza")]
    [InlineData("la boda es en el pueblo")]
    [InlineData("la cena es en mi casa")]
    [InlineData("el concierto es en la plaza")]
    [InlineData("el examen es en la escuela")]
    [InlineData("la visita es en el pueblo")]
    [InlineData("luz es en la plaza")]
    [InlineData("es en la plaza")]
    [InlineData("es en serio")]
    [InlineData("el pago es en monedas")]
    [InlineData("la clase es en la tarde")]
    [InlineData("la tienda es en el verano")]
    [InlineData("él es en el fondo bueno")]
    [InlineData("la tienda es en dos días")]
    [InlineData("la tienda fue en el pueblo")]
    [InlineData("la tienda está en el pueblo")]
    [InlineData("yo estoy en casa")]
    [InlineData("la casa es de luz")]
    [InlineData("el pan es para la tienda")]
    [InlineData("la casa que es en el pueblo es grande")]
    public void SerWithEnThatStaysClean(string sentence)
    {
        Assert.Equal(string.Empty, Described(sentence));
    }

    // ---- (c) the first and second persons after the verb ---------------------------------------------------------------------------------------------------

    // A subject pronoun after the verb of a question (or a relative) is its subject. Limit: a verb that is also a noun after qué is read charitably as the noun when the
    // verb would disagree (qué haces yo stays clean: haces is also the plural of haz), since qué may be a determiner (¿qué casa?).
    [Theory]
    [InlineData("dónde estás yo", "conj.person|estás|estoy")]
    [InlineData("dónde está tú", "conj.person|está|estás")]
    [InlineData("cómo está tú", "conj.person|está|estás")]
    [InlineData("cómo estás usted", "conj.person|estás|está")]
    [InlineData("qué quiere tú", "conj.person|quiere|quieres")]
    [InlineData("dónde vives usted", "conj.person|vives|vive")]
    [InlineData("donde estas yo", "conj.person|estas|estoy")]
    [InlineData("dónde están tú", "conj.person|están|estás")]
    [InlineData("cuándo viene ellos", "conj.person|viene|vienen")]
    [InlineData("quién es tú", "conj.person|es|eres")]
    public void ASubjectPronounAfterTheVerbOfAQuestionAgreesWithIt(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    [Theory]
    [InlineData("dónde estás tú")]
    [InlineData("cómo está usted")]
    [InlineData("qué quieres tú")]
    [InlineData("dónde vive usted")]
    [InlineData("qué haces tú")]
    [InlineData("cómo estáis vosotros")]
    [InlineData("dónde están ustedes")]
    [InlineData("cómo te llamas tú")]
    [InlineData("quién eres tú")]
    [InlineData("dónde están tú y luz")]
    [InlineData("qué quieres yo te doy pan")]
    [InlineData("dónde está yo no sé")]
    [InlineData("cómo es tu casa")]
    [InlineData("me gustas tú")]
    [InlineData("mira tú")]
    [InlineData("la casa donde vive él")]
    [InlineData("lo que tienes tú")]
    [InlineData("qué te gusta")]
    public void APronounAfterAVerbThatStaysClean(string sentence)
    {
        Assert.Equal(string.Empty, Described(sentence));
    }

    // estar, and a verb of price after cuánto, in the first or second person singular with a phrase an article opens after it: the phrase is the subject.
    [Theory]
    [InlineData("dónde estás la herrería", "conj.person|estás|está")]
    [InlineData("dónde estoy la plaza", "conj.person|estoy|está")]
    [InlineData("dónde estás los panes", "conj.person|estás|están")]
    [InlineData("cuánto cuestas el pan", "conj.person|cuestas|cuesta")]
    [InlineData("cuánto cuestas las manzanas", "conj.person|cuestas|cuestan")]
    public void AVerbOfTheFirstOrSecondPersonWithItsSubjectAfterIt(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    [Theory]
    [InlineData("dónde estás la herrería", DirectionsGrammar, "conj.person|estás|está")]
    [InlineData("cuánto cuestas el pan", PriceGrammar, "conj.person|cuestas|cuesta")]
    [InlineData("dónde está la herrería", DirectionsGrammar, "")]
    [InlineData("cuánto cuesta el pan", PriceGrammar, "")]
    public void TheSameUnderTheGamesGrammars(string sentence, string grammar, string expected)
    {
        Assert.Equal(expected, Described(sentence, grammar));
    }

    // A possessive or a demonstrative may open the name of the person spoken to (¿dónde estás, mi amor?), a plural verb may be of the people the phrase names (¿dónde
    // estamos los niños?: we children), and a phrase of time is no subject.
    [Theory]
    [InlineData("dónde estás mi amor")]
    [InlineData("dónde estamos los niños")]
    [InlineData("dónde estás el lunes")]
    [InlineData("yo estoy en la casa")]
    [InlineData("dónde está la herrería")]
    [InlineData("cuánto cuesta el pan")]
    public void AVerbOfTheFirstOrSecondPersonThatStaysClean(string sentence)
    {
        Assert.Equal(string.Empty, Described(sentence));
    }
}
