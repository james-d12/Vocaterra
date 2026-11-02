namespace Vocaterra.Nlp.Es.Tests.Analysis;

using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Lexicon;

/// <summary>
/// M29c: an agreement error on a modifier must not "correct" a noun that is right. In a fixed plural expression (muchas gracias, buenos días, buenas tardes, buenas
/// noches) the plural is kept and the modifier takes the noun's gender; everywhere else the generic rule stands (a determiner fixes the number of the noun, an
/// adjective agrees with its noun). The tests run on the lexicon of the bundle, because <c>gracias</c> is an interjection in the seed and the plural of the noun
/// <c>gracia</c> only in the imported entries.
/// </summary>
public class FixedPluralExpressionTests
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

    private static string[] Described(string sentence)
    {
        return Analyzer.Value.Analyze(TestTokens.From(sentence), []).Errors
            .Select(static e => $"{e.Tag}|{e.Actual}|{e.Expected}")
            .ToArray();
    }

    [Theory]
    [InlineData("muchas gracias")]
    [InlineData("buenos días")]
    [InlineData("buenas tardes")]
    [InlineData("buenas noches")]
    [InlineData("mucha gracia")]
    [InlineData("buena noche")]
    [InlineData("buen día")]
    public void TheCorrectExpressionsAreClean(string sentence)
    {
        Assert.Empty(Described(sentence));
    }

    [Theory]
    [InlineData("mucho gracias", new[] { "gender.agree|mucho|muchas", "noun.number|mucho|muchas" })]
    [InlineData("muchos gracias", new[] { "gender.agree|muchos|muchas" })]
    [InlineData("mucha gracias", new[] { "noun.number|mucha|muchas" })]
    [InlineData("no mucho gracias", new[] { "gender.agree|mucho|muchas", "noun.number|mucho|muchas" })]
    [InlineData("muchas gracia", new[] { "noun.number|gracia|gracias" })]
    [InlineData("buenos día", new[] { "noun.number|día|días" })]
    [InlineData("buenas noche", new[] { "noun.number|noche|noches" })]
    [InlineData("buenas tarde", new[] { "noun.number|tarde|tardes" })]
    [InlineData("buenos noches", new[] { "gender.agree|buenos|buenas" })]
    [InlineData("buena noches", new[] { "noun.number|buena|buenas" })]
    [InlineData("buenas días", new[] { "gender.agree|buenas|buenos" })]
    [InlineData("bueno tardes", new[] { "gender.agree|bueno|buenas", "noun.number|bueno|buenas" })]
    public void TheModifierIsCorrectedAndThePluralNounIsKept(string sentence, string[] expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    [Theory]
    [InlineData("mucho gracia", new[] { "gender.agree|mucho|mucha" })]
    [InlineData("bueno noche", new[] { "gender.agree|bueno|buena" })]
    public void ASingularExpressionKeepsTheGenericGenderCheck(string sentence, string[] expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    [Theory]
    [InlineData("dos espada", new[] { "noun.number|espada|espadas" })]
    [InlineData("quiero dos agua por favor", new[] { "noun.number|agua|aguas" })]
    [InlineData("la río", new[] { "gender.agree|la|el" })]
    [InlineData("el casas", new[] { "gender.agree|el|la", "noun.number|casas|casa" })]
    [InlineData("las casa", new[] { "noun.number|casa|casas" })]
    [InlineData("un casas", new[] { "gender.agree|un|una", "noun.number|casas|casa" })]
    [InlineData("los buenos día", new[] { "noun.number|día|días" })]
    [InlineData("dos buenos día", new[] { "noun.number|día|días" })]
    [InlineData("muchos vino", new[] { "noun.number|vino|vinos" })]
    [InlineData("yo tiene dos espada", new[] { "conj.person|tiene|tengo", "noun.number|espada|espadas" })]
    public void TheGenericRuleIsUnchangedOutsideTheFixedExpressions(string sentence, string[] expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    // M58 (found by its check of correct learner lines at the game's prompts): a fixed expression after a noun is its own phrase, not adjectives of that noun.
    // hola Luz buenos días (the baker's name, then the greeting) gave gender.agree and noun.number buena on buenos, as luz's adjective, before M58.
    [Theory]
    [InlineData("hola luz buenos días")]
    [InlineData("luz buenos días")]
    [InlineData("domingo buenas tardes")]
    [InlineData("hola mar buenas noches")]
    [InlineData("la casa roja buenas noches")]
    public void AFixedExpressionAfterANounIsItsOwnPhrase(string sentence)
    {
        Assert.Empty(Described(sentence));
    }

    // The expression's own checks still run after a noun, and an adjective of the noun is still agreed with it.
    [Theory]
    [InlineData("luz buenos día", new[] { "noun.number|día|días" })]
    [InlineData("hola luz buenas días", new[] { "gender.agree|buenas|buenos" })]
    [InlineData("la luz bueno", new[] { "gender.agree|bueno|buena" })]
    [InlineData("la luz buenos", new[] { "gender.agree|buenos|buena", "noun.number|buenos|buena" })]
    public void TheChecksAroundTheExpressionStillRun(string sentence, string[] expected)
    {
        Assert.Equal(expected, Described(sentence));
    }
}
