namespace Vocaterra.Nlp.Es.Tests.Analysis;

using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.SlotGrammar;

public class LinguisticAnalyzerTests
{
    private static ErrorTag Single(string text, string tag)
    {
        var errors = Fixtures.Errors(text);
        return Assert.Single(errors, e => e.Tag == tag);
    }

    private static void AssertNo(string text, string tag)
    {
        var errors = Fixtures.Errors(text);
        Assert.True(errors.All(e => e.Tag != tag), $"'{text}' unexpectedly produced {Fixtures.Describe(errors)}");
    }

    private static void AssertClean(string text)
    {
        var errors = Fixtures.Errors(text);
        Assert.True(errors.Count == 0, $"'{text}' unexpectedly produced {Fixtures.Describe(errors)}");
    }

    [Fact]
    public void WorkedExampleProducesExactlyPersonAndNumberErrors()
    {
        var result = Fixtures.Analyze("yo tiene dos espada", "[PRON]? [VERB:tener] [NUM:qty] [NOUN:item]");

        Assert.Equal(
            [
                new ErrorTag(ErrorTags.ConjPerson, new TextSpan(3, 5), "tengo", "tiene"),
                new ErrorTag(ErrorTags.NounNumber, new TextSpan(13, 6), "espadas", "espada"),
            ],
            result.Errors);
        Assert.Equal(
            [new SlotValue("qty", "2", new TextSpan(9, 3)), new SlotValue("item", "espada", new TextSpan(13, 6))],
            result.Slots);
        Assert.Equal("[PRON]? [VERB:tener] [NUM:qty] [NOUN:item]", result.MatchedGrammarId);
        Assert.Equal(1.0, result.Confidence);
        Assert.Equal(4, result.Tokens.Count);
    }

    [Fact]
    public void WorkedExampleCorrectedHasNoErrors()
    {
        var result = Fixtures.Analyze("tú tienes dos espadas", "[PRON]? [VERB:tener] [NUM:qty] [NOUN:item]");

        Assert.Empty(result.Errors);
        Assert.Equal("qty=2,item=espada", string.Join(",", result.Slots.Select(s => $"{s.Name}={s.Value}")));
    }

    [Fact]
    public void TokensCarryAllReadings()
    {
        var result = Fixtures.Analyze("el vino");

        Assert.Equal(["el"], result.Tokens[0].Readings.Select(r => r.Lemma));
        Assert.Contains(result.Tokens[1].Readings, r => r.Pos == "noun");
        Assert.Contains(result.Tokens[1].Readings, r => r is { Pos: "verb", Lemma: "venir" });
    }

    // vocab.unknown
    [Fact]
    public void UnknownWordIsTaggedWithSpanAndEmptyExpectation()
    {
        var result = Fixtures.Analyze("tengo una espadass");

        var error = Assert.Single(result.Errors);
        Assert.Equal(new ErrorTag(ErrorTags.VocabUnknown, new TextSpan(10, 8), string.Empty, "espadass"), error);
        Assert.Equal(0.5, result.Confidence);
        Assert.Empty(result.Tokens[2].Readings);
    }

    [Fact]
    public void KnownWordsAreNotUnknown()
    {
        var result = Fixtures.Analyze("tengo una espada");

        Assert.Empty(result.Errors);
        Assert.Equal(1.0, result.Confidence);
    }

    [Fact]
    public void DigitTokensAreKnownNumerals()
    {
        var result = Fixtures.Analyze("tengo 250 monedas");

        Assert.Empty(result.Errors);
        Assert.Equal(1.0, result.Confidence);
        Assert.Equal("250", result.Tokens[1].Readings[0].Lemma);
        AssertNo("tengo 1 monedas", ErrorTags.VocabUnknown);
        Assert.Equal("moneda", Single("tengo 1 monedas", ErrorTags.NounNumber).Expected);
    }

