namespace Vocaterra.Nlp.Response.Tests.Templates;

using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Normalization;
using Vocaterra.Nlp.Response.Templates;

/// <summary>
/// M26b (CR-013): a word the learner wrote without an accent that is a lexicon form as spelled (<c>esta</c>) is restored by a recast when the analysis read it as the accented
/// word (<c>está</c>), the same treatment as a word the lexicon lacks as spelled (<c>arbol</c>). A word read as typed is kept (<c>esta espada</c>).
/// </summary>
public class RecastReadAsAccentedTests
{
    private static LexiconEntry Entry(string form, string lemma, string pos, string? person = null, string? number = null, string? tense = null, string? mood = null, string? gender = null)
    {
        return new LexiconEntry(form, lemma, pos, new EntryFeatures(person, number, tense, mood, gender));
    }

    private static readonly Lexicon Words = Lexicon.FromEntries(
    [
        Entry("el", "el", "art", number: "sg", gender: "m"),
        Entry("la", "el", "art", number: "sg", gender: "f"),
        Entry("él", "él", "pron", "3", "sg", gender: "m"),
        Entry("esta", "este", "adj", number: "sg", gender: "f"),
        Entry("está", "estar", "verb", "3", "sg", "pres", "ind"),
        Entry("donde", "donde", "adv"),
        Entry("dónde", "dónde", "adv"),
        Entry("árbol", "árbol", "noun", number: "sg", gender: "m"),
        Entry("espada", "espada", "noun", number: "sg", gender: "f"),
        Entry("aquí", "aquí", "adv"),
        Entry("dos", "2", "num", number: "pl"),
        Entry("espadas", "espada", "noun", number: "pl", gender: "f"),
    ]);

    private static (NormalizedText Text, AnalysisResult Analysis) Analyze(string sentence)
    {
        var text = TextNormalizer.Normalize(sentence, new NormalizationOptions { NumbersToDigits = false, ConvertIndefiniteArticles = false });
        return (text, new LinguisticAnalyzer(Words).Analyze(text));
    }

    private static string Recast(string sentence)
    {
        var (text, analysis) = Analyze(sentence);
        return RecastBuilder.Build(text, analysis, Words);
    }

    [Fact]
    public void EstaReadAsTheVerbGetsItsAccentAndNeverBecomesEste()
    {
        // The analysis finds no gender.agree any more, so nothing turns esta into este; the accent is the only change, and árbol gets its own.
        var (_, analysis) = Analyze("donde esta el arbol");
        Assert.Empty(analysis.Errors);

        Assert.Equal("donde está el árbol", Recast("donde esta el arbol"));
        Assert.Equal("dónde está el árbol", Recast("dónde esta el arbol"));
    }

    [Fact]
    public void EstaReadAsTheDemonstrativeStaysAsTyped()
    {
        Assert.Equal("esta espada", Recast("esta espada"));
    }

    [Fact]
    public void ElReadAsThePronounGetsItsAccent()
    {
        Assert.Equal("él está aquí", Recast("el esta aqui"));
    }

    [Fact]
    public void ElBeforeANounStaysTheArticle()
    {
        Assert.Equal("el árbol", Recast("el arbol"));
    }

    [Fact]
    public void AnAccentedWordIsLeftAlone()
    {
        Assert.Equal("dónde está el árbol", Recast("dónde está el árbol"));
    }

    [Fact]
    public void TheSpellingsAreNamedByTextPosition()
    {
        var (_, analysis) = Analyze("donde esta el arbol");

        var spellings = AccentRestoration.SpellingsFor(Words, analysis);

        var only = Assert.Single(spellings);
        Assert.Equal(6, only.Key);
        Assert.Equal("está", only.Value);
    }

    [Fact]
    public void WithoutALexiconNothingIsRestored()
    {
        var (text, analysis) = Analyze("donde esta el arbol");

        Assert.Equal("donde esta el arbol", RecastBuilder.Build(text, analysis));
    }
}
