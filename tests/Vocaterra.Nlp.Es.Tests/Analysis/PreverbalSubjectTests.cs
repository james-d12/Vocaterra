namespace Vocaterra.Nlp.Es.Tests.Analysis;

using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Packs;
using Vocaterra.Nlp.Language.SlotGrammar;

/// <summary>
/// M59 (a), found by the game's V12 and V13: <c>conj.person</c> looked only after a subject pronoun, so a name or a noun before its verb was not checked
/// (<c>Domingo trabajo en la herrería</c>, <c>las manzanas es roja</c> gave nothing). A noun phrase that opens its clause is now the subject of the verb after it.
/// Each mistake gave no error before M59; the clean neighbours are tests as well. Runs on the lexicon of the bundle, with the game's grammars where the bundle's
/// prompts bind the verb (<c>trabajo</c> is also a noun, and without a grammar the charitable reading takes the noun).
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

    [Theory]
    [InlineData("domingo trabajo en la herrería", Works, "conj.person|trabajo|trabaja")]
    [InlineData("hoy domingo trabajo en la herrería", Works, "conj.person|trabajo|trabaja")]
    [InlineData("sí domingo trabajo en la herrería", Works, "conj.person|trabajo|trabaja")]
    [InlineData("domingo voy a hacer un hacha", WillMake, "conj.person|voy|va")]
    [InlineData("mañana domingo vamos a hacer un hacha", WillMake, "conj.person|vamos|va")]
    [InlineData("luz hice pan", Makes, "conj.person|hice|hizo")]
    [InlineData("domingo hacen un hacha", Makes, "conj.person|hacen|hace")]
    [InlineData("las manzanas es roja", Describe, "conj.person|es|son")]
    [InlineData("la manzana son roja", Describe, "conj.person|son|es")]
    public void TheGamesNameOrNounBeforeItsVerbIsChecked(string sentence, string grammar, string expected)
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

    // ---- Every tense, every number, and a subject of two phrases ------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("mar pescaste en el río", "conj.person|pescaste|pescó")]
    [InlineData("luz tengo pan", "conj.person|tengo|tiene")]
    [InlineData("domingo no tengo pan", "conj.person|tengo|tiene")]
    [InlineData("luz comía pan", "")]
    [InlineData("los niños come pan", "conj.person|come|comen")]
    [InlineData("el niño comen pan", "conj.person|comen|come")]
    [InlineData("mis padres tiene una casa", "conj.person|tiene|tienen")]
    [InlineData("dos niños come pan", "conj.person|come|comen")]
    [InlineData("luz y domingo comió pan", "conj.person|comió|comieron")]
    [InlineData("el niño y la niña come pan", "conj.person|come|comen")]
    [InlineData("el pan y la leche está en la mesa", "conj.person|está|están")]
    [InlineData("las manzanas me gusta", "conj.person|gusta|gustan")]
    [InlineData("cuando domingo tengo pan", "conj.person|tengo|tiene")]
    [InlineData("el domingo luz tengo pan", "conj.person|tengo|tiene")]
    public void ANounSubjectBeforeItsVerbAgreesWithIt(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    // ---- The clean neighbours ---------------------------------------------------------------------------------------------------------------------------

    // Agreement in every tense and number, and a subject of two phrases.
    [Theory]
    [InlineData("mar pescó en el río")]
    [InlineData("luz tiene pan")]
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

    // A noun that is not the subject: an object put first (with a determiner and a verb of the first or second person, or with an object pronoun that stands for it),
    // a phrase a preposition governs, a phrase of time, a phrase asked about, a person spoken to, a noun said as an interjection, and a coordination with o, a pronoun or
    // a clause of its own.
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
    [InlineData("domingo qué haces")]
    [InlineData("hola domingo tengo pan")]
    [InlineData("gracias quiero pan")]
    [InlineData("señor quiero pan")]
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

    // A name the lexicon lacks (vocab.unknown, nothing else) or that a grammar binds has no reading, so nothing is said about its verb.
    [Fact]
    public void ANameWithoutAReadingHasNothingToAgree()
    {
        Assert.Equal("vocab.unknown|pedro|", Described("pedro tengo pan"));
        Assert.Equal(string.Empty, Described("pedro tengo pan", "[WORD:name] [VERB:tener] [NOUN:item]"));
        Assert.Equal(string.Empty, Described("domingo tengo pan", "[WORD:name] [VERB:tener] [NOUN:item]"));
    }

    // cuesta is also a noun (a slope), and with no grammar to bind the verb the charitable reading takes the noun rather than report a mistake; the game's prompts
    // bind their verbs (see the game's sentences above).
    [Fact]
    public void AVerbThatIsAlsoANounIsCheckedWhereAGrammarBindsTheVerb()
    {
        Assert.Equal(string.Empty, Described("dos panes cuesta tres monedas"));
        Assert.Equal("conj.person|cuesta|cuestan", Described("dos panes cuesta tres monedas", "[NUM:qty]? [NOUN:item] [VERB:costar] [NUM]? [NOUN]"));
    }
}