    [Fact]
    public void AccentlessSpellingIsStillKnown()
    {
        var result = Fixtures.Analyze("yo estoy aqui adios");

        Assert.Empty(result.Errors);
        Assert.Equal(1.0, result.Confidence);
    }

    // conj.person
    [Theory]
    [InlineData("yo tiene", "tengo")]
    [InlineData("tú tengo", "tienes")]
    [InlineData("él tienes", "tiene")]
    [InlineData("nosotros tiene", "tenemos")]
    [InlineData("ellos come", "comen")]
    [InlineData("ella compran", "compra")]
    [InlineData("usted compro", "compra")]
    [InlineData("ustedes compra", "compran")]
    [InlineData("yo fue", "fui")]
    [InlineData("yo es", "soy")]
    [InlineData("yo no tiene", "tengo")]
    public void SubjectVerbMismatchIsTaggedWithTheConjugatedForm(string text, string expected)
    {
        var error = Single(text, ErrorTags.ConjPerson);

        Assert.Equal(expected, error.Expected);
    }

    [Theory]
    [InlineData("yo tengo")]
    [InlineData("tú tienes")]
    [InlineData("usted compra")]
    [InlineData("nosotros compramos")]
    [InlineData("ustedes compran")]
    [InlineData("yo no tengo")]
    [InlineData("yo me llamo")]
    [InlineData("ella es")]
    [InlineData("yo fui")]
    [InlineData("compro una espada")]
    [InlineData("tiene una espada")]
    public void MatchingOrImplicitSubjectsAreNotFlagged(string text)
    {
        AssertNo(text, ErrorTags.ConjPerson);
    }

    [Fact]
    public void BareInfinitiveAfterSubjectPronounIsAPersonError()
    {
        var error = Single("yo comprar una espada", ErrorTags.ConjPerson);

        Assert.Equal("compro", error.Expected);
        Assert.Equal("comprar", error.Actual);
        AssertNo("yo quiero comprar una espada", ErrorTags.ConjPerson);
    }

    [Fact]
    public void PersonErrorKeepsThePreteriteTense()
    {
        Assert.Equal("compré", Single("yo compró una espada", ErrorTags.ConjPerson).Expected);
    }

    // conj.tense
    [Theory]
    [InlineData("ayer compro una espada", "compré")]
    [InlineData("anoche como pan", "comí")]
    [InlineData("la semana pasada vendo una espada", "vendí")]
    [InlineData("el año pasado tengo un perro", "tuve")]
    [InlineData("ayer yo voy a la tienda", "fui")]
    public void PresentVerbWithPastTimeMarkerIsATenseError(string text, string expected)
    {
        var error = Single(text, ErrorTags.ConjTense);

        Assert.Equal(expected, error.Expected);
    }

    [Theory]
    [InlineData("ayer compré una espada")]
    [InlineData("anoche comí pan")]
    [InlineData("compro una espada")]
    [InlineData("hoy compro una espada")]
    [InlineData("la semana pasada compré una espada")]
    [InlineData("la espada pasada es roja y compro")]
    public void PastVerbsOrPresentWithoutMarkerAreNotTenseErrors(string text)
    {
        AssertNo(text, ErrorTags.ConjTense);
    }

    // conj.irregular
    [Fact]
    public void RegularizedIrregularVerbIsTaggedWithTheExpectedForm()
    {
        var result = Fixtures.Analyze("yo sabo", "[PRON]? [VERB:saber]");

        var error = Assert.Single(result.Errors);
        Assert.Equal(new ErrorTag(ErrorTags.ConjIrregular, new TextSpan(3, 4), "sé", "sabo"), error);
        Assert.Equal(1.0, result.Confidence);
        Assert.Equal("saber", result.Tokens[1].Readings[0].Lemma);
        Assert.Equal("[PRON]? [VERB:saber]", result.MatchedGrammarId);
    }

