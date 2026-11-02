namespace Vocaterra.Nlp.Es.Tests.Analysis;

using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.SlotGrammar;

/// <summary>
/// M69, the price question with an unaccented or inflected question word (V20; the QA pass of 2026-11-01, QA-language-10): <c>cuanto son todo</c> was understood but
/// <c>son</c> was read as the noun, so it was clean; <c>cuántos cuestan todo</c> did not check the question word, which before a verb of price or <c>ser</c> with a
/// neuter subject is the invariable <c>cuánto</c>; and <c>¿cuántos son?</c> gave <c>noun.number</c> <c>sones</c>, a false alarm. Both are graded now as M66 grades
/// <c>¿cuánto son todo?</c> and <c>¿cuánto cuestan todo?</c>. Runs on the lexicon of the bundle.
/// </summary>
public class QuantityQuestionTests
{
    // The game's ask_total grammars (V20).
    private const string TotalGrammar = "cuánto es todo (esto)?";
    private const string TotalPriceGrammar = "cuánto [VERB:costar] todo (esto)?";

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

    // The question word typed without its accent at the start of the utterance, before a verb or a noun, is the question word (cuánto), as with the accent.
    [Theory]
    [InlineData("cuanto son todo", "conj.person|son|es")]
    [InlineData("cuanto cuestan todo", "conj.person|cuestan|cuesta")]
    [InlineData("cuanto cuestan el pan", "conj.person|cuestan|cuesta")]
    [InlineData("cuanto costa el pan", "conj.irregular|costa|cuesta")]
    public void TheQuestionWordWithoutItsAccentIsTheQuestionWord(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    // Before a verb of price or ser whose subject is a neuter pronoun, the question word is the invariable cuánto, and a plural verb disagrees with the pronoun.
    [Theory]
    [InlineData("cuántos cuestan todo", "noun.number|cuántos|cuánto;conj.person|cuestan|cuesta")]
    [InlineData("cuántas cuestan todo", "gender.agree|cuántas|cuánto;noun.number|cuántas|cuánto;conj.person|cuestan|cuesta")]
    [InlineData("cuántos son todo", "noun.number|cuántos|cuánto;conj.person|son|es")]
    [InlineData("cuantos cuestan todo", "noun.number|cuantos|cuánto;conj.person|cuestan|cuesta")]
    [InlineData("cuántos cuestan eso", "noun.number|cuántos|cuánto;conj.person|cuestan|cuesta")]
    [InlineData("cuánta cuestan todo", "gender.agree|cuánta|cuánto;conj.person|cuestan|cuesta")]
    public void AnInflectedQuestionWordBeforeAPluralVerbAndANeuterSubjectIsCuanto(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    // M73 (M69's left point): cuántas cuesta todo read cuesta as the noun of the phrase cuántas opens (noun.number cuestas). Right after the question word and before a
    // neuter pronoun (or, where its noun cannot agree with the question word, before a phrase a determiner opens) a word that is a noun and a finite verb is the verb, and
    // before a verb of price whose neuter subject it agrees with the question word is the invariable cuánto, as before a verb that disagrees.
    [Theory]
    [InlineData("cuántas cuesta todo", "gender.agree|cuántas|cuánto;noun.number|cuántas|cuánto")]
    [InlineData("cuántos cuesta todo", "noun.number|cuántos|cuánto")]
    [InlineData("cuánta cuesta todo", "gender.agree|cuánta|cuánto")]
    [InlineData("cuántas cuesta eso", "gender.agree|cuántas|cuánto;noun.number|cuántas|cuánto")]
    [InlineData("cuántas cuesta todo esto", "gender.agree|cuántas|cuánto;noun.number|cuántas|cuánto")]
    [InlineData("cuantas cuesta todo", "gender.agree|cuantas|cuánto;noun.number|cuantas|cuánto")]
    [InlineData("cuántos vale todo", "noun.number|cuántos|cuánto")]
    public void AnInflectedQuestionWordBeforeAVerbOfPriceThatAgreesWithItsNeuterSubjectIsCuanto(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    // M73: the verb, not the noun cuestas, where the noun cannot agree with the question word before a phrase a determiner opens; its subject is that phrase, so the
    // question word is left alone (as M69 leaves it in cuántos cuestan el pan) and only a verb that disagrees with the phrase is reported.
    [Theory]
    [InlineData("cuántas cuesta el pan", "")]
    [InlineData("cuántos cuesta el pan", "")]
    [InlineData("cuántas cuesta los panes", "conj.person|cuesta|cuestan")]
    public void AFormOfCostarThatIsAlsoANounAfterAnInflectedQuestionWordIsTheVerb(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    [Theory]
    [InlineData("cuántas cuesta todo", TotalPriceGrammar, "gender.agree|cuántas|cuánto;noun.number|cuántas|cuánto")]
    [InlineData("cuántos cuestan todo", TotalPriceGrammar, "noun.number|cuántos|cuánto;conj.person|cuestan|cuesta")]
    [InlineData("cuanto es todo", TotalGrammar, "")]
    [InlineData("cuánto cuesta todo", TotalPriceGrammar, "")]
    public void TheSameUnderTheGamesGrammars(string sentence, string grammar, string expected)
    {
        Assert.Equal(expected, Described(sentence, grammar));
    }

    // Clean: the price question as it should be, with or without the accent; how many with a noun; how many are there (cuántos son: before M69 noun.number sones); a
    // singular verb after cuántos or cuántas, which may leave the coins unsaid (¿cuántos es todo?: how many coins); and cuanto as a relative or before más. Left as it
    // was: a form of costar that is also a noun right after cuántas or cuántos (cuántas cuesta todo) is read as the noun of the phrase the determiner opens
    // (noun.number cuestas), as before M69; M73 reads it as the verb (above). Clean since M73 too: the noun after the question word where the noun agrees with it and no
    // neuter pronoun follows (cuántas cuestas hay, cuántas cuestas tiene el camino), the price said with its noun (cuántas monedas cuesta todo), and the slope itself.
    [Theory]
    [InlineData("cuánto cuesta todo")]
    [InlineData("cuánto es todo")]
    [InlineData("cuanto cuesta el pan")]
    [InlineData("cuanto es todo")]
    [InlineData("cuanto cuesta")]
    [InlineData("cuánto cuestan las manzanas")]
    [InlineData("cuántas manzanas quieres")]
    [InlineData("cuántos panes tienes")]
    [InlineData("cuántos son")]
    [InlineData("cuántas son las manzanas")]
    [InlineData("cuántos es todo")]
    [InlineData("cuántos cuestan dos monedas")]
    [InlineData("cuanto más mejor")]
    [InlineData("cuanto antes")]
    [InlineData("todo cuanto tengo")]
    [InlineData("cuántas cuestas hay")]
    [InlineData("cuántas cuestas tiene el camino")]
    [InlineData("cuántas monedas cuesta todo")]
    [InlineData("cuántas monedas cuesta el pan")]
    [InlineData("la cuesta es grande")]
    [InlineData("la cuesta es muy larga")]
    [InlineData("cuánto cuesta eso")]
    [InlineData("cuánto vale todo")]
    public void ThePriceAndQuantityQuestionsThatStayClean(string sentence)
    {
        Assert.Equal(string.Empty, Described(sentence));
    }
}
