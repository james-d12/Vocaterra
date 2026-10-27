namespace Vocaterra.Nlp.Response.Tests.Templates;

using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Normalization;
using Vocaterra.Nlp.Language.Packs;
using Vocaterra.Nlp.Response.Templates;

/// <summary>
/// M29c: the recast of an agreement error on a modifier keeps the noun that is right ("no, mucho gracias" gives "no, muchas gracias", never "no mucha gracia"; "buenos día"
/// gives "buenos días", never "bueno día"), and the generic recasts (a determiner fixes the number of the noun) are unchanged. The lexicon is the seed plus the noun
/// <c>gracia</c>, which the seed lacks (it has <c>gracias</c> only as an interjection; the bundle imports the noun).
/// </summary>
public class RecastAgreementModifierTests
{
    private static readonly Lexicon Words = SpanishLexicon.FromEntries(
    [
        .. SpanishLexicon.Seed.Entries,
        new LexiconEntry("gracia", "gracia", "noun", new EntryFeatures(null, "sg", null, null, "f")),
        new LexiconEntry("gracias", "gracia", "noun", new EntryFeatures(null, "pl", null, null, "f")),
    ]);

    private static readonly LinguisticAnalyzer Analyzer = new(Words, SpanishLanguagePack.Instance);

    private static string Recast(string player)
    {
        var text = SpanishTextNormalizer.Normalize(player, new NormalizationOptions { NumbersToDigits = false, ConvertIndefiniteArticles = false });
        return RecastBuilder.Build(text, Analyzer.Analyze(text), Words, SpanishLanguagePack.Instance);
    }

    [Theory]
    [InlineData("no mucho gracias", "no muchas gracias")]
    [InlineData("mucho gracias", "muchas gracias")]
    [InlineData("mucha gracias", "muchas gracias")]
    [InlineData("muchas gracia", "muchas gracias")]
    [InlineData("buenos día", "buenos días")]
    [InlineData("buenas noche", "buenas noches")]
    [InlineData("buenas tarde", "buenas tardes")]
    [InlineData("buenos noches", "buenas noches")]
    [InlineData("buena días", "buenos días")]
    [InlineData("buenas días", "buenos días")]
    public void TheFixedPluralExpressionIsRecastWithTheModifierAgreeingWithThePluralNoun(string player, string expected)
    {
        Assert.Equal(expected, Recast(player));
    }

    [Theory]
    [InlineData("muchas gracias")]
    [InlineData("buenos días")]
    [InlineData("mucha gracia")]
    public void ACorrectExpressionIsRecastUnchanged(string player)
    {
        Assert.Equal(player, Recast(player));
    }

    [Theory]
    [InlineData("dos espada", "dos espadas")]
    [InlineData("quiero dos agua por favor", "quiero dos aguas por favor")]
    [InlineData("la río", "el río")]
    [InlineData("el casas", "la casa")]
    [InlineData("las casa", "las casas")]
    [InlineData("un casas", "una casa")]
    [InlineData("mucho espada", "mucha espada")]
    [InlineData("yo tiene dos espada", "yo tengo dos espadas")]
    public void TheGenericRecastsAreUnchanged(string player, string expected)
    {
        Assert.Equal(expected, Recast(player));
    }

    [Fact]
    public void TheExpectedFormOfAModifierErrorIsTheFormTheRecastUses()
    {
        var text = SpanishTextNormalizer.Normalize("no mucho gracias", new NormalizationOptions { NumbersToDigits = false, ConvertIndefiniteArticles = false });
        var errors = Analyzer.Analyze(text).Errors;
        Assert.Contains(errors, static e => e.Tag == ErrorTags.GenderAgree && e.Expected == "muchas" && e.Actual == "mucho");
        Assert.DoesNotContain(errors, static e => e.Expected is "gracia" or "mucha");
    }
}