    [Theory]
    [InlineData("ayer yo tení una espada", "tuve")]
    [InlineData("yo hací una espada", "hice")]
    public void RegularizedPreteritesAreTagged(string text, string expected)
    {
        Assert.Equal(expected, Single(text, ErrorTags.ConjIrregular).Expected);
    }

    [Theory]
    [InlineData("yo sé")]
    [InlineData("yo tuve una espada")]
    [InlineData("yo compro una espada")]
    public void CorrectIrregularFormsAreNotTagged(string text)
    {
        AssertNo(text, ErrorTags.ConjIrregular);
    }

    [Fact]
    public void RegularizedIrregularAlsoParticipatesInSubjectChecks()
    {
        var errors = Fixtures.Errors("tú sabo");

        Assert.Equal([ErrorTags.ConjPerson, ErrorTags.ConjIrregular], errors.OrderBy(e => e.Tag == ErrorTags.ConjPerson ? 0 : 1).Select(e => e.Tag));
        Assert.Equal("sabes", errors.Single(e => e.Tag == ErrorTags.ConjPerson).Expected);
        Assert.Equal("sé", errors.Single(e => e.Tag == ErrorTags.ConjIrregular).Expected);
    }

    // gender.agree
    [Theory]
    [InlineData("el mesa", "la")]
    [InlineData("la escudo", "el")]
    [InlineData("un espada", "una")]
    [InlineData("una escudo", "un")]
    [InlineData("los espadas", "las")]
    [InlineData("unas escudos", "unos")]
    [InlineData("el espada roja", "la")]
    public void ArticleGenderMismatchGetsTheAgreeingArticle(string text, string expected)
    {
        Assert.Equal(expected, Single(text, ErrorTags.GenderAgree).Expected);
    }

    [Theory]
    [InlineData("la mesa")]
    [InlineData("el escudo")]
    [InlineData("las espadas")]
    [InlineData("unos escudos")]
    [InlineData("el agua")]
    [InlineData("la agua")]
    [InlineData("un arma")]
    [InlineData("una arma")]
    [InlineData("el comerciante")]
    [InlineData("la comerciante")]
    [InlineData("dos espadas")]
    public void AgreeingArticlesAreNotFlagged(string text)
    {
        AssertNo(text, ErrorTags.GenderAgree);
    }

    [Fact]
    public void FeminineNounsWithStressedAStillNeedFemininePlurals()
    {
        Assert.Equal("las", Single("los armas", ErrorTags.GenderAgree).Expected);
    }

    [Theory]
    [InlineData("la espada rojo", "roja")]
    [InlineData("el escudo roja", "rojo")]
    [InlineData("una espada nuevo", "nueva")]
    [InlineData("las mesas cansado", "cansadas")]
    [InlineData("buenas días", "buenos")]
    [InlineData("buen espada", "buena")]
    public void AdjectiveGenderMismatchGetsTheAgreeingAdjective(string text, string expected)
    {
        var error = Single(text, ErrorTags.GenderAgree);

        Assert.Equal(expected, error.Expected);
    }

    [Theory]
    [InlineData("la espada roja")]
    [InlineData("el escudo rojo")]
    [InlineData("la espada grande")]
    [InlineData("el escudo grande")]
    [InlineData("buenos días")]
    [InlineData("una gran espada")]
    [InlineData("mucha agua")]
    [InlineData("mi espada")]
    [InlineData("este escudo")]
    [InlineData("esta espada")]
    public void AgreeingAdjectivesAreNotFlagged(string text)
    {
        AssertClean(text);
    }

    [Fact]
    public void DeterminerAdjectivesAgreeInGender()
    {
        Assert.Equal("mucha", Single("mucho espada", ErrorTags.GenderAgree).Expected);
        Assert.Equal("esta", Single("este espada", ErrorTags.GenderAgree).Expected);
        Assert.Equal("nuestra", Single("nuestro espada", ErrorTags.GenderAgree).Expected);
    }

