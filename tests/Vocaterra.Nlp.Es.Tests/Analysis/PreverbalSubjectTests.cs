namespace Vocaterra.Nlp.Es.Tests.Analysis;

using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Packs;
using Vocaterra.Nlp.Language.SlotGrammar;

/// <summary>
/// M59 (a), found by the game's V12 and V13: <c>conj.person</c> looked only after a subject pronoun, so a noun phrase before its verb was not checked
/// (<c>las manzanas es roja</c>, <c>los niños come pan</c> gave nothing). A noun phrase that opens its clause is now the subject of a third-person verb after it. A lone
/// name is never checked: a transcript has no comma, and <c>Luz quiero pan</c> is right Spanish with the name said to the listener (<c>Luz, quiero pan</c>); nor is a
/// verb of the first or second person after any noun phrase. Each mistake gave no error before M59, and each lone name gave a false one at <c>a00b2d1</c>; the clean
/// neighbours are tests as well. Runs on the lexicon of the bundle, with the game's grammars where the bundle's prompts bind the verb (<c>trabajo</c> and <c>son</c> are
/// also nouns, and without a grammar the charitable reading takes the noun).
/// </summary>
public class PreverbalSubjectTests
{
    // The game's grammars (state_action, describe), so that the verb is bound as it is at the lesson's prompts.
    private const string Works = "(sí)? (mañana|ayer|hoy|luego|después)? (domingo|luz|mar|león)? [VERB:trabajar] en [ART] [NOUN:place] (mañana|ayer|hoy|luego|después)?";
    private const string Makes = "(sí)? (mañana|ayer|hoy|luego|después)? (domingo|luz|mar|león)? [VERB:hacer] [ART]? [NUM:qty]? [NOUN:item] (mañana|ayer|hoy|luego|después)?";
    private const string WillMake = "(mañana|ayer|hoy|luego|después)? (domingo|luz|mar|león)? [VERB:ir] a [VERB:hacer] [ART]? [NUM:qty]? [NOUN:item] (mañana|ayer|hoy|luego|después)?";
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

    // ---- The game's sentences (V12, V13) ------------------------------------------------------------------------------------------------------------------

    // M62: the predicate adjective agrees with the phrase too, so las manzanas es roja also gives rojas (las manzanas son rojas); la manzana son roja gives es alone.
    [Theory]
    [InlineData("las manzanas es roja", Describe, "conj.person|es|son;noun.number|roja|rojas")]
    [InlineData("la manzana son roja", Describe, "conj.person|son|es")]
    [InlineData("sí las manzanas es roja", Describe, "conj.person|es|son;noun.number|roja|rojas")]
    public void TheGamesNounPhraseBeforeItsVerbIsChecked(string sentence, string grammar, string expected)
    {
        Assert.Equal(expected, Described(sentence, grammar));
    }

    [Theory]
    [InlineData("domingo trabaja en la herrería", Works)]
    [InlineData("hoy domingo trabaja en la herrería", Works)]
    [InlineData("domingo va a hacer un hacha", WillMake)]
    [InlineData("luz hizo pan", Makes)]
    [InlineData("domingo hace un hacha", Makes)]
    [InlineData("las manzanas son rojas", Describe)]
    [InlineData("la manzana es roja", Describe)]
    [InlineData("trabajo en la herrería", Works)]
    [InlineData("hice pan", Makes)]
    public void TheGamesSentencesThatAgreeAreClean(string sentence, string grammar)
    {
        Assert.Equal(string.Empty, Described(sentence, grammar));
    }

    // ---- A lone name may be the listener --------------------------------------------------------------------------------------------------------------------

