namespace Vocaterra.Nlp.Es.Tests.Analysis;

using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Packs;
using Vocaterra.Nlp.Language.SlotGrammar;

/// <summary>
/// Sentences with words that the imported lexicon reads as several parts of speech (<c>libro</c> is a noun and a form of <c>librar</c>, <c>vino</c> a noun and a form
/// of <c>venir</c>, <c>ayudas</c> a noun and a form of <c>ayudar</c>, <c>como</c> a conjunction and a form of <c>comer</c>). The analyzer resolves each word to one
/// part of speech before the checks run (M18), so the correct sentences analyze clean and the mistakes in the incorrect ones are still found. The tests run on the
/// lexicon of the bundle, <c>content/game-v1/lexicon/*.json</c> merged the way the content loader merges it, because the homograph rate is a property of that vocabulary.
/// </summary>
public class HomographSentencesTests
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

    private static IReadOnlyList<ErrorTag> Errors(string sentence, params string[] grammarPatterns)
    {
        var grammars = grammarPatterns.Select(pattern => SlotGrammar.Parse(pattern)).ToList();
        return Analyzer.Value.Analyze(TestTokens.From(sentence), grammars).Errors;
    }

    private static string Describe(IReadOnlyList<ErrorTag> errors)
    {
        return errors.Count == 0 ? "no errors" : string.Join("; ", errors.Select(static e => $"{e.Tag} '{e.Actual}' -> '{e.Expected}'"));
    }

    private const string BuyQuerer = "[VERB:querer] [VERB:comprar] [ART]? [NUM:qty]? [NOUN:item]";
    private const string BuyComprar = "[VERB:comprar] [ART]? [NUM:qty]? [NOUN:item]";
    private const string Offer = "[VERB:tener:verb] [NUM:qty] [NOUN:item]";
    private const string AskPrice = "cuánto [VERB:costar] [ART]? [NOUN:item]";
    private const string CastSpell = "[VERB:lanzar]? [ART]? [NOUN:spell] [ADJ]?";
    private const string CastOf = "[ART]? (bola|lanza) de [NOUN:spell]";

    public static TheoryData<string> CorrectSentences { get; } = new()
    {
        // The sentences of the milestone brief.
        "ayer compré un libro",
        "quiero comprar una lanza",
        "la lanza es grande",
        "el vino es bueno",
        "él vino ayer",
        "las ayudas",
        "tú ayudas",
        "nosotros comemos como siempre",
        "ellos fueron a casa",
        "la casa es grande",
        "yo libro",

        // A noun that is also a verb form, after an article, a numeral or a possessive, with and without a past-time adverb.
        "ayer compré una lanza",
        "ayer compré un vino",
        "anoche vendí un libro",
        "anteayer perdí el vino",
        "ayer él compró un libro",
        "ayer vi una lanza",
        "los libros son buenos",
        "las lanzas son grandes",
        "las ayudas son buenas",
        "dos libros",
        "mis libros",

        // The same words as the verb, after a subject pronoun, a clitic or a noun phrase.
        "yo canto",
        "yo trabajo",
        "ella lanza una piedra",
        "mi madre cocina",
        "ayer canté una canción",
        "ayer ayudé a mi madre",
        "ayer vino mi hermano",
        "ayer fuimos a casa",
        "yo sé",
        "sé preocupado",
        "ella está ocupada",
        "malos corazones",
        "los corazones son malos",

        // Nouns and adjectives that read as both genders, and words before or after a copula.
        "el cura es bueno",
        "la cura es buena",
        "la estudiante es alta",
        "los estudiantes son altos",
        "ella es alta",
        "el trabajo es difícil",
        "la cocina es grande",
        "ellos fueron buenos",

        // Verbs and nouns with no article at all, and fixed greetings.
        "quiero vino",
        "compro libros",
        "buenos días",
        "buenas tardes",
    };

    public static TheoryData<string, string[]> CorrectSentencesWithGrammars { get; } = new()
    {
        // buy-item-open: the sentence with ayer matches no grammar; the others match one, and its [NOUN:item] binds the homograph as a noun.
        { "ayer compré un libro", [BuyQuerer, BuyComprar] },
        { "quiero comprar una lanza", [BuyQuerer, BuyComprar] },
        { "quiero comprar un vino", [BuyQuerer, BuyComprar] },
        { "compro un libro", [BuyQuerer, BuyComprar] },
        { "compro dos libros", [BuyQuerer, BuyComprar] },

        // offer-items-open, ask-price-open and spell-cast-open.
        { "tengo dos lanzas", [Offer] },
        { "yo tengo dos vinos", [Offer] },
        { "cuánto cuesta la espada", [AskPrice] },
        { "cuánto cuesta el escudo", [AskPrice] },
        { "cuánto cuesta la poción", [AskPrice] },
        { "cuánto cuesta la lanza", [AskPrice] },
        { "cuánto cuesta el libro", [AskPrice] },
        { "lanzo fuego", [CastSpell, CastOf] },
        { "una lanza de fuego", [CastSpell, CastOf] },
        { "lanzo la lanza", [CastSpell, CastOf] },

        // The interrogative adverb reading of cuánto must win without a grammar as well.
        { "cuánto cuesta la espada", [] },
        { "cuánto cuesta el escudo", [] },
        { "cuánto cuesta la poción", [] },
        { "cuánto cuesta", [] },
        { "cuánto cuesta xyzzy", [] },
    };

    [Theory]
    [MemberData(nameof(CorrectSentences))]
    public void ACorrectSentenceWithAHomographAnalyzesClean(string sentence)
    {
        var errors = Errors(sentence);

        Assert.True(errors.Count == 0, $"'{sentence}' should analyze clean but produced {Describe(errors)}.");
    }

    [Theory]
    [MemberData(nameof(CorrectSentencesWithGrammars))]
    public void ACorrectSentenceAnalyzesCleanUnderItsGrammars(string sentence, string[] grammars)
    {
        var errors = Errors(sentence, grammars);

        // The unknown word of the last sentence is the one error it is meant to keep: the unknown word must not make cuánto an adjective that agrees with the verb.
        var unexpected = errors.Where(e => !(sentence.EndsWith("xyzzy", StringComparison.Ordinal) && e.Tag == ErrorTags.VocabUnknown)).ToList();
        Assert.True(unexpected.Count == 0, $"'{sentence}' (grammars: {string.Join(" | ", grammars)}) should analyze clean but produced {Describe(unexpected)}.");
    }

    // sentence, then the errors it must produce as "tag|actual|expected", in the analyzer's order.
    public static TheoryData<string, string[]> IncorrectSentences { get; } = new()
    {
        // A past-time adverb with a present verb; the nouns in the same sentence are not touched.
        { "ayer compro un libro", ["conj.tense|compro|compré"] },
        { "ayer compro una lanza", ["conj.tense|compro|compré"] },
        { "ayer canto una canción", ["conj.tense|canto|canté"] },
        { "ayer ella lanza una piedra", ["conj.tense|lanza|lanzó"] },
        { "ayer quiero comprar una lanza", ["conj.tense|quiero|quise"] },

        // A verb form that is also a noun, right after a subject pronoun.
        { "yo lanza", ["conj.person|lanza|lanzo"] },
        { "yo ayudas", ["conj.person|ayudas|ayudo"] },
        { "tú ayuda", ["conj.person|ayuda|ayudas"] },
        { "ellos ayuda", ["conj.person|ayuda|ayudan"] },
        { "nosotros como", ["conj.person|como|comemos"] },

        // A noun that is also a verb form, behind an article, a numeral or a possessive that does not agree with it.
        { "un lanza", ["gender.agree|un|una"] },
        { "una libro", ["gender.agree|una|un"] },
        { "el lanza", ["gender.agree|el|la"] },
        { "dos libro", ["noun.number|libro|libros"] },
        { "mis libro", ["noun.number|libro|libros"] },
        { "muchos vino", ["noun.number|vino|vinos"] },
        { "mucho espada", ["gender.agree|mucho|mucha"] },
        { "este espada", ["gender.agree|este|esta"] },
        { "la libros", ["gender.agree|la|el", "noun.number|libros|libro"] },

        // The copula classes, with an adjective that is a lemma of its own (ocupada) and one that is not.
        { "soy ocupado", ["ser_estar|soy|estoy"] },
        { "ella es ocupada", ["ser_estar|es|está"] },
        // M62: the adjective agrees with ella as well (ella es alta).
        { "ella está alto", ["ser_estar|está|es", "gender.agree|alto|alta"] },
        { "una casa grandes", ["noun.number|grandes|grande"] },
        { "una buen casa", ["gender.agree|buen|buena"] },

        // Under a grammar the mistakes are the same.
        { "cuánto cuesta el espada", ["gender.agree|el|la"] },
        { "yo tiene dos espada", ["conj.person|tiene|tengo", "noun.number|espada|espadas"] },
    };

    [Theory]
    [MemberData(nameof(IncorrectSentences))]
    public void AnIncorrectSentenceKeepsItsMistake(string sentence, string[] expected)
    {
        var errors = Errors(sentence);

        var actual = errors.Select(static e => $"{e.Tag}|{e.Actual}|{e.Expected}").ToArray();
        Assert.True(
            expected.SequenceEqual(actual, StringComparer.Ordinal),
            $"'{sentence}' should produce [{string.Join("; ", expected)}] but produced {Describe(errors)}.");
    }

    [Theory]
    [InlineData("cuánto cuesta el espada", AskPrice)]
    [InlineData("yo lanza", CastSpell)]
    [InlineData("un lanza", CastSpell)]
    [InlineData("ayer compro un libro", BuyComprar)]
    public void AGrammarDoesNotHideAMistakeThatTheNeighbouringWordsShow(string sentence, string grammar)
    {
        // Local syntax wins over a grammar binding: yo lanza matches [VERB:lanzar]? [ART]? [NOUN:spell] with lanza as the noun, and is still a person mistake.
        var withGrammar = Errors(sentence, grammar);
        var without = Errors(sentence);

        Assert.True(
            without.Count > 0 && withGrammar.Select(static e => e.Tag).SequenceEqual(without.Select(static e => e.Tag), StringComparer.Ordinal),
            $"'{sentence}' under '{grammar}' produced {Describe(withGrammar)}, but without a grammar {Describe(without)}.");
    }

    [Fact]
    public void ThePastTimeAdverbDoesNotChangeTheReadingOfANounAfterAnArticle()
    {
        foreach (var adverb in new[] { "ayer", "anoche", "anteayer" })
        {
            var errors = Errors($"{adverb} compré un libro");

            Assert.True(errors.Count == 0, $"'{adverb} compré un libro' should analyze clean but produced {Describe(errors)}.");
        }
    }

    [Fact]
    public void TheAnalysisIsTheSameWhenItIsRepeatedOnAFreshAnalyzer()
    {
        var sentence = "ayer compro un libro y la lanza es grande";
        var first = Errors(sentence);
        var second = BuildAnalyzer().Analyze(TestTokens.From(sentence)).Errors;

        Assert.Equal(first, second);
    }

    [Fact]
    public void TheTokensStillListEveryReadingOfTheLexicon()
    {
        // The resolution is internal to the checks; the intent stage matches keyword rules on the lemmas of all readings.
        var result = Analyzer.Value.Analyze(TestTokens.From("ayer compré un libro"));

        Assert.Contains(result.Tokens[3].Readings, r => r is { Pos: "noun", Lemma: "libro" });
        Assert.Contains(result.Tokens[3].Readings, r => r is { Pos: "verb", Lemma: "librar" });
    }
}