    // noun.number
    [Theory]
    [InlineData("dos espada", "espadas")]
    [InlineData("tres escudo", "escudos")]
    [InlineData("quince poción", "pociones")]
    [InlineData("2 espada", "espadas")]
    [InlineData("los escudo", "escudos")]
    [InlineData("las espada", "espadas")]
    [InlineData("unos escudo", "escudos")]
    [InlineData("muchos dragón", "dragones")]
    [InlineData("mis espada", "espadas")]
    [InlineData("un escudos", "escudo")]
    [InlineData("un espadas", "espada")]
    [InlineData("el escudos", "escudo")]
    [InlineData("la espadas", "espada")]
    public void DeterminerNumberMismatchGetsTheNounInThatNumber(string text, string expected)
    {
        Assert.Equal(expected, Single(text, ErrorTags.NounNumber).Expected);
    }

    [Theory]
    [InlineData("dos espadas")]
    [InlineData("una espada")]
    [InlineData("un escudo")]
    [InlineData("el escudo")]
    [InlineData("los escudos")]
    [InlineData("quince pociones")]
    [InlineData("el lunes")]
    [InlineData("los lunes")]
    [InlineData("muchos dragones")]
    [InlineData("espada")]
    public void MatchingNumbersAreNotFlagged(string text)
    {
        AssertNo(text, ErrorTags.NounNumber);
    }

    [Theory]
    [InlineData("dos espadas roja", "rojas")]
    [InlineData("las espadas roja", "rojas")]
    [InlineData("la espada rojas", "roja")]
    [InlineData("el escudo rojos", "rojo")]
    public void AdjectiveNumberMismatchGetsTheAgreeingAdjective(string text, string expected)
    {
        var error = Single(text, ErrorTags.NounNumber);

        Assert.Equal(expected, error.Expected);
    }

    [Fact]
    public void AdjectiveFollowsTheNumberTheDeterminerImposes()
    {
        var errors = Fixtures.Errors("dos espada roja");

        Assert.Equal(
            [
                new ErrorTag(ErrorTags.NounNumber, new TextSpan(4, 6), "espadas", "espada"),
                new ErrorTag(ErrorTags.NounNumber, new TextSpan(11, 4), "rojas", "roja"),
            ],
            errors);
    }

    // ser_estar
    [Theory]
    [InlineData("soy cansado", "estoy")]
    [InlineData("yo soy enfermo", "estoy")]
    [InlineData("eres contento", "estás")]
    [InlineData("ella es listo", "está")]
    [InlineData("la espada es abierta", "está")]
    [InlineData("estoy alto", "soy")]
    [InlineData("estás inteligente", "eres")]
    [InlineData("él está simpático", "es")]
    [InlineData("yo soy muy cansado", "estoy")]
    [InlineData("fui cansado", "estuve")]
    public void WrongCopulaIsTaggedWithTheRightOne(string text, string expected)
    {
        Assert.Equal(expected, Single(text, ErrorTags.SerEstar).Expected);
    }

    [Theory]
    [InlineData("estoy cansado")]
    [InlineData("yo estoy enfermo")]
    [InlineData("ella está contenta")]
    [InlineData("soy alto")]
    [InlineData("eres inteligente")]
    [InlineData("es simpático")]
    [InlineData("soy español")]
    [InlineData("la espada es roja")]
    [InlineData("estoy muy cansado")]
    [InlineData("soy el herrero")]
    public void CorrectCopulaChoiceIsNotFlagged(string text)
    {
        AssertNo(text, ErrorTags.SerEstar);
    }

    [Fact]
    public void SerEstarErrorSpansTheVerbOnly()
    {
        var error = Single("yo soy cansado", ErrorTags.SerEstar);

        Assert.Equal(new ErrorTag(ErrorTags.SerEstar, new TextSpan(3, 3), "estoy", "soy"), error);
    }

