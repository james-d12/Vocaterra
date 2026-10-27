namespace Vocaterra.Nlp.Language.Tests.Analysis;

using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Morphology;
using Vocaterra.Nlp.Language.Packs;
using Vocaterra.Nlp.Language.SlotGrammar;

/// <summary>
/// The rules of <see cref="ReadingResolver"/> one at a time, on a small lexicon in which the words that matter are homographs: <c>libro</c> (noun, and a form of
/// <c>librar</c>), <c>lanza</c> (noun, and a form of <c>lanzar</c>), <c>canto</c>, <c>paseo</c>, <c>temprano</c> and <c>mucho</c> (adjective and adverb), <c>solo</c>.
/// </summary>
public class ReadingResolverTests
{
    private static LexiconEntry Entry(string form, string lemma, string pos, string? person = null, string? number = null, string? tense = null, string? mood = null, string? gender = null)
    {
        return new LexiconEntry(form, lemma, pos, new EntryFeatures(person, number, tense, mood, gender));
    }

    private static readonly Lexicon TestLexicon = Lexicon.FromEntries(
    [
        // Articles, a numeral, pronouns and the closed classes.
        Entry("el", "el", "art", number: "sg", gender: "m"),
        Entry("la", "el", "art", number: "sg", gender: "f"),
        Entry("un", "un", "art", number: "sg", gender: "m"),
        Entry("una", "un", "art", number: "sg", gender: "f"),
        Entry("dos", "2", "num", number: "pl"),
        Entry("yo", "yo", "pron", "1", "sg"),
        Entry("tú", "tú", "pron", "2", "sg"),
        Entry("me", "me", "pron", "1", "sg"),
        Entry("a", "a", "prep"),
        Entry("de", "de", "prep"),
        Entry("y", "y", "conj"),
        Entry("ayer", "ayer", "adv"),
        Entry("siempre", "siempre", "adv"),

        // Verbs: the copula, and the verbs that are also nouns.
        Entry("ser", "ser", "verb"),
        Entry("es", "ser", "verb", "3", "sg", "pres", "ind"),
        Entry("soy", "ser", "verb", "1", "sg", "pres", "ind"),
        Entry("estoy", "estar", "verb", "1", "sg", "pres", "ind"),
        Entry("estar", "estar", "verb"),
        Entry("comprar", "comprar", "verb"),
        Entry("compro", "comprar", "verb", "1", "sg", "pres", "ind"),
        Entry("compré", "comprar", "verb", "1", "sg", "pret", "ind"),
        Entry("librar", "librar", "verb"),
        Entry("libro", "librar", "verb", "1", "sg", "pres", "ind"),
        Entry("libras", "librar", "verb", "2", "sg", "pres", "ind"),
        Entry("libré", "librar", "verb", "1", "sg", "pret", "ind"),
        Entry("lanzar", "lanzar", "verb"),
        Entry("lanzo", "lanzar", "verb", "1", "sg", "pres", "ind"),
        Entry("lanzas", "lanzar", "verb", "2", "sg", "pres", "ind"),
        Entry("lanza", "lanzar", "verb", "3", "sg", "pres", "ind"),
        Entry("lanzó", "lanzar", "verb", "3", "sg", "pret", "ind"),
        Entry("cantar", "cantar", "verb"),
        Entry("canto", "cantar", "verb", "1", "sg", "pres", "ind"),
        Entry("canté", "cantar", "verb", "1", "sg", "pret", "ind"),
        Entry("pasear", "pasear", "verb"),
        Entry("paseo", "pasear", "verb", "1", "sg", "pres", "ind"),
        Entry("paseé", "pasear", "verb", "1", "sg", "pret", "ind"),
        Entry("quiero", "querer", "verb", "1", "sg", "pres", "ind"),
        Entry("querer", "querer", "verb"),

        // Nouns that are also verb forms, and one that is not.
        Entry("libro", "libro", "noun", number: "sg", gender: "m"),
        Entry("libros", "libro", "noun", number: "pl", gender: "m"),
        Entry("lanza", "lanza", "noun", number: "sg", gender: "f"),
        Entry("lanzas", "lanza", "noun", number: "pl", gender: "f"),
        Entry("canto", "canto", "noun", number: "sg", gender: "m"),
        Entry("paseo", "paseo", "noun", number: "sg", gender: "m"),
        Entry("espada", "espada", "noun", number: "sg", gender: "f"),
        Entry("pasillo", "pasillo", "noun", number: "sg", gender: "m"),

        // Adjectives, and adjectives that are also adverbs or interjections.
        Entry("grande", "grande", "adj", number: "sg"),
        Entry("alta", "alto", "adj", number: "sg", gender: "f"),
        Entry("correcto", "correcto", "adj", number: "sg", gender: "m"),
        Entry("temprano", "temprano", "adj", number: "sg", gender: "m"),
        Entry("temprano", "temprano", "adv"),
        Entry("mucho", "mucho", "adj", number: "sg", gender: "m"),
        Entry("mucho", "mucho", "adv"),
        Entry("mucha", "mucho", "adj", number: "sg", gender: "f"),
        Entry("solo", "solo", "adj", number: "sg", gender: "m"),
        Entry("solo", "solo", "adv"),
        Entry("su", "su", "adj", number: "sg"),
        Entry("sus", "su", "adj", number: "pl"),
        Entry("sus", "sus", "adj", number: "sg", gender: "m"),
        Entry("sus", "sus", "interj"),
        Entry("hola", "hola", "interj"),
    ]);

