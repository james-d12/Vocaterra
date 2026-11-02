namespace Vocaterra.Nlp.Es.Tests.Analysis;

using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.SlotGrammar;

/// <summary>
/// M70 (QA-language-4): <c>gustar</c> and <c>encantar</c> agree with the thing liked, and the one who likes is a dative pronoun. Before M70 <c>me gusto el pan</c> and
/// <c>yo gusto el pan</c> were clean (the game praised them at <c>lesson-like-bread</c>); each mistake below gave no error, and the clean neighbours are tests as
/// well. Runs on the bundle's lexicon, with the game's <c>state_like</c> grammar where it says so.
/// </summary>
public class LikingVerbTests
{
    // The game's grammar of intent state_like, as the prompts lesson-like-bread and farmer-open match it.
    private const string LikeGrammar = "(ayer|hoy)? (sí|no)? me [VERB:gustar] (mucho)? [ART] [NOUN:item] (de (luz|domingo|mar|león))? (ayer|hoy)?";

    private static readonly LinguisticAnalyzer Analyzer = new(TestSupport.Bundle.Lexicon, SpanishLanguagePack.Instance);

    private static string Described(string sentence, params string[] grammars)
    {
        var parsed = grammars.Select(static g => SlotGrammar.Parse(g)).ToList();
        return string.Join(';', Analyzer.Analyze(TestTokens.From(sentence), parsed).Errors.Select(static e => $"{e.Tag}|{e.Actual}|{e.Expected}"));
    }

    // A form of the first or second person behind its dative pronoun, with the thing liked after it: the form of the thing.
    [Theory]
    [InlineData("me gusto el pan", "conj.person|gusto|gusta")]
    [InlineData("me gusto las manzanas", "conj.person|gusto|gustan")]
    [InlineData("te gustas el pan", "conj.person|gustas|gusta")]
    [InlineData("no me gusto la fruta", "conj.person|gusto|gusta")]
    [InlineData("me encanto el pan", "conj.person|encanto|encanta")]
    [InlineData("nos gustamos las manzanas", "conj.person|gustamos|gustan")]
    public void AFirstOrSecondPersonBehindTheDativeTakesTheFormOfTheThingLiked(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
        Assert.Equal(expected, Described(sentence, LikeGrammar));
    }

    // M69 (an O4 finding of M69's runs, seed cTAdl0hRGj58): gustamos is the present and the preterite, and after a past marker the form of the thing liked is the
    // preterite's (before M69 the present's: ayer no me gustamos unos aparatos gave gustan).
    [Theory]
    [InlineData("ayer no me gustamos unos aparatos", "conj.person|gustamos|gustaron")]
    [InlineData("ayer nos gustamos el pan", "conj.person|gustamos|gustó")]
    [InlineData("hoy nos gustamos el pan", "conj.person|gustamos|gusta")]
    public void AFormOfTwoTensesAfterAPastMarkerTakesThePreterite(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    // A subject pronoun and no dative: the dative of the pronoun with the form of the thing, over the pronoun and the verb, so the recast says me gusta el pan.
    [Theory]
    [InlineData("yo gusto el pan", "conj.person|yo gusto|me gusta")]
    [InlineData("yo gusto las manzanas", "conj.person|yo gusto|me gustan")]
    [InlineData("yo no gusto el pan", "conj.person|yo no gusto|no me gusta")]
    [InlineData("tú gustas las manzanas", "conj.person|tú gustas|te gustan")]
    [InlineData("tú gusto el pan", "conj.person|tú gusto|te gusta")]
    [InlineData("ella gusta el pan", "conj.person|ella gusta|le gusta")]
    [InlineData("yo gusta el pan", "conj.person|yo gusta|me gusta")]
    [InlineData("nosotros gustamos la fruta", "conj.person|nosotros gustamos|nos gusta")]
    [InlineData("yo encanto el pan", "conj.person|yo encanto|me encanta")]
    public void ASubjectPronounWithNoDativeIsTheDative(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    // M56's rule for the third person, unchanged.
    [Theory]
    [InlineData("me gusta las manzanas", "conj.person|gusta|gustan")]
    [InlineData("me gustan el pan", "conj.person|gustan|gusta")]
    public void AThirdPersonOfTheOtherNumberIsAsBefore(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    [Theory]
    [InlineData("me gusta el pan")]
    [InlineData("me gustan las manzanas")]
    [InlineData("te gusta el pan")]
    [InlineData("a mí me gusta el pan")]
    [InlineData("no me gusta")]
    [InlineData("te gusta")]
    [InlineData("nos gusta la fruta")]
    [InlineData("me gustas tú")]
    [InlineData("te gusto yo")]
    [InlineData("me gusto")]
    [InlineData("te gusto")]
    [InlineData("tú me gustas")]
    [InlineData("yo te gusto")]
    [InlineData("el pan me gusta")]
    [InlineData("las manzanas me gustan")]
    [InlineData("a ella le gustan las manzanas")]
    [InlineData("me encanta el pan")]
    [InlineData("me encantan las manzanas")]
    [InlineData("me gusta mucho el pan")]
    [InlineData("me quedo el libro")]
    [InlineData("ella gusta de la música")]
    [InlineData("mucho gusto")]
    public void CorrectSpanishIsClean(string sentence)
    {
        Assert.Equal(string.Empty, Described(sentence));
    }
}