    // word_order
    [Fact]
    public void PostNominalAdjectiveBeforeTheNounIsAWordOrderError()
    {
        var error = Single("una roja espada", ErrorTags.WordOrder);

        Assert.Equal(new ErrorTag(ErrorTags.WordOrder, new TextSpan(4, 11), "espada roja", "roja espada"), error);
        Assert.Equal("espada nueva", Single("tengo nueva espada", ErrorTags.WordOrder).Expected);
        Assert.Equal("escudo viejo", Single("un viejo escudo", ErrorTags.WordOrder).Expected);
    }

    [Theory]
    [InlineData("una espada roja")]
    [InlineData("una gran espada")]
    [InlineData("un buen escudo")]
    [InlineData("un mal día")]
    [InlineData("mucho oro")]
    [InlineData("poca agua")]
    [InlineData("otra espada")]
    [InlineData("buenos días")]
    [InlineData("tengo una espada")]
    public void NormalOrderIsNotAWordOrderError(string text)
    {
        AssertNo(text, ErrorTags.WordOrder);
    }

    [Fact]
    public void WordOrderDoesNotHideAgreementErrors()
    {
        var errors = Fixtures.Errors("una roja escudo");

        Assert.Contains(errors, e => e.Tag == ErrorTags.WordOrder);
        Assert.Contains(errors, e => e.Tag == ErrorTags.GenderAgree && e.Expected == "un");
        Assert.Contains(errors, e => e.Tag == ErrorTags.GenderAgree && e.Expected == "rojo");
    }

    // ordering, confidence, grammars
    [Fact]
    public void ErrorsAreOrderedBySpanStartThenTaxonomyOrder()
    {
        var errors = Fixtures.Errors("ayer soy cansado una roja escudos");

        for (var i = 1; i < errors.Count; i++)
        {
            var previous = errors[i - 1];
            var current = errors[i];
            Assert.True(previous.Span.Start <= current.Span.Start, Fixtures.Describe(errors));
            if (previous.Span.Start == current.Span.Start)
            {
                Assert.True(
                    ErrorTags.All.ToList().IndexOf(previous.Tag) <= ErrorTags.All.ToList().IndexOf(current.Tag),
                    Fixtures.Describe(errors));
            }
        }

        var sameSpan = errors.Where(e => e.Span.Start == 5).Select(e => e.Tag).ToArray();
        Assert.Equal([ErrorTags.ConjTense, ErrorTags.SerEstar], sameSpan);
    }

