namespace Vocaterra.Nlp.Es.Tests.Analysis;

using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Packs;
using Vocaterra.Nlp.Language.SlotGrammar;

/// <summary>
/// M64 (a), the review of <c>feature/village</c> (R1): the person check took the subject pronoun right before the verb as its subject, with no look at a <c>y</c> before it,
/// so a coordinated subject was read as its last pronoun (<c>mar y yo fuimos al río</c> gave <c>conj.person</c> "fui", and the villager recast it into «Mar y yo fui al
/// río»); M62's predicate check did the same (<c>él y yo somos altos</c> gave <c>noun.number</c> "alto"). A pronoun joined by <c>y</c> to members before it that open
/// their clause now ends a plural subject: the first person plural when a member is of the first person, the second or the third plural when one is of the second, else
/// the third plural. <c>y</c> that joins two clauses is left as it was. Each correct line gave a false error before M64; the clause coordinations and the flagged
/// neighbours are tests as well. Runs on the lexicon of the bundle.
/// </summary>
public class CoordinatedSubjectTests
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

    // ---- The review's lines: correct Spanish that was flagged -------------------------------------------------------------------------------------------------

    // Session 8 models «Luz y yo fuimos a la tienda» and asks «¿Y Mar y tú? ¿Adónde fueron ayer?». Each gave conj.person "fui" before M64.
    [Theory]
    [InlineData("mar y yo fuimos al río")]
    [InlineData("luz y yo fuimos al río")]
    [InlineData("tú y yo fuimos al río")]
    [InlineData("mis amigos y yo fuimos al río")]
    [InlineData("y mar y yo fuimos al río")]
    [InlineData("ayer mar y yo fuimos al río")]
    [InlineData("por la mañana luz y yo fuimos a la tienda")]
    public void TheLessonsAnswerWithACoordinatedSubjectIsClean(string sentence)
    {
        Assert.Equal(string.Empty, Described(sentence));
    }

    // Before M64: tú y yo somos amigos gave conj.person "soy" (the game's own line guide.lesson.villager.be.1); usted y yo vamos "voy"; luz y yo compramos "compro";
    // él y yo somos altos "soy" and noun.number "alto"; ella y yo estamos cansadas "estoy" and noun.number "cansada"; ella y él son altos "es" and noun.number "alto";
    // tú y ella son altas and tú y ella sois altas "es"; ella y ella son altas "es"; nosotros y ellos somos amigos "son"; ella y yo somos amigas "soy".
    [Theory]
    [InlineData("tú y yo somos amigos")]
    [InlineData("usted y yo vamos al río")]
    [InlineData("luz y yo compramos pan")]
    [InlineData("él y yo somos altos")]
    [InlineData("ella y yo estamos cansadas")]
    [InlineData("ella y él son altos")]
    [InlineData("tú y ella son altas")]
    [InlineData("tú y ella sois altas")]
    [InlineData("ella y ella son altas")]
    [InlineData("nosotros y ellos somos amigos")]
    [InlineData("ella y yo somos amigas")]
    [InlineData("él y yo no fuimos al río")]
    [InlineData("ella y yo nos vamos")]
    public void APronounJoinedByYEndsAPluralSubject(string sentence)
    {
        Assert.Equal(string.Empty, Described(sentence));
    }

    // A member of the second person and none of the first: ustedes (the third person plural) is the plural of tú where vosotros is not used, as in the bundle's
    // dialects, and vosotros (the second) in Spain. Both are right.
    [Theory]
    [InlineData("tú y él son amigos")]
    [InlineData("tú y él sois amigos")]
    [InlineData("usted y tú van al río")]
    [InlineData("ella y tú comen pan")]
    [InlineData("ella y tú coméis pan")]
    public void ASecondPersonMemberTakesTheSecondOrTheThirdPlural(string sentence)
    {
        Assert.Equal(string.Empty, Described(sentence));
    }

    // A name the lexicon lacks is a member too (its vocab.unknown is the only error), and so is a phrase with de after it.
    [Theory]
    [InlineData("pedro y yo fuimos al río", "vocab.unknown|pedro|")]
    [InlineData("el hermano de luz y yo fuimos al río", "")]
    [InlineData("la hija del panadero y yo somos amigas", "vocab.unknown|panadero|")]
    [InlineData("la hija de domingo y yo somos amigas", "")]
    [InlineData("domingo y yo fuimos al río", "")]
    public void ANameAndAPhraseWithDeAreMembers(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    // ---- The verb agrees with the coordinated subject or is flagged ----------------------------------------------------------------------------------------

    // A verb that agrees with no accepted person is flagged with the form of the coordinated subject: the first member opens its clause, so y cannot join two clauses
    // there, and a person slip is what the lesson is about. tú y yo fui gave nothing before M64 (yo fui agrees); tú y ella es gave nothing (ella es). A correction for a
    // member of the second person proposes the third person plural.
    [Theory]
    [InlineData("tú y yo fui al río", "conj.person|fui|fuimos")]
    [InlineData("mar y yo fui al río", "conj.person|fui|fuimos")]
    [InlineData("luz y yo compro pan", "conj.person|compro|compramos")]
    [InlineData("mis amigos y yo fue al río", "conj.person|fue|fuimos")]
    [InlineData("tú y ella come pan", "conj.person|come|comen")]
    [InlineData("tú y yo comprar pan", "conj.person|comprar|compramos")]
    [InlineData("ella y él es alto", "conj.person|es|son;noun.number|alto|altos")]
    public void AVerbThatAgreesWithNoAcceptedPersonIsFlagged(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    // The predicate adjective is plural, and its gender is known only when every member has one: ella y él is masculine, ella y ella feminine; yo, tú and a name give
    // none, so only the number is checked.
    [Theory]
    [InlineData("ella y ella son altos", "gender.agree|altos|altas")]
    [InlineData("él y yo somos alto", "noun.number|alto|altos")]
    [InlineData("luz y yo somos alto", "noun.number|alto|altos")]
    [InlineData("ella y yo somos altos", "")]
    [InlineData("luz y yo somos altas", "")]
    public void APredicateAdjectiveTakesThePluralAndAKnownGender(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    // ---- y that joins two clauses ----------------------------------------------------------------------------------------------------------------------------

    // The pronoun after y is the subject of its own clause when the member before y follows a verb or a preposition, or when the word before y is no member (a verb).
    [Theory]
    [InlineData("luz hizo pan y yo fui al río")]
    [InlineData("comí pan y tú compraste fruta")]
    [InlineData("mar pescó y yo comí pescado")]
    [InlineData("luz compró pan y yo fui al río")]
    [InlineData("yo comí pan y tú bebiste agua")]
    [InlineData("ellos trabajan y yo descanso")]
    [InlineData("mar come fruta y ella bebe agua")]
    [InlineData("tú vendes pan y yo compro fruta")]
    [InlineData("domingo hace hachas y nosotros vendemos madera")]
    [InlineData("el niño come y yo bebo")]
    [InlineData("los niños juegan y tú trabajas")]
    [InlineData("luz es alta y yo soy bajo")]
    [InlineData("comemos pan y ellos beben agua")]
    [InlineData("fui al río y tú fuiste a la tienda")]
    [InlineData("fui al río con luz y yo comí pan")]
    [InlineData("comí el pan de luz y yo fui al río")]
    [InlineData("mar pescó y luz y yo comimos pescado")]
    public void YThatJoinsTwoClausesLeavesThePronounItsOwnSubject(string sentence)
    {
        Assert.Equal(string.Empty, Described(sentence));
    }

    // The pronoun is still the subject of its own clause there, so its verb is still checked against it alone.
    [Theory]
    [InlineData("luz hizo pan y yo fuimos al río", "conj.person|fuimos|fui")]
    [InlineData("comí pan y tú compré fruta", "conj.person|compré|compraste")]
    [InlineData("el niño come y yo bebe", "conj.person|bebe|bebo")]
    public void APronounAfterAClauseIsCheckedAlone(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    // A pronoun first in the coordination is no subject of the verb (yo y Luz fuimos): the word after it is y. A list said without its commas (Luz, Mar y yo) cannot be
    // told from a name said to the listener and a new clause, so its verb is not checked in any person.
    [Theory]
    [InlineData("yo y luz fuimos al río")]
    [InlineData("luz mar y yo fuimos al río")]
    [InlineData("luz mar y yo fui al río")]
    public void AnUnclearCoordinationIsNotChecked(string sentence)
    {
        Assert.Equal(string.Empty, Described(sentence));
    }

    // ---- M66: ni ... ni and o ----------------------------------------------------------------------------------------------------------------------------------

    // ni ... ni takes the person rule of y (the plural); o takes it too and the form of the member nearest the verb. Before M66 ni tú ni yo fuimos gave conj.person
    // "fui", tú o yo vamos "voy", ni ella ni yo somos altas "soy" (and noun.number "alta"), o tú o yo vamos "voy", tú o ella son "eres" and ni tú ni él son "es"
    // (M64 read the pronoun alone), and o vosotros o nosotras somos guapos gender.agree "guapas" (somos agrees with nosotras and with the plural of both, so the
    // adjective's gender is left open). Noun phrases joined by ni or o were never checked, and are not now (ni luz ni mar fueron, luz o mar va).
    [Theory]
    [InlineData("ni tú ni yo fuimos")]
    [InlineData("ni tú ni yo fuimos al río")]
    [InlineData("ni ella ni yo somos altas")]
    [InlineData("tú o yo vamos")]
    [InlineData("o tú o yo vamos")]
    [InlineData("tú o ella son altas")]
    [InlineData("ni tú ni él son amigos")]
    [InlineData("o vosotros o nosotras somos guapos")]
    public void NiAndOJoinASubjectAsYDoes(string sentence)
    {
        Assert.Equal(string.Empty, Described(sentence));
    }

    // Clean before M66 as well, and still: o with the form of the nearest member, and noun phrases joined by ni or o.
    [Theory]
    [InlineData("tú o yo voy")]
    [InlineData("tú o ella es alta")]
    [InlineData("o vosotros o nosotras somos guapas")]
    [InlineData("ni luz ni mar fueron al río")]
    [InlineData("luz o mar va")]
    [InlineData("luz o mar van")]
    public void OAlsoTakesTheNearestMemberAndNounPhrasesStayUnchecked(string sentence)
    {
        Assert.Equal(string.Empty, Described(sentence));
    }

    // A verb that agrees with no accepted form is flagged with the plural (tú o yo vas gave "voy" before M66; ni tú ni yo fui gave nothing, yo fui agreeing).
    [Theory]
    [InlineData("ni tú ni yo fui", "conj.person|fui|fuimos")]
    [InlineData("tú o yo vas", "conj.person|vas|vamos")]
    [InlineData("ni ella ni yo soy altas", "conj.person|soy|somos")]
    [InlineData("tú o ella es alto", "gender.agree|alto|alta")]
    public void AVerbThatAgreesWithNoFormOfNiOrOIsFlagged(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    // o or ni that joins two clauses leaves the pronoun its own subject, and a coordination that mixes y and o cannot be told.
    [Theory]
    [InlineData("comes pan o yo como fruta")]
    [InlineData("tú y ella o yo vamos")]
    [InlineData("tú y ella o yo voy")]
    public void OAfterAClauseOrMixedWithYIsLeftAsBefore(string sentence)
    {
        Assert.Equal(string.Empty, Described(sentence));
    }
}
