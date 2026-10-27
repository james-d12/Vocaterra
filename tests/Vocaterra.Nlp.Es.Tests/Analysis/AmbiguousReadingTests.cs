namespace Vocaterra.Nlp.Es.Tests.Analysis;

using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Packs;

/// <summary>
/// Regression tests for the analyzer on words that have several readings, which an imported lexicon makes common: an imperative that is spelled like the
/// third person, a form that is an adjective under two lemmas, a noun form that reads as singular and plural. They were found by the fuzz oracles when the
/// Wiktionary lexicon was added; each test fails with the old first-reading logic.
/// </summary>
public class AmbiguousReadingTests
{
    private static LexiconEntry Verb(string form, string lemma, string? person, string? number, string? tense, string? mood)
    {
        return new LexiconEntry(form, lemma, "verb", new EntryFeatures(person, number, tense, mood, null));
    }

    private static LexiconEntry Pronoun(string form, string person, string number)
    {
        return new LexiconEntry(form, form, "pron", new EntryFeatures(person, number, null, null, null));
    }

    private static LexiconEntry Adjective(string form, string lemma, string number, string gender)
    {
        return new LexiconEntry(form, lemma, "adj", new EntryFeatures(null, number, null, null, gender));
    }

    private static LexiconEntry Noun(string form, string lemma, string number, string gender)
    {
        return new LexiconEntry(form, lemma, "noun", new EntryFeatures(null, number, null, null, gender));
    }

    private static LexiconEntry Article(string form, string number, string gender)
    {
        return new LexiconEntry(form, "el", "art", new EntryFeatures(null, number, null, null, gender));
    }

    // The lexicon an import produces for these words: habla and está are also imperatives, ocupada is a lemma of its own next to ocupado,
    // corazones is the plural of corazón and (as a card suit) a singular noun of its own.
    private static readonly LinguisticAnalyzer Analyzer = new(SpanishLexicon.FromEntries(
    [
        Pronoun("tú", "2", "sg"),
        Pronoun("ella", "3", "sg"),
        Verb("hablar", "hablar", null, null, null, null),
        Verb("hablas", "hablar", "2", "sg", "pres", "ind"),
        Verb("habla", "hablar", "3", "sg", "pres", "ind"),
        Verb("habla", "hablar", "2", "sg", null, "imp"),
        Verb("ser", "ser", null, null, null, null),
        Verb("es", "ser", "3", "sg", "pres", "ind"),
        Verb("eres", "ser", "2", "sg", "pres", "ind"),
        Verb("está", "estar", "3", "sg", "pres", "ind"),
        Verb("está", "estar", "2", "sg", null, "imp"),
        Verb("estás", "estar", "2", "sg", "pres", "ind"),
        Adjective("alta", "alto", "sg", "f"),
        Adjective("ocupada", "ocupada", "sg", "f"),
        Adjective("ocupada", "ocupado", "sg", "f"),
        Adjective("malos", "malo", "pl", "m"),
        Adjective("malo", "malo", "sg", "m"),
        Adjective("rojos", "rojo", "pl", "m"),
        Noun("corazón", "corazón", "sg", "m"),
        Noun("corazones", "corazón", "pl", "m"),
        Noun("corazones", "corazones", "sg", "m"),
        Article("los", "pl", "m"),
    ]), SpanishLanguagePack.Instance);

    private static IReadOnlyList<ErrorTag> Errors(string text)
    {
        return Analyzer.Analyze(TestTokens.From(text)).Errors;
    }

    [Fact]
    public void AnImperativeThatIsSpelledLikeTheThirdPersonDoesNotExcuseAPersonMistake()
    {
        var error = Assert.Single(Errors("tú habla"));

        Assert.Equal(ErrorTags.ConjPerson, error.Tag);
        Assert.Equal("hablas", error.Expected);
        Assert.Equal("habla", error.Actual);
        Assert.Empty(Errors("tú hablas"));
    }

    [Fact]
    public void TheExpectedCopulaFormComesFromTheDeclarativeReadingNotFromTheImperative()
    {
        // está is also the second person imperative of estar; the correction for "ella está alta" is es (third person), not eres.
        var error = Assert.Single(Errors("ella está alta"), e => e.Tag == ErrorTags.SerEstar);

        Assert.Equal("es", error.Expected);
    }

    [Fact]
    public void AFormThatIsAnAdjectiveUnderTwoLemmasIsClassifiedByAllOfThem()
    {
        // ocupada sorts before ocupado, but ocupado is an estar adjective, so ser is the mistake whichever reading is listed first.
        var wrong = Assert.Single(Errors("ella es ocupada"), e => e.Tag == ErrorTags.SerEstar);

        Assert.Equal("está", wrong.Expected);
        Assert.DoesNotContain(Errors("ella está ocupada"), e => e.Tag == ErrorTags.SerEstar);
    }

    [Fact]
    public void ANounFormThatIsSingularAndPluralAgreesWithAnAdjectiveOfEitherNumber()
    {
        Assert.Empty(Errors("malos corazones"));
        Assert.Empty(Errors("malo corazones"));
    }

    [Fact]
    public void ADeterminerStillFixesTheNumberOfAnAmbiguousNounPhrase()
    {
        // With a plural article the phrase is plural, so a singular adjective is still a number mistake.
        Assert.Empty(Errors("los corazones rojos"));
        Assert.Contains(Errors("los corazones malo"), e => e.Tag == ErrorTags.NounNumber);
    }
}