    [Fact]
    public void EmptyTextHasNoTokensErrorsOrSlots()
    {
        var result = Fixtures.Analyze(string.Empty, "tengo [NUM:qty] [NOUN:item]");

        Assert.Empty(result.Tokens);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Slots);
        Assert.Null(result.MatchedGrammarId);
    }

    [Fact]
    public void FirstBestGrammarIsReported()
    {
        var result = Fixtures.Analyze(
            "quiero comprar una espada",
            "tengo [NUM:qty] [NOUN:item]",
            "querer comprar [ART]? [NOUN:item]",
            "[VERB:querer] [VERB:comprar] [ART]? [NOUN:item]");

        Assert.Equal("querer comprar [ART]? [NOUN:item]", result.MatchedGrammarId);
        Assert.Equal("item", Assert.Single(result.Slots).Name);
    }

    [Fact]
    public void NoMatchingGrammarLeavesSlotsEmpty()
    {
        var result = Fixtures.Analyze("hola gracias", "tengo [NUM:qty] [NOUN:item]");

        Assert.Null(result.MatchedGrammarId);
        Assert.Empty(result.Slots);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void GrammarMatchingToleratesUnrelatedErrors()
    {
        var result = Fixtures.Analyze("ayer yo sabo dos espada", "ayer [PRON]? [VERB:saber] [NUM:qty] [NOUN:item]");

        Assert.Equal("ayer [PRON]? [VERB:saber] [NUM:qty] [NOUN:item]", result.MatchedGrammarId);
        Assert.Equal("qty=2,item=espada", string.Join(",", result.Slots.Select(s => $"{s.Name}={s.Value}")));
        Assert.Equal([ErrorTags.ConjTense, ErrorTags.ConjIrregular, ErrorTags.NounNumber], result.Errors.Select(e => e.Tag));
    }

    [Fact]
    public void ImperativesWithClitics()
    {
        var result = Fixtures.Analyze("dame la llave por favor", "[VERB:dar] [ART]? [NOUN:item]");

        Assert.Empty(result.Errors);
        Assert.Equal("item=llave", string.Join(",", result.Slots.Select(s => $"{s.Name}={s.Value}")));
    }

    [Theory]
    [InlineData("todos los días")]
    [InlineData("tengo veintiuna monedas")]
    [InlineData("tengo 21 monedas")]
    [InlineData("el agua es fría")]
    [InlineData("las espadas rojas y los escudos azules")]
    [InlineData("tengo una espada y un escudo")]
    [InlineData("dos buenas espadas")]
    [InlineData("buenas noches")]
    [InlineData("muchas gracias")]
    [InlineData("me gusta el queso")]
    public void CommonBeginnerPhrasesAreClean(string text)
    {
        AssertClean(text);
    }

    [Fact]
    public void AccentedRegularizedFormIsStillDetected()
    {
        Assert.Equal("estuvo", Single("ella estó en casa", ErrorTags.ConjIrregular).Expected);
    }

    [Fact]
    public void AnalysisIsDeterministicAcrossAnalyzerInstances()
    {
        var grammars = new[] { SlotGrammar.Parse("[PRON]? [VERB:tener] [NUM:qty] [NOUN:item]") };
        var text = TestTokens.From("ayer yo tiene dos espada roja y soy cansado");

        var first = new LinguisticAnalyzer(SpanishLexicon.LoadSeed(), SpanishLanguagePack.Instance).Analyze(text, grammars);
        var second = new LinguisticAnalyzer(SpanishLexicon.LoadSeed(), SpanishLanguagePack.Instance).Analyze(text, grammars);

        Assert.Equal(first, second);
        Assert.Equal(CanonicalJson.Serialize(first), CanonicalJson.Serialize(second));
        Assert.Equal(CanonicalJson.Hash(first), CanonicalJson.Hash(second));
    }

    [Fact]
    public void WorkedExampleCanonicalJsonIsPinned()
    {
        var result = Fixtures.Analyze("yo tiene dos espada", "[PRON]? [VERB:tener] [NUM:qty] [NOUN:item]");

        var json = CanonicalJson.Serialize(new { result.Errors, result.Slots, result.MatchedGrammarId, result.Confidence });

        Assert.Equal(
            "{\"errors\":[{\"tag\":\"conj.person\",\"span\":{\"start\":3,\"length\":5},\"expected\":\"tengo\",\"actual\":\"tiene\"},"
            + "{\"tag\":\"noun.number\",\"span\":{\"start\":13,\"length\":6},\"expected\":\"espadas\",\"actual\":\"espada\"}],"
            + "\"slots\":[{\"name\":\"qty\",\"value\":\"2\",\"span\":{\"start\":9,\"length\":3}},"
            + "{\"name\":\"item\",\"value\":\"espada\",\"span\":{\"start\":13,\"length\":6}}],"
            + "\"matchedGrammarId\":\"[PRON]? [VERB:tener] [NUM:qty] [NOUN:item]\",\"confidence\":1}",
            json);
    }

    [Fact]
    public void AnalysisResultRoundTripsThroughCanonicalJson()
    {
        var result = Fixtures.Analyze("yo tiene dos espada", "[PRON]? [VERB:tener] [NUM:qty] [NOUN:item]");

        var json = CanonicalJson.Serialize(result);

        Assert.Equal(result, CanonicalJson.Deserialize<AnalysisResult>(json));
    }
}
