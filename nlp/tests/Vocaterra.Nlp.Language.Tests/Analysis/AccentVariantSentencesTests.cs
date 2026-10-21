namespace Vocaterra.Nlp.Language.Tests.Analysis;

using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Morphology;
using Vocaterra.Nlp.Language.SlotGrammar;

/// <summary>
/// Sentences written without accents, on the lexicon of the bundle (M26b, CR-013). A word spelled without an accent that is itself a lexicon form (<c>esta</c>, <c>el</c>,
/// <c>tu</c>, <c>se</c>) used to hide the accented word it may stand for (<c>está</c>, <c>él</c>, <c>tú</c>, <c>sé</c>), so <c>dónde esta el arbol</c> read <c>esta</c> as the
/// demonstrative and reported a false <c>gender.agree</c>. The resolver now reads the word as typed unless the neighbouring words contradict that reading.
/// </summary>
public class AccentVariantSentencesTests
{
    private static readonly Lazy<Lexicon> Words = new(BuildLexicon);
    private static readonly Lazy<MorphologyGenerator> Morphology = new(static () => new MorphologyGenerator(Words.Value));
    private static readonly Lazy<LinguisticAnalyzer> Analyzer = new(BuildAnalyzer);

    private static LinguisticAnalyzer BuildAnalyzer()
    {
        return new LinguisticAnalyzer(Words.Value, Morphology.Value);
    }

