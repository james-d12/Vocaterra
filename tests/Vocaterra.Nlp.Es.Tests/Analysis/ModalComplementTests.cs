namespace Vocaterra.Nlp.Es.Tests.Analysis;

using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Packs;
using Vocaterra.Nlp.Language.SlotGrammar;

/// <summary>
/// M66 (3), the grading gaps of sessions 7 to 10 (handover 9.3 B): a finite verb after a modal was not flagged. After <c>poder</c>, <c>querer</c>, <c>deber</c>,
/// <c>necesitar</c> and <c>tener que</c> a finite form of a verb is the learner's slip for the infinitive, which <c>conj.tense</c> now expects, as it does after
/// <c>ir a</c> (<c>voy a como</c> gives <c>comer</c>), so the learner meets one kind of message. Which word is the verb the reading resolver decides: a noun that the
/// modal may take stays the noun (<c>quiero vino</c>), and a word that is also a conjunction is the verb only before a phrase opened by a determiner or <c>más</c>.
/// Each flagged line gave nothing before M66; the clean neighbours are tests as well. Runs on the lexicon of the bundle.
/// </summary>
public class ModalComplementTests
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

        return new LinguisticAnalyzer(SpanishLexicon.FromEntries(entries), SpanishLanguagePack.Instance);
    }

    private static string Described(string sentence, params string[] grammars)
    {
        var parsed = grammars.Select(static g => SlotGrammar.Parse(g)).ToList();
        return string.Join(';', Analyzer.Value.Analyze(TestTokens.From(sentence), parsed).Errors.Select(static e => $"{e.Tag}|{e.Actual}|{e.Expected}"));
    }

    // The brief's lines. pago is also a noun, which poder cannot take (no noun object); vendo is also a noun (the rare vendo), which cannot run into the bare noun
    // madera; como is also a conjunction, which cannot open más fruta. compro, voy and va are only verbs.
    [Theory]
    [InlineData("puedo pago con fruta", "conj.tense|pago|pagar")]
    [InlineData("quiero vendo madera", "conj.tense|vendo|vender")]
    [InlineData("tengo que voy al río", "conj.tense|voy|ir")]
    [InlineData("tengo que va al río", "conj.tense|va|ir")]
    [InlineData("necesito compro pan", "conj.tense|compro|comprar")]
    [InlineData("debo como más fruta", "conj.tense|como|comer")]
    public void AFiniteVerbAfterAModalExpectsTheInfinitive(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    // Other persons and tenses of the modal, and of the verb after it.
    [Theory]
    [InlineData("puede paga con fruta", "conj.tense|paga|pagar")]
    [InlineData("podemos compramos pan", "conj.tense|compramos|comprar")]
    [InlineData("quería compro pan", "conj.tense|compro|comprar")]
    [InlineData("tienes que vas al río", "conj.tense|vas|ir")]
    [InlineData("yo quiero vendo madera", "conj.tense|vendo|vender")]
    [InlineData("debo como un pan", "conj.tense|como|comer")]
    [InlineData("ayer quería compro pan", "conj.tense|compro|comprar")]
    public void OtherFormsOfTheModalAndTheVerbAreFlaggedTheSameWay(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    // The game's grammars bind the verb after the modal (the sell and the obligation rows): the slip is flagged under them as well.
    [Theory]
    [InlineData("puedo pago con fruta", "[VERB:poder] [VERB:pagar] con [NOUN:item]", "conj.tense|pago|pagar")]
    [InlineData("tengo que voy al río", "[VERB:tener] que [VERB:ir] al [NOUN:place]", "conj.tense|voy|ir")]
    [InlineData("quiero vendo madera", "[VERB:querer] [VERB:vender] [NOUN:item]", "conj.tense|vendo|vender")]
    public void TheSlipIsFlaggedUnderAGrammarThatBindsTheVerb(string sentence, string grammar, string expected)
    {
        Assert.Equal(expected, Described(sentence, grammar));
    }

    // The infinitive, and a noun that querer, necesitar or deber may take: vino (wine, and venir), pago (a payment, and pagar), with nothing or an adjective or a phrase
    // of time after it; pan, panes and dinero are only nouns. A clause after que is no complement of this kind.
    [Theory]
    [InlineData("puedo pagar con fruta")]
    [InlineData("quiero vender madera")]
    [InlineData("tengo que ir al río")]
    [InlineData("necesito comprar pan")]
    [InlineData("debo comer más fruta")]
    [InlineData("quiero vino")]
    [InlineData("necesito pago")]
    [InlineData("quiero pan")]
    [InlineData("quiero dos panes")]
    [InlineData("debo dinero")]
    [InlineData("quiero que vengas")]
    [InlineData("quiero vino blanco")]
    [InlineData("quiero vino mañana")]
    [InlineData("quiero vino el domingo")]
    [InlineData("necesito pago hoy")]
    public void TheInfinitiveAndANounTheModalMayTakeStayClean(string sentence)
    {
        Assert.Equal(string.Empty, Described(sentence));
    }

    // como stays the conjunction where it says how much or like what: before a word that is no phrase with a determiner or más, after an object pronoun, before a
    // phrase of time.
    [Theory]
    [InlineData("necesito como mínimo dos panes")]
    [InlineData("puedo como mucho dos")]
    [InlineData("quiero como postre fruta")]
    [InlineData("te quiero como a un hermano")]
    [InlineData("lo necesito como el aire")]
    [InlineData("puedo como tú")]
    public void ComoThatSaysHowMuchOrLikeWhatStaysTheConjunction(string sentence)
    {
        Assert.Equal(string.Empty, Described(sentence));
    }

    // A verb that opens a clause of its own, which a comma would set off in writing: the modal again, ser, estar, haber and tener, and ir before a.
    [Theory]
    [InlineData("quiero quiero pan")]
    [InlineData("no puedo estoy cansado")]
    [InlineData("no puedo es tarde")]
    [InlineData("no quiero tengo pan")]
    [InlineData("no puedo voy a dormir")]
    [InlineData("no puedo hay mucho trabajo")]
    public void AVerbThatOpensAClauseOfItsOwnIsNotTheModalsComplement(string sentence)
    {
        Assert.Equal(string.Empty, Described(sentence));
    }

    // Found by M66's own run of O3 at five times the default depth (CsCheck seed c5E0ikVTT_s3): podamos is poder's subjunctive and podar's indicative, and podar takes a
    // noun (los podamos: we prune them), so a modal is a word whose readings of the indicative are all the modal's, and the noun after podamos stays the noun.
    // podemos is poder's indicative (and podar's subjunctive), the modal.
    [Theory]
    [InlineData("los realmente podamos estudio unas", "")]
    [InlineData("podamos estudio", "")]
    [InlineData("podemos estudio", "conj.tense|estudio|estudiar")]
    public void AFormThatIsAlsoAnotherVerbsIsNoModal(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }
}
