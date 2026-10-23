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
}