    private static Lexicon BuildLexicon()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "content", "lexicon");
        var entries = new List<LexiconEntry>();
        foreach (var path in Directory.GetFiles(folder, "*.json").Order(StringComparer.Ordinal))
        {
            entries.AddRange(Lexicon.ReadFile(File.ReadAllBytes(path)).Entries);
        }

        return Lexicon.FromEntries(entries);
    }

    private static IReadOnlyList<ErrorTag> Errors(string sentence, params string[] grammarPatterns)
    {
        var grammars = grammarPatterns.Select(pattern => SlotGrammar.Parse(pattern)).ToList();
        return Analyzer.Value.Analyze(TestTokens.From(sentence), grammars).Errors;
    }

    private static string Describe(IReadOnlyList<ErrorTag> errors)
    {
        return errors.Count == 0 ? "no errors" : string.Join("; ", errors.Select(static e => $"{e.Tag} '{e.Actual}' -> '{e.Expected}'"));
    }

    private static void AssertNoErrors(string sentence)
    {
        var errors = Errors(sentence);
        Assert.True(errors.Count == 0, $"'{sentence}' gave {Describe(errors)}");
    }

    private static void AssertTags(string sentence, params string[] tags)
    {
        var errors = Errors(sentence);
        Assert.True(errors.Select(static e => e.Tag).Order(StringComparer.Ordinal).SequenceEqual(tags.Order(StringComparer.Ordinal)), $"'{sentence}' gave {Describe(errors)}");
    }

    // ---- esta and está ---------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("dónde esta el arbol")]
    [InlineData("donde esta el arbol")]
    [InlineData("dónde está el árbol")]
    [InlineData("donde esta la espada")]
    [InlineData("como esta el herrero")]
    [InlineData("aqui esta el herrero")]
    [InlineData("la espada esta en la mesa")]
    [InlineData("el arbol esta aqui")]
    public void EstaBeforeAnArticleOrWithoutANounIsTheVerb(string sentence)
    {
        AssertNoErrors(sentence);
    }

    [Fact]
    public void EstaAfterAQuestionWordIsTheVerbEvenBeforeANoun()
    {
        // The question word asks for a verb, so esta is not the demonstrative (which would need the noun to be feminine).
        AssertNoErrors("donde esta arbol");
        AssertNoErrors("cómo esta casa");
    }

    [Fact]
    public void EstaBeforeANounIsStillTheDemonstrative()
    {
        AssertNoErrors("esta espada es buena");
        AssertNoErrors("quiero comprar esta espada");
        AssertNoErrors("esta casa es grande");
        AssertNoErrors("cuanto cuesta esta espada");
    }

    [Fact]
    public void ADemonstrativeStillAgreesWithItsNoun()
    {
        // The unaccented reading wins here: a demonstrative before a noun fits, so nothing turns esta into está and the gender mistake is found.
        AssertTags("esta libro es bueno", ErrorTags.GenderAgree);
        AssertTags("este espada es buena", ErrorTags.GenderAgree);
    }

    [Fact]
    public void EstaAsTheVerbIsCheckedAsTheVerb()
    {
        // ella está alto is a ser/estar mistake: the copula check runs on a word that used to be read as the demonstrative.
        AssertTags("ella esta alto", ErrorTags.SerEstar);
        AssertTags("el esta simpatico", ErrorTags.SerEstar);
        AssertTags("estas inteligente", ErrorTags.SerEstar);
    }

    [Fact]
    public void TheResolverReadsEstaAsEstarThirdPersonSingularPresent()
    {
        var resolved = Resolve("donde esta el arbol");

        var reading = Assert.Single(resolved[1].Readings);
        Assert.Equal("estar", reading.Lemma);
        Assert.Equal("verb", reading.Pos);
        Assert.Equal("3", reading.Person);
        Assert.Equal("sg", reading.Number);
        Assert.Equal("pres", reading.Tense);
        Assert.Equal("ind", reading.Mood);
    }

    [Fact]
    public void TheResolverReadsEstaAsTheDemonstrativeBeforeANoun()
    {
        var resolved = Resolve("esta espada es buena");

        var reading = Assert.Single(resolved[0].Readings);
        Assert.Equal("este", reading.Lemma);
        Assert.Equal("adj", reading.Pos);
    }

    [Fact]
    public void TheTokensTheAnalyzerReturnsKeepTheReadingsOfTheSpellingAsTyped()
    {
        var result = Analyzer.Value.Analyze(TestTokens.From("donde esta el arbol"));

        Assert.All(result.Tokens[1].Readings, reading => Assert.Equal("este", reading.Lemma));
    }

    // ---- el and él -------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("el arbol es grande")]
    [InlineData("el herrero tiene una espada")]
    [InlineData("quiero el libro")]
    [InlineData("dame el libro")]
    [InlineData("para el herrero")]
    [InlineData("el es mi amigo")]
    [InlineData("el come pan")]
    [InlineData("para el")]
    public void ElIsTheArticleBeforeANounAndThePronounWhereNothingFollowsThatContinuesANounPhrase(string sentence)
    {
        AssertNoErrors(sentence);
    }

    [Fact]
    public void TheResolverReadsElAsTheArticleBeforeANounAndAsThePronounBeforeAVerb()
    {
        Assert.Equal("art", Assert.Single(Resolve("el arbol es grande")[0].Readings).Pos);
        Assert.Equal("pron", Assert.Single(Resolve("el come pan")[0].Readings).Pos);
        Assert.Equal("pron", Assert.Single(Resolve("para el")[1].Readings).Pos);
    }

    [Fact]
    public void ElBeforeANounStaysTheArticleSoAGenderMistakeIsStillFound()
    {
        AssertTags("el espada es buena", ErrorTags.GenderAgree);
    }

    [Fact]
    public void ElBeforeAVerbIsThePronounSoAPersonMistakeIsFound()
    {
        AssertTags("el tienes", ErrorTags.ConjPerson);
        AssertNoErrors("el tiene una espada");
    }

    // ---- tu and tú -------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TuBeforeANounStaysThePossessive()
    {
        AssertNoErrors("tu casa es grande");
        AssertNoErrors("para tu madre");
        AssertNoErrors("dame tu espada");
        AssertTags("tu espadas son buenas", ErrorTags.NounNumber);
    }

    [Fact]
    public void TuBeforeAVerbIsThePronoun()
    {
        AssertNoErrors("tu eres mi amigo");
        AssertNoErrors("tu tienes una espada");
        AssertTags("tu tengo una espada", ErrorTags.ConjPerson);
        Assert.Equal("pron", Assert.Single(Resolve("tu eres mi amigo")[0].Readings).Pos);
    }

    [Fact]
    public void MiBeforeANounStaysThePossessiveAndMiWithoutOneIsThePronoun()
    {
        AssertNoErrors("para mi");
        AssertNoErrors("mi espada es buena");
        AssertTags("mi espadas son buenas", ErrorTags.NounNumber);
        Assert.Equal("pron", Assert.Single(Resolve("para mi")[1].Readings).Pos);
        Assert.Equal("adj", Assert.Single(Resolve("mi espada es buena")[0].Readings).Pos);
    }

    // ---- si, se, te, que, mas --------------------------------------------------------------------------------------------------------

    [Fact]
    public void SiBeforeAClauseStaysTheConjunctionAndSiAloneIsTheInterjection()
    {
        AssertNoErrors("si quiero comprar una espada");
        AssertNoErrors("si tengo dinero compro la espada");
        AssertNoErrors("si");
        Assert.Equal("conj", Assert.Single(Resolve("si quiero comprar una espada")[0].Readings).Pos);
        Assert.Equal("interj", Assert.Single(Resolve("si")[0].Readings).Pos);
    }

    [Fact]
    public void SeBeforeAVerbStaysThePronounAndSeWithoutOneCanBeTheVerb()
    {
        Assert.Empty(Errors("se llama ana", "se llama [WORD:name]"));
        AssertNoErrors("se lo doy");
        AssertNoErrors("no se");
        AssertNoErrors("no se si tiene espadas");
        AssertNoErrors("yo no se nada");
        Assert.Equal("pron", Assert.Single(Resolve("se lo doy")[0].Readings).Pos);
        Assert.All(Resolve("no se")[1].Readings, reading => Assert.NotEqual("pron", reading.Pos));
    }

    [Fact]
    public void TeBeforeAVerbStaysTheClitic()
    {
        AssertNoErrors("te quiero");
        AssertNoErrors("te lo doy");
        AssertNoErrors("quiero un te");
        Assert.Equal("pron", Assert.Single(Resolve("te lo doy")[0].Readings).Pos);
        Assert.Equal("noun", Assert.Single(Resolve("quiero un te")[2].Readings).Pos);
    }

    [Fact]
    public void QueAndMasKeepTheirReadingsAsTyped()
    {
        AssertNoErrors("quiero que vengas");
        AssertNoErrors("que quieres");
        AssertNoErrors("quiero mas espadas");
        AssertNoErrors("tengo mas dinero");
        AssertNoErrors("quiero dos mas");
        Assert.All(Resolve("quiero mas espadas")[1].Readings, reading => Assert.Contains(reading.Pos, new[] { "conj", "noun" }));
    }

    [Fact]
    public void QueAtTheEndIsTheQuestionWord()
    {
        Assert.Equal("pron", Assert.Single(Resolve("que")[0].Readings).Pos);
    }

    // ---- como and cómo ---------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("tu como te llamas")]
    [InlineData("y tu como estas")]
    [InlineData("como te llamas")]
    [InlineData("como estas")]
    [InlineData("soy el herrero y tu como te llamas")]
    [InlineData("yo como pan")]
    [InlineData("como pan")]
    [InlineData("como manzanas todos los dias")]
    public void ComoIsTheQuestionWordBeforeAClauseAndTheVerbBeforeAnObject(string sentence)
    {
        AssertNoErrors(sentence);
    }

    [Fact]
    public void TheResolverReadsComoByWhatFollowsIt()
    {
        Assert.Equal("adv", Assert.Single(Resolve("como te llamas")[0].Readings).Pos);
        Assert.Equal("adv", Assert.Single(Resolve("tu como te llamas")[1].Readings).Pos);
        Assert.Equal("adv", Assert.Single(Resolve("como estas")[0].Readings).Pos);
        Assert.Equal("verb", Assert.Single(Resolve("yo como pan")[1].Readings).Pos);
        Assert.Equal("verb", Assert.Single(Resolve("como pan")[0].Readings).Pos);
        Assert.Equal("verb", Assert.Single(Resolve("como manzanas todos los dias")[0].Readings).Pos);
    }

    [Fact]
    public void PersonMistakesNextToComoAreStillFound()
    {
        AssertTags("tu tengo una espada", ErrorTags.ConjPerson);
        AssertTags("tu como pan", ErrorTags.ConjPerson);
    }

    // ---- mistakes stay mistakes ------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheReadmeSentencesKeepTheirErrors()
    {
        AssertTags("yo tiene dos espada", ErrorTags.ConjPerson, ErrorTags.NounNumber);
        AssertNoErrors("ayer compré un libro");
        Assert.Empty(Errors("me llamo ana", "me llamo [WORD:name]"));
        Assert.Empty(Errors("soy ana", "soy [WORD:name]"));
    }

    [Fact]
    public void AccentedSpellingsAreNeverReadAsTheUnaccentedWord()
    {
        // The writer chose the accent: está is the verb whatever follows it, and él the pronoun.
        AssertNoErrors("él tiene una espada");
        AssertNoErrors("tú tienes una espada");
        AssertTags("tú tengo una espada", ErrorTags.ConjPerson);
        AssertNoErrors("la espada está en la mesa");
    }

    [Fact]
    public void ASlotGrammarThatMatchedTheWordAsTypedKeepsThatReading()
    {
        // The grammar binds el as the article, so the pronoun reading is not considered although nothing follows the article that continues a noun phrase.
        var result = Analyzer.Value.Analyze(TestTokens.From("el"), [SlotGrammar.Parse("[ART]")]);

        Assert.Empty(result.Errors);
        Assert.Equal("[ART]", result.MatchedGrammarId);
    }

    [Fact]
    public void TheAnalysisIsTheSameOnAFreshAnalyzer()
    {
        const string sentence = "donde esta el arbol grandes";
        var fresh = new LinguisticAnalyzer(BuildLexicon()).Analyze(TestTokens.From(sentence));

        Assert.Equal(Describe(Analyzer.Value.Analyze(TestTokens.From(sentence)).Errors), Describe(fresh.Errors));
    }

    // The resolver's output for a sentence, as the analyzer calls it: the readings as typed, the readings of the accented spellings, and no slot grammar.
    private static IReadOnlyList<AnalyzedToken> Resolve(string sentence)
    {
        var tokens = Analyzer.Value.Analyze(TestTokens.From(sentence)).Tokens;
        var variants = tokens
            .Select(static t => (IReadOnlyList<LexicalFeatures>)[.. Words.Value.LookupWithAccentVariants(t.Token.Text).Variants.Select(static v => v.Reading)])
            .ToList();
        return ReadingResolver.Resolve(tokens, variants, null, CountErrors);
    }

    private static int CountErrors(IReadOnlyList<AnalyzedToken> tokens)
    {
        var found = new List<ErrorTag>();
        new AgreementChecker(tokens, Morphology.Value, found).Run();
        return found.Distinct().Count();
    }
}
