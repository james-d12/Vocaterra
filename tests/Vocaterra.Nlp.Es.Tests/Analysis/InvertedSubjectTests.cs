namespace Vocaterra.Nlp.Es.Tests.Analysis;

using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Packs;
using Vocaterra.Nlp.Language.SlotGrammar;

/// <summary>
/// M66, the grading gaps of sessions 7 to 10 (handover 9.3 B): a subject pronoun put out of the place the person check looked in. (1) A pronoun right after a finite
/// verb, as a question puts it, was taken as the subject of the infinitive after it, so <c>¿puede usted vender manzanas?</c> gave <c>conj.person</c> "vende" (the
/// bare-infinitive rule of <c>yo comprar</c>); the pronoun is the subject of the verb before it. (8, the filed M63) A pronoun before a question word was lost: the
/// check of a verb after it looked past <c>dónde</c> but not past <c>qué</c> (<c>ella qué quieres</c> gave nothing), and M56's check of <c>estar</c> against the
/// phrase after it started its clause at the question word (<c>ellas adónde están un detenido lento</c> gave <c>conj.person</c> "está"). Each case marked so gave the
/// old result before M66; the clean and still-flagged neighbours are tests as well. Runs on the lexicon of the bundle.
/// </summary>
public class InvertedSubjectTests
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

    // ---- (1) a subject pronoun after its verb ------------------------------------------------------------------------------------------------------------------

    // Session 10's verb (poder) in its register (usted): before M66 each gave conj.person on the infinitive (vende, compra, vienen, paga, viene, comen), and the
    // last on the verb after dónde (estás).
    [Theory]
    [InlineData("puede usted vender manzanas")]
    [InlineData("quiere usted comprar pan")]
    [InlineData("pueden ustedes venir mañana")]
    [InlineData("puede usted pagar con fruta")]
    [InlineData("puede usted venir")]
    [InlineData("quieren ustedes comer pan")]
    [InlineData("sabes tú dónde está la herrería")]
    public void ASubjectPronounAfterItsVerbLeavesTheInfinitiveToThatVerb(string sentence)
    {
        Assert.Equal(string.Empty, Described(sentence));
    }

    // Clean before M66 as well, and still: the infinitive after a, a noun after the pronoun, and a pronoun after a verb of its own person.
    [Theory]
    [InlineData("va usted a comprar fruta")]
    [InlineData("tiene usted pan")]
    [InlineData("quiere usted pan")]
    public void OtherInvertedQuestionsStayClean(string sentence)
    {
        Assert.Equal(string.Empty, Described(sentence));
    }

    // An infinitive after a pronoun that follows a verb of another person belongs to that verb, so it is not checked against the pronoun (¿puedes usted vender?
    // gave vende before M66); a finite verb after a pronoun that follows a word of another person is still checked (mira, yo quiere pan; vale, yo quiere pan).
    [Theory]
    [InlineData("puedes usted vender manzanas", "")]
    [InlineData("mira yo quiere pan", "conj.person|quiere|quiero")]
    [InlineData("vale yo quiere pan", "conj.person|quiere|quiero")]
    public void APronounAfterAVerbOfAnotherPersonIsCheckedOnlyAgainstAFiniteVerb(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    // With no verb before it the pronoun is the subject of the infinitive, a person error as before.
    [Theory]
    [InlineData("usted vender manzanas", "conj.person|vender|vende")]
    [InlineData("yo comprar pan", "conj.person|comprar|compro")]
    [InlineData("ustedes venir mañana", "conj.person|venir|vienen")]
    public void ABareInfinitiveAfterASubjectPronounIsStillAPersonError(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    // ---- (8) a subject pronoun before a question word (M63) ------------------------------------------------------------------------------------------------------

    // The fuzz seeds of M63: 8zKtIhOY3951 (O5) and c5SlpISRmTZ3 (O3). Both gave conj.person "está" before M66 (the phrase after estar was taken for its subject).
    [Theory]
    [InlineData("ellas adónde están un detenido lento")]
    [InlineData("ellas dónde están teme")]
    public void TheFuzzSeedsOfM63AreClean(string sentence)
    {
        Assert.Equal(string.Empty, Described(sentence));
    }

    [Theory]
    [InlineData("ellos dónde están")]
    [InlineData("ellas adónde van")]
    [InlineData("ella qué quiere")]
    [InlineData("tú qué quieres")]
    [InlineData("ustedes qué quieren comprar")]
    [InlineData("tú cómo te llamas")]
    [InlineData("yo qué sé")]
    [InlineData("ellas dónde están las casas")]
    public void APronounBeforeTheQuestionWordIsTheSubjectAndAgrees(string sentence)
    {
        Assert.Equal(string.Empty, Described(sentence));
    }

    // ella qué quieres gave nothing before M66 (qué stood where the verb was looked for); ellas dónde está was flagged before as well (dónde, an adverb, was skipped).
    [Theory]
    [InlineData("ella qué quieres", "conj.person|quieres|quiere")]
    [InlineData("tú qué quieren", "conj.person|quieren|quieres")]
    [InlineData("nosotros qué quieren", "conj.person|quieren|queremos")]
    [InlineData("ellas dónde está", "conj.person|está|están")]
    [InlineData("tú adónde va", "conj.person|va|vas")]
    public void AVerbAfterTheQuestionWordThatDisagreesWithThePronounIsFlagged(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    // Two readings are left alone: a third person with a subject after it (ellas, ¿dónde está la casa?; flagged "están" before M66), and a third person singular
    // after qué, which may be the subject itself (ellos, ¿qué pasa?).
    [Theory]
    [InlineData("ellas dónde está la casa")]
    [InlineData("ellos qué pasa")]
    [InlineData("tú qué quiere")]
    public void AQuestionWithASubjectOfItsOwnIsNotChecked(string sentence)
    {
        Assert.Equal(string.Empty, Described(sentence));
    }
}