    // One bare noun that nothing is joined to may be the person spoken to, in any person and number (Luz, ¿tienen pan? asks ustedes), so its verb is not checked; each of
    // these gave conj.person at a00b2d1. The game's lesson steps judge a retelling with their own person check.
    [Theory]
    [InlineData("luz quiero pan")]
    [InlineData("luz tienes pan")]
    [InlineData("luz tienen pan")]
    [InlineData("luz queremos pan")]
    [InlineData("luz necesito pan")]
    [InlineData("domingo tengo madera")]
    [InlineData("domingo necesito un hacha")]
    [InlineData("domingo tienes un hacha")]
    [InlineData("león quiero tres manzanas")]
    [InlineData("mar pescaste en el río")]
    [InlineData("domingo no tengo pan")]
    [InlineData("cuando domingo tengo pan")]
    public void ALoneNameBeforeItsVerbIsNotChecked(string sentence)
    {
        Assert.DoesNotContain("conj.person", Described(sentence), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("domingo trabajo en la herrería", Works)]
    [InlineData("hoy domingo trabajo en la herrería", Works)]
    [InlineData("domingo voy a hacer un hacha", WillMake)]
    [InlineData("mañana domingo vamos a hacer un hacha", WillMake)]
    [InlineData("luz hice pan", Makes)]
    [InlineData("domingo hacen un hacha", Makes)]
    public void ALoneNameIsNotCheckedWithTheGamesGrammarsEither(string sentence, string grammar)
    {
        Assert.DoesNotContain("conj.person", Described(sentence, grammar), StringComparison.Ordinal);
    }

    // Before a verb of the first or second person, names joined by y may be the people spoken to (Luz y Domingo, queremos pan), and a phrase with a determiner the object
    // put first (el pan quiero). The coordinations gave conj.person at a00b2d1.
    [Theory]
    [InlineData("luz y domingo queremos pan")]
    [InlineData("luz y domingo tenéis pan")]
    [InlineData("luz y domingo quiero pan")]
    [InlineData("el pan quiero")]
    [InlineData("el niño tengo")]
    public void AVerbOfTheFirstOrSecondPersonAfterANounPhraseIsNotChecked(string sentence)
    {
        Assert.DoesNotContain("conj.person", Described(sentence), StringComparison.Ordinal);
    }

    // ---- A third-person verb after a phrase that is surely its subject ----------------------------------------------------------------------------------------

    [Theory]
    [InlineData("los niños come pan", "conj.person|come|comen")]
    [InlineData("el niño comen pan", "conj.person|comen|come")]
    [InlineData("los niños no come pan", "conj.person|come|comen")]
    [InlineData("cuando los niños come pan", "conj.person|come|comen")]
    [InlineData("el domingo los niños come pan", "conj.person|come|comen")]
    [InlineData("mis padres tiene una casa", "conj.person|tiene|tienen")]
    [InlineData("dos niños come pan", "conj.person|come|comen")]
    [InlineData("luz y domingo comió pan", "conj.person|comió|comieron")]
    [InlineData("el niño y la niña come pan", "conj.person|come|comen")]
    [InlineData("el pan y la leche está en la mesa", "conj.person|está|están")]
    [InlineData("las manzanas me gusta", "conj.person|gusta|gustan")]
    public void ANounSubjectBeforeAThirdPersonVerbAgreesWithIt(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    // ---- The clean neighbours ---------------------------------------------------------------------------------------------------------------------------

    // Agreement in every tense and number, and a subject of two phrases.
    [Theory]
    [InlineData("mar pescó en el río")]
    [InlineData("luz tiene pan")]
    [InlineData("luz comía pan")]
    [InlineData("domingo no tiene pan")]
    [InlineData("los niños comen pan")]
    [InlineData("luz y domingo comieron pan")]
    [InlineData("el pan y la leche están en la mesa")]
    [InlineData("las manzanas me gustan")]
    [InlineData("el pan me gusta")]
    [InlineData("domingo es el herrero")]
    [InlineData("domingo y luz son amigos")]
    public void ASubjectThatAgreesIsClean(string sentence)
    {
        Assert.Equal(string.Empty, Described(sentence));
    }

    // A noun that is not the subject: an object put first (with an object pronoun that stands for it), a phrase a preposition governs, a phrase of time, a phrase asked
    // about, a person spoken to, and a coordination with o, a pronoun or a clause of its own.
    [Theory]
    [InlineData("el pan quiero")]
    [InlineData("el pan lo compré")]
    [InlineData("las manzanas las compro")]
    [InlineData("en la herrería trabaja domingo")]
    [InlineData("a luz le gusta el pan")]
    [InlineData("la casa de luz es grande")]
    [InlineData("el domingo tengo pan")]
    [InlineData("esta mañana compré pan")]
    [InlineData("todos los días como pan")]
    [InlineData("la semana pasada fui a la casa")]
    [InlineData("mañana tengo pan")]
    [InlineData("qué fruta come luz")]
    [InlineData("cuántas manzanas quieres")]
    [InlineData("cuántas manzanas compra luz")]
    [InlineData("qué frutas come luz")]
    [InlineData("el pan lo compran los niños")]
    [InlineData("a los niños les gusta el pan")]
    [InlineData("domingo qué haces")]
    [InlineData("hola domingo tengo pan")]
    [InlineData("gracias quiero pan")]
    [InlineData("señor quiero pan")]
    [InlineData("los niños o la niña come pan")]
    [InlineData("luz o domingo comió pan")]
    [InlineData("yo y luz comimos pan")]
    [InlineData("como pan y luz bebe agua")]
    public void ANounThatIsNotTheSubjectIsLeftAlone(string sentence)
    {
        Assert.Equal(string.Empty, Described(sentence));
    }

    // haber has no subject, and ser may agree with a plural predicate.
    [Theory]
    [InlineData("hay dos panes")]
    [InlineData("en la casa hay dos panes")]
    [InlineData("mi comida favorita son las manzanas")]
    [InlineData("el problema son los niños")]
    public void HaberAndSerWithAPluralPredicateAreLeftAlone(string sentence)
    {
        Assert.Equal(string.Empty, Described(sentence));
    }

    // M64 (b), the review of feature/village (R5): an amount before a singular ser is read as one sum, and both es and son are right. Before M64 the first four gave
    // conj.person "son" (dos panes es suficiente also noun.number "suficientes"), and so did the next three.
    [Theory]
    [InlineData("diez monedas es mucho dinero")]
    [InlineData("dos panes es suficiente")]
    [InlineData("dos panes es poco")]
    [InlineData("quince monedas es mucho")]
    [InlineData("dos panes no es nada")]
    [InlineData("diez monedas es muy caro")]
    [InlineData("veinte monedas es un buen precio")]
    [InlineData("dos panes fue suficiente")]
    [InlineData("diez monedas era mucho dinero")]
    [InlineData("dos panes son suficientes")]
    [InlineData("diez monedas son mucho dinero")]
    public void AnAmountBeforeASingularSerIsOneSum(string sentence)
    {
        Assert.Equal(string.Empty, Described(sentence, Describe));
    }

    // Still flagged: a phrase with no number, another verb than ser, a predicate of the plural and one that says no amount.
    [Theory]
    [InlineData("las manzanas es roja", Describe, "conj.person|es|son;noun.number|roja|rojas")]
    [InlineData("los niños come pan", Makes, "conj.person|come|comen")]
    [InlineData("dos niños come pan", Makes, "conj.person|come|comen")]
    [InlineData("dos panes es suficientes", Describe, "conj.person|es|son")]
    [InlineData("dos panes es caros", Describe, "conj.person|es|son")]
    [InlineData("dos manzanas es roja", Describe, "conj.person|es|son")]
    [InlineData("las diez monedas es mucho dinero", Describe, "conj.person|es|son")]
    public void AnAmountBeforeAnotherVerbOrPredicateIsStillChecked(string sentence, string grammar, string expected)
    {
        Assert.Equal(expected, Described(sentence, grammar));
    }

    // A name the lexicon lacks (vocab.unknown, nothing else) or that a grammar binds has no reading, so nothing is said about its verb.
    [Fact]
    public void ANameWithoutAReadingHasNothingToAgree()
    {
        Assert.Equal("vocab.unknown|pedro|", Described("pedro tengo pan"));
        Assert.Equal(string.Empty, Described("pedro tengo pan", "[WORD:name] [VERB:tener] [NOUN:item]"));
        Assert.Equal(string.Empty, Described("domingo tengo pan", "[WORD:name] [VERB:tener] [NOUN:item]"));
    }

    // cuesta is also a noun (a slope). A grammar that binds the verb makes it the verb, as the game's prompts do (see the game's sentences above); since M66 so does
    // a phrase with a determiner that opens the utterance (below), which before M66 the charitable reading ran into the noun.
    [Fact]
    public void AVerbThatIsAlsoANounIsCheckedWhereAGrammarBindsTheVerb()
    {
        Assert.Equal("conj.person|cuesta|cuestan", Described("dos panes cuesta tres monedas"));
        Assert.Equal("conj.person|cuesta|cuestan", Described("dos panes cuesta tres monedas", "[NUM:qty]? [NOUN:item] [VERB:costar] [NUM]? [NOUN]"));
    }

    // ---- M66 (4b): a noun that is also a verb after the phrase that opens the utterance ------------------------------------------------------------------------

    // The QA pass (2026-11-01, QA-language-3): las manzanas cuesta una moneda was praised at the farmer's prompt. cuesta is also a noun, and the charitable reading
    // ran the phrase into it, so the person check saw no verb. A noun phrase does not run into a bare noun: a word right after its noun that is a noun and a verb of
    // the third person (and no adjective) is the verb. Each gave nothing before M66, with and without the game's price grammar.
    [Theory]
    [InlineData("las manzanas cuesta una moneda", "conj.person|cuesta|cuestan")]
    [InlineData("los panes cuesta dos monedas", "conj.person|cuesta|cuestan")]
    [InlineData("las manzanas rojas cuesta una moneda", "conj.person|cuesta|cuestan")]
    [InlineData("las manzanas vale una moneda", "conj.person|vale|valen")]
    public void APluralNounBeforeASingularVerbOfPriceIsFlagged(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
        Assert.Equal(expected, Described(sentence, "[ART]? [NUM:qty]? [NOUN:item] [ADJ]? [VERB:costar] [ART]? [NUM]? [NOUN]"));
    }

    [Theory]
    [InlineData("las manzanas es roja", "conj.person|es|son")]
    [InlineData("un pan cuestan dos monedas", "conj.person|cuestan|cuesta")]
    public void TheNeighboursThatWereFlaggedBeforeStillAre(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    // cada manzana gave word_order "manzana cada" before M66 (cada, an adjective that goes before its noun, was not listed as one); the others were clean before.
    [Theory]
    [InlineData("las manzanas cuestan una moneda")]
    [InlineData("la manzana cuesta una moneda")]
    [InlineData("cada manzana cuesta una moneda")]
    [InlineData("dos manzanas cuestan dos monedas")]
    [InlineData("la cuesta es larga")]
    [InlineData("el pan vino y agua")]
    [InlineData("cada dos días como pan")]
    public void ASubjectThatAgreesWithItsVerbOfPriceIsClean(string sentence)
    {
        Assert.Equal(string.Empty, Described(sentence));
    }
}
