namespace Vocaterra.Nlp.Language.Tests.Analysis;

using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Lexicon;

/// <summary>
/// M49: tense checks for the near future (conj.tense). A future-time marker with a preterite or imperfect form expects the ir a + infinitive form; a conjugated
/// verb after "ir a" expects the infinitive; "ayer" with a simple future expects the preterite. Runs on the lexicon of the bundle.
/// </summary>
public class NearFutureTenseTests
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

        return new LinguisticAnalyzer(Lexicon.FromEntries(entries));
    }

    private static string[] Described(string sentence)
    {
        return Analyzer.Value.Analyze(TestTokens.From(sentence), []).Errors
            .Select(static e => $"{e.Tag}|{e.Actual}|{e.Expected}")
            .ToArray();
    }

    [Theory]
    [InlineData("mañana comí pan", "conj.tense|comí|voy a comer")]
    [InlineData("mañana yo comí pan", "conj.tense|comí|voy a comer")]
    [InlineData("mañana comimos pan", "conj.tense|comimos|vamos a comer")]
    [InlineData("mañana compraste pan", "conj.tense|compraste|vas a comprar")]
    [InlineData("pasado mañana comió pan", "conj.tense|comió|va a comer")]
    [InlineData("la próxima semana compré pan", "conj.tense|compré|voy a comprar")]
    [InlineData("el próximo año tuve pan", "conj.tense|tuve|voy a tener")]
    [InlineData("la semana que viene comí pan", "conj.tense|comí|voy a comer")]
    [InlineData("mañana comía pan", "conj.tense|comía|voy a comer")]
    [InlineData("mañana fui a la tienda", "conj.tense|fui|voy")]
    public void AFutureMarkerWithAPastFormExpectsIrAInfinitive(string sentence, string expected)
    {
        Assert.Equal([expected], Described(sentence));
    }

    [Theory]
    [InlineData("voy a como", "conj.tense|como|comer")]
    [InlineData("vamos a tenemos", "conj.tense|tenemos|tener")]
    [InlineData("voy a compro pan", "conj.tense|compro|comprar")]
    [InlineData("mañana voy a comí", "conj.tense|comí|comer")]
    public void AConjugatedVerbAfterIrAExpectsTheInfinitive(string sentence, string expected)
    {
        Assert.Equal([expected], Described(sentence));
    }

    // R02, found by the fuzz suite on the game's bundle ("va a vais a culpa ayer por favor"): a past-time marker and a finite verb after "ir a" were both
    // reported for the same word, two conj.tense errors with one span. The verb after "ir a" is now reported once, with the infinitive as the expected form.
    [Theory]
    [InlineData("ayer voy a como", "conj.tense|como|comer")]
    [InlineData("voy a como ayer", "conj.tense|como|comer")]
    [InlineData("ayer vamos a tenemos pan", "conj.tense|tenemos|tener")]
    public void AVerbAfterIrAWithAPastMarkerIsReportedOnce(string sentence, string expected)
    {
        Assert.Equal([expected], Described(sentence));
    }

    [Fact]
    public void NoTwoErrorsShareATagAndASpanWhenAPastMarkerMeetsIrAAndAFiniteVerb()
    {
        var errors = Analyzer.Value.Analyze(TestTokens.From("va a vais a culpa ayer por favor"), []).Errors;

        Assert.Equal(errors.Count, errors.Select(static e => (e.Tag, e.Span)).Distinct().Count());
    }

    [Theory]
    [InlineData("ayer comeré pan", "conj.tense|comeré|comí")]
    [InlineData("ayer canto", "conj.tense|canto|canté")]
    public void AYesterdayMarkerWithAFutureOrPresentExpectsThePreterite(string sentence, string expected)
    {
        Assert.Equal([expected], Described(sentence));
    }

    [Theory]
    [InlineData("quiero comer")]
    [InlineData("voy a la casa")]
    [InlineData("voy a comer")]
    [InlineData("mañana voy a comer")]
    [InlineData("voy a comer mañana")]
    [InlineData("ayer comí")]
    [InlineData("hoy como pan")]
    [InlineData("vamos a la tienda")]
    [InlineData("necesito agua mañana")]
    [InlineData("mañana como pan")]
    [InlineData("mañana comeré pan")]
    [InlineData("la próxima semana como pan")]
    [InlineData("luego comí pan")]
    [InlineData("más tarde comí pan")]
    [InlineData("esta mañana comí pan")]
    [InlineData("ayer por la mañana comí pan")]
    [InlineData("vas a comer mañana")]
    [InlineData("qué vas a comprar mañana")]
    [InlineData("comes pan mañana")]
    [InlineData("vamos a ver")]
    [InlineData("mañana vamos a la tienda")]
    [InlineData("ayer fui a la tienda")]
    [InlineData("dijo que mañana venía")]
    public void WhatLearnersAndTheGameSayIsClean(string sentence)
    {
        Assert.Empty(Described(sentence));
    }

    [Fact]
    public void ProximoBeforeATimeNounIsNotAWordOrderMistake()
    {
        Assert.Equal(["conj.tense|compré|voy a comprar"], Described("la próxima semana compré pan"));
        Assert.Empty(Described("el próximo año"));
        Assert.Empty(Described("el próximo verano"));
    }

    [Fact]
    public void ProximoBeforeAnOtherNounIsStillAWordOrderMistake()
    {
        Assert.Contains("word_order|próximos salones|salones próximos", Described("unos seis próximos salones"));
    }

    [Theory]
    [InlineData("voy comer pan", "conj.tense|voy comer|voy a comer")]
    [InlineData("vamos comer fruta", "conj.tense|vamos comer|vamos a comer")]
    [InlineData("van tener agua", "conj.tense|van tener|van a tener")]
    [InlineData("yo voy comprar pan", "conj.tense|voy comprar|voy a comprar")]
    [InlineData("vas beber agua", "conj.tense|vas beber|vas a beber")]
    [InlineData("mañana voy comer", "conj.tense|voy comer|voy a comer")]
    public void IrWithABareInfinitiveExpectsIrAInfinitive(string sentence, string expected)
    {
        Assert.Equal([expected], Described(sentence));
    }

    [Theory]
    [InlineData("voy a comer")]
    [InlineData("quiero comer")]
    [InlineData("necesito comer")]
    [InlineData("vamos a la tienda")]
    [InlineData("voy al río")]
    [InlineData("ir a comer")]
    [InlineData("fui comer")]
    [InlineData("voy contento")]
    [InlineData("vas a comer")]
    [InlineData("vamos")]
    [InlineData("voy")]
    public void IrCasesThatAreNotAMissingAStayClean(string sentence)
    {
        Assert.Empty(Described(sentence));
    }

    [Fact]
    public void AGerundAfterIrIsNotAMissingA()
    {
        // The lexicon has no gerunds, so "corriendo" is vocab.unknown as it always was; the point is that no tense error is added.
        Assert.DoesNotContain(Described("voy corriendo"), static e => e.StartsWith("conj.tense", StringComparison.Ordinal));
    }

    [Fact]
    public void ABareInfinitiveAfterIrKeepsTheFullSentenceIntact()
    {
        var result = Analyzer.Value.Analyze(TestTokens.From("voy comer pan"), []);
        var error = Assert.Single(result.Errors);
        Assert.Equal("conj.tense", error.Tag);
        Assert.Equal(0, error.Span.Start);
        Assert.Equal(9, error.Span.Length);
    }
}