    private static readonly MorphologyGenerator Morphology = new(TestLexicon);
    private static readonly IAnalysisRules Rules = SpanishLanguagePack.Instance.CreateAnalysisRules(TestLexicon, Morphology);
    private static readonly LinguisticAnalyzer Analyzer = new(TestLexicon, Morphology, SpanishLanguagePack.Instance);

    private static IReadOnlyList<ErrorTag> Errors(string text, params string[] grammarPatterns)
    {
        return Analyzer.Analyze(TestTokens.From(text), grammarPatterns.Select(pattern => SlotGrammar.Parse(pattern)).ToList()).Errors;
    }

    private static string Describe(IReadOnlyList<ErrorTag> errors)
    {
        return errors.Count == 0 ? "no errors" : string.Join("; ", errors.Select(static e => $"{e.Tag} '{e.Actual}' -> '{e.Expected}'"));
    }

    private static int CountErrors(IReadOnlyList<AnalyzedToken> tokens)
    {
        var found = new List<ErrorTag>();
        Rules.CreateAgreementChecker(tokens, found).Run();
        return found.Distinct().Count();
    }

    // The resolver's output for a text: the tokens with every lexicon reading go in, with the best match of the grammar patterns if any.
    private static IReadOnlyList<AnalyzedToken> Resolve(string text, params string[] grammarPatterns)
    {
        var tokens = Analyzer.Analyze(TestTokens.From(text)).Tokens;
        SlotGrammarMatch? best = null;
        foreach (var pattern in grammarPatterns)
        {
            var match = SlotGrammarMatcher.Match(tokens, SlotGrammar.Parse(pattern), SpanishLanguagePack.Instance);
            if (match is not null && (best is null || match.ExplicitTokenCount > best.ExplicitTokenCount))
            {
                best = match;
            }
        }

        return ReadingResolver.Resolve(tokens, tokens.Select(static _ => (IReadOnlyList<LexicalFeatures>)[]).ToList(), best, CountErrors, Rules, SpanishLanguagePack.Instance.Folding);
    }

    private static string PosOf(IReadOnlyList<AnalyzedToken> tokens, int index)
    {
        return string.Join("+", tokens[index].Readings.Select(static r => r.Pos).Distinct().Order(StringComparer.Ordinal));
    }

    [Fact]
    public void AWordAfterAnArticleOrNumeralIsTheNoun()
    {
        Assert.Equal("noun", PosOf(Resolve("un libro"), 1));
        Assert.Equal("noun", PosOf(Resolve("dos libros"), 1));
        Assert.Equal("noun", PosOf(Resolve("ayer compré una lanza"), 3));
    }

    [Fact]
    public void AWordAfterAnAdjectiveThatOpensAPhraseIsTheNoun()
    {
        // The word behind a prenominal adjective, with or without a determiner, is the noun the phrase is about.
        Assert.Equal("noun", PosOf(Resolve("un correcto libro"), 2));
        Assert.Equal("noun", PosOf(Resolve("correcto paseo"), 1));
        Assert.Contains(Errors("correcto paseo"), e => e.Tag == ErrorTags.WordOrder);
    }

