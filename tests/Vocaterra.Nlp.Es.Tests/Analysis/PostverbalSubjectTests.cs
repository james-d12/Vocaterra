namespace Vocaterra.Nlp.Es.Tests.Analysis;

using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Packs;
using Vocaterra.Nlp.Language.SlotGrammar;

/// <summary>
/// M56, found by the game's V10, V11 and V13: three agreement checks that were silent where a learner errs. (a) ser where a place takes estar: ser with lejos or cerca
/// and dónde with ser and a place give ser_estar; (b) a subject after its verb (after estar, an experiencer verb such as gustar with its pronoun, and a verb of price
/// after cuánto) is agreed with the verb, conj.person. Each mistake gave no error before M56; the clean neighbours are tests as well. Runs on the lexicon of the bundle.
/// </summary>
public class PostverbalSubjectTests
{
    // The price grammar of the game's bundle (intent ask_price), so that cuesta is bound as the verb as it is at the baker's prompt.
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

    // ---- (a) ser where a place takes estar ----------------------------------------------------------------------------------------------------------------

    // son is also a noun, and without a subject pronoun or dónde before it (son cerca) the reading resolver takes the noun, as it does for son with an adjective.
    [Theory]
    [InlineData("es lejos", "ser_estar|es|está")]
    [InlineData("es cerca", "ser_estar|es|está")]
    [InlineData("es muy lejos", "ser_estar|es|está")]
    [InlineData("ellos son cerca", "ser_estar|son|están")]
    [InlineData("eran lejos", "ser_estar|eran|estaban")]
    [InlineData("la herrería no es lejos", "ser_estar|es|está")]
    [InlineData("es lejos la herrería", "ser_estar|es|está")]
    [InlineData("mi casa es cerca de la plaza", "ser_estar|es|está")]
    public void SerWithLejosOrCercaExpectsEstar(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    [Theory]
    [InlineData("dónde es la herrería", "ser_estar|es|está")]
    [InlineData("donde es la herrería", "ser_estar|es|está")]
    [InlineData("dónde es mi casa", "ser_estar|es|está")]
    [InlineData("dónde es esta casa", "ser_estar|es|está")]
    [InlineData("dónde era la casa", "ser_estar|era|estaba")]
    [InlineData("no sé dónde es la herrería", "ser_estar|es|está")]
    [InlineData("dónde son las casas", "ser_estar|son|están")]
    [InlineData("dónde son la casa", "ser_estar|son|está")]
    public void DondeWithSerAndAPlaceExpectsEstarAgreeingWithThePlace(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    // The event reading: an event's place takes ser (it says where the event takes place), and the core tells an event by its noun (SpanishAnalysisTables.IsEventNoun).
    // ¿dónde es? alone may be asked of an event, so it is left clean; so are origin (de dónde), an adjective (lejano), a time (cerca de las 3) and ir (fue lejos).
    [Theory]
    [InlineData("la fiesta es en la plaza")]
    [InlineData("la fiesta es cerca")]
    [InlineData("la fiesta es cerca de la plaza")]
    [InlineData("es lejos la fiesta")]
    [InlineData("dónde es la fiesta")]
    [InlineData("dónde es la clase")]
    [InlineData("dónde es la reunión")]
    [InlineData("dónde es")]
    [InlineData("de dónde es tu padre")]
    [InlineData("es lejano")]
    [InlineData("está lejos")]
    [InlineData("está muy cerca")]
    [InlineData("son cerca de las 3")]
    [InlineData("fue lejos")]
    [InlineData("es de lejos")]
    [InlineData("dónde está la herrería")]
    [InlineData("cómo son las casas")]
    public void SerOfAnEventAndEstarOfAPlaceStayClean(string sentence)
    {
        Assert.Equal(string.Empty, Described(sentence));
    }

    // ---- (b) a subject after its verb ---------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("dónde están la herrería", "conj.person|están|está")]
    [InlineData("dónde está las casas", "conj.person|está|están")]
    [InlineData("aquí están el pan", "conj.person|están|está")]
    [InlineData("dónde quedan la herrería", "conj.person|quedan|queda")]
    [InlineData("me gusta las manzanas", "conj.person|gusta|gustan")]
    [InlineData("me gustan el pan", "conj.person|gustan|gusta")]
    [InlineData("me gusta mucho las manzanas", "conj.person|gusta|gustan")]
    [InlineData("te gustan el pan", "conj.person|gustan|gusta")]
    [InlineData("me falta 2 monedas", "conj.person|falta|faltan")]
    [InlineData("a mi hermano le gustan el pan", "conj.person|gustan|gusta")]
    [InlineData("creo que me gustan el pan", "conj.person|gustan|gusta")]
    [InlineData("cuánto valen el pan", "conj.person|valen|vale")]
    [InlineData("cuánto me cuesta los panes", "conj.person|cuesta|cuestan")]
    public void AVerbBeforeItsSubjectAgreesWithIt(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    [Theory]
    [InlineData("cuánto cuestan el pan", "conj.person|cuestan|cuesta")]
    [InlineData("cuánto cuesta los panes", "conj.person|cuesta|cuestan")]
    [InlineData("cuánto cuesta 2 panes", "conj.person|cuesta|cuestan")]
    public void ThePriceQuestionAgreesItsVerbWithTheThingPriced(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence, PriceGrammar));
    }

    [Theory]
    [InlineData("me gustan las manzanas")]
    [InlineData("me gusta el pan")]
    [InlineData("me gusta mucho el pan")]
    [InlineData("dónde están las casas")]
    [InlineData("dónde está la panadería")]
    [InlineData("dónde queda la herrería")]
    [InlineData("aquí está el pan")]
    [InlineData("me faltan 2 monedas")]
    [InlineData("me gusta los lunes y los martes")]
    [InlineData("me gusta el pan y la fruta")]
    [InlineData("me gustan el pan y la fruta")]
    [InlineData("me gustan la mayoría de las frutas")]
    [InlineData("el pan me gusta")]
    [InlineData("están el lunes en casa")]
    [InlineData("cuestan 2 monedas")]
    [InlineData("los panes cuestan 2 monedas")]
    [InlineData("hay 2 panes")]
    [InlineData("hay un pan")]
    public void ASubjectThatAgreesAndANounPhraseThatIsNoSubjectStayClean(string sentence)
    {
        Assert.Equal(string.Empty, Described(sentence));
    }

    [Theory]
    [InlineData("cuánto cuesta el pan")]
    [InlineData("cuánto cuestan los panes")]
    [InlineData("cuánto cuestan 2 panes")]
    [InlineData("cuánto cuesta")]
    public void ThePriceQuestionThatAgreesStaysClean(string sentence)
    {
        Assert.Equal(string.Empty, Described(sentence, PriceGrammar));
    }

    // A pronoun after a preposition is no subject: before M56 the subject-pronoun check paired it with the verb, so a ella le gustan las manzanas gave conj.person
    // (expected gusta) and a nosotros nos gusta el pan gave gustamos.
    [Theory]
    [InlineData("a ella le gustan las manzanas")]
    [InlineData("a nosotros nos gusta el pan")]
    [InlineData("a ellos les gusta el pan")]
    [InlineData("a él le gustan las manzanas")]
    public void APronounAfterAPrepositionIsNoSubject(string sentence)
    {
        Assert.Equal(string.Empty, Described(sentence));
    }

    [Theory]
    [InlineData("a ella le gusta las manzanas", "conj.person|gusta|gustan")]
    [InlineData("a nosotros nos gustan el pan", "conj.person|gustan|gusta")]
    public void AnExperiencerAfterAPrepositionLeavesTheSubjectAfterTheVerb(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }
}