    [Fact]
    public void AWordAfterASubjectPronounOrAClitic_IsTheVerb()
    {
        Assert.Equal("verb", PosOf(Resolve("yo libro"), 1));
        Assert.Equal("verb", PosOf(Resolve("me lanza"), 1));
        Assert.Equal("verb", PosOf(Resolve("yo siempre libro"), 2));
        Assert.Equal(ErrorTags.ConjPerson, Assert.Single(Errors("tú lanzo")).Tag);
        Assert.Equal(ErrorTags.ConjPerson, Assert.Single(Errors("tú libro")).Tag);
    }

    [Fact]
    public void AWordAfterAPrepositionOrACopula_IsNotAFiniteVerb()
    {
        Assert.Equal("noun", PosOf(Resolve("de lanza"), 1));
        Assert.Equal("noun", PosOf(Resolve("a libro"), 1));
        Assert.Equal("noun", PosOf(Resolve("es libro"), 1));
        Assert.Equal("noun", PosOf(Resolve("soy lanza"), 1));
    }

    [Fact]
    public void AGrammarBindingResolvesWhatLocalSyntaxLeavesOpen()
    {
        // quiero libro: nothing around libro says whether it is the noun or a verb form, and the tie goes to the earlier reading (the lemma librar sorts before libro).
        Assert.Equal("verb", PosOf(Resolve("quiero libro"), 1));
        Assert.Equal("noun", PosOf(Resolve("quiero libro", "[VERB:querer] [NOUN:item]"), 1));
        Assert.Equal("verb", PosOf(Resolve("quiero libro", "[VERB:querer] [VERB:librar]"), 1));
    }

    [Fact]
    public void AGrammarBindingUnitesTheReadingsOfAWordThatTiedParsesReadInDifferentWays()
    {
        // Two optional elements that can take the same word give two best parses, and the word keeps what either of them accepts: with [NOUN]? beside [VERB]? the
        // noun reading stays open and costs nothing, while a grammar that has only the verb for the word forces the verb and with it a tense mistake.
        Assert.Empty(Errors("ayer compré libro", "ayer [VERB:comprar] [NOUN]? [VERB]?"));
        Assert.Equal(ErrorTags.ConjTense, Assert.Single(Errors("ayer compré libro", "ayer [VERB:comprar] [VERB]")).Tag);
    }

    [Fact]
    public void LocalSyntaxWinsWhenAGrammarBindingDisagrees()
    {
        // The grammar reads lanza as [NOUN:spell] (yo is its implicit subject), but a word right after a subject pronoun is the verb, and the person is wrong.
        var errors = Errors("yo lanza", "[VERB:lanzar]? [ART]? [NOUN:spell]");

        Assert.Equal(ErrorTags.ConjPerson, Assert.Single(errors).Tag);
        Assert.Equal("verb", PosOf(Resolve("yo lanza", "[VERB:lanzar]? [ART]? [NOUN:spell]"), 1));
    }

    [Fact]
    public void ALiteralThatIsSpelledLikeTheWordSaysNothingAboutItsPartOfSpeech()
    {
        // The literal libro is a form, not a reading, so the fallback still reads the word as the noun; the literal librar matches libro through the lemma of its
        // verb reading, and that reading is the one the grammar asked for.
        Assert.Empty(Errors("ayer compré libro", "ayer compré libro"));
        Assert.Equal(ErrorTags.ConjTense, Assert.Single(Errors("ayer compré libro", "ayer compré librar")).Tag);
    }

    [Fact]
    public void TheFallbackTakesTheReadingWithFewerErrors()
    {
        // With another finite verb in the sentence the noun reading of libro costs nothing and the verb reading costs a tense mistake.
        Assert.Equal("noun", PosOf(Resolve("ayer compré libro"), 2));
        Assert.Empty(Errors("ayer compré libro"));
    }

    [Fact]
    public void APastTimeAdverbWithoutAFiniteVerbCostsMoreThanOneTenseMistake()
    {
        // ayer canto: read as a noun the sentence has an adverb of the past and no verb, which costs two; read as the verb it costs one tense mistake, so the mistake is
        // not explained away. Without the adverb nothing is charged and the noun reading stands: canto correcto espada is a noun and its adjective, then another noun,
        // and not a verb that makes correcto espada a word-order mistake.
        Assert.Equal(ErrorTags.ConjTense, Assert.Single(Errors("ayer canto")).Tag);
        Assert.Equal(ErrorTags.ConjTense, Assert.Single(Errors("ayer libro")).Tag);
        Assert.Empty(Errors("canto correcto espada"));
    }

    [Fact]
    public void AnInterjectionReadingIsTheLastResort()
    {
        Assert.Equal("adj", PosOf(Resolve("sus libros"), 0));
        Assert.Equal("interj", PosOf(Resolve("hola"), 0));
    }

    [Fact]
    public void ADeterminerAdjectiveWinsOverAnAdjectiveOfTheSameSpelling()
    {
        // sus is the plural of su and, in this lexicon, an adjective of its own: the determiner reading is the one that opens the phrase.
        var sus = Resolve("sus libros")[0];

        Assert.All(sus.Readings, r => Assert.Equal("su", r.Lemma));
        Assert.Contains(Errors("sus libro"), e => e.Tag == ErrorTags.NounNumber);
    }

    [Fact]
    public void AQuantifierBeforeANounIsTheDeterminerNotTheAdverb()
    {
        Assert.Equal("adj", PosOf(Resolve("mucho espada"), 0));
        Assert.Equal(ErrorTags.GenderAgree, Assert.Single(Errors("mucho espada")).Tag);
        Assert.Equal("adj", PosOf(Resolve("mucho lanza"), 0));
        Assert.Equal(ErrorTags.GenderAgree, Assert.Single(Errors("mucho lanza")).Tag);
    }

    [Fact]
    public void AnAdverbDoesNotModifyANounButSomeAdverbsDo()
    {
        // temprano pasillo: temprano cannot be the adverb before a noun, so it is the adjective in front of its noun. solo espada: solo is the adverb that goes before nouns.
        Assert.Equal("adj", PosOf(Resolve("temprano pasillo"), 0));
        Assert.Contains(Errors("temprano pasillo"), e => e.Tag == ErrorTags.WordOrder);
        Assert.Empty(Errors("solo espada"));
    }

    [Fact]
    public void ATokenWithOneClassIsReturnedAsItIsAndAnUnknownWordKeepsNoReadings()
    {
        var all = Analyzer.Analyze(TestTokens.From("un xyzzy")).Tokens;
        var resolved = ReadingResolver.Resolve(all, all.Select(static _ => (IReadOnlyList<LexicalFeatures>)[]).ToList(), null, CountErrors, Rules, SpanishLanguagePack.Instance.Folding);

        Assert.Same(all[0], resolved[0]);
        Assert.Empty(resolved[1].Readings);
    }

    [Fact]
    public void ALongSentenceIsSettledInBoundedTimeAndTheSameWayEveryTime()
    {
        // Fourteen ambiguous words are 2^14 combinations, far above the exhaustive bound, so the fallback descends word by word.
        var text = "ayer compré " + string.Join(' ', Enumerable.Repeat("libro", 14));
        var first = Resolve(text);
        var second = Resolve(text);

        Assert.True(Math.Pow(2, 14) > ReadingResolver.MaxExhaustiveCombinations);
        Assert.Equal(first.Select(static t => string.Join(",", t.Readings.Select(static r => r.Lemma + r.Pos))), second.Select(static t => string.Join(",", t.Readings.Select(static r => r.Lemma + r.Pos))));
        Assert.Equal(Errors(text), Errors(text));
        Assert.Empty(Errors(text));
    }

    [Fact]
    public void ATieGoesToTheEarlierClassInReadingOrder()
    {
        // Neither reading of libro makes an error in "libro libro" (a sentence of homographs only has no subject pronoun and no past adverb); the earlier reading of
        // each word in the lexicon's order is the verb form of librar, and the result is the same on every call.
        var first = Resolve("libro libro");
        var second = Resolve("libro libro");

        Assert.Equal(PosOf(first, 0), PosOf(second, 0));
        Assert.Equal("verb", PosOf(first, 0));
    }
}
