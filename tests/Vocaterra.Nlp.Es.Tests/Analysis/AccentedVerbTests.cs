namespace Vocaterra.Nlp.Es.Tests.Analysis;

using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Es.Text;
using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Response.Templates;

/// <summary>
/// M70, the owner's answer of 2026-11-01 (D-game-22, item 3): a verb written without the accent that marks its tense (<c>compre</c> for <c>compré</c>) is no error
/// where the accented form of the same person is the one the sentence needs; the analysis reads it as that form and keeps an <see cref="AccentNote"/> naming it.
/// Before M70 <c>ayer compre pan</c> gave <c>conj.tense</c> with <c>compré</c> as expected. Runs on the bundle's lexicon.
/// </summary>
public class AccentedVerbTests
{
    private static readonly Lexicon Words = TestSupport.Bundle.Lexicon;

    private static readonly LinguisticAnalyzer Analyzer = new(Words, SpanishLanguagePack.Instance);

    private static AnalysisResult Analyze(string sentence)
    {
        return Analyzer.Analyze(TestTokens.From(sentence));
    }

    private static string Errors(AnalysisResult result)
    {
        return string.Join(';', result.Errors.Select(static e => $"{e.Tag}|{e.Actual}|{e.Expected}"));
    }

    private static string Notes(AnalysisResult result)
    {
        return string.Join(';', (result.Notes ?? []).Select(static n => $"{n.Written}|{n.Accented}|{n.Marks}|{n.Reading.Tense}"));
    }

    [Theory]
    [InlineData("ayer compre pan", "compre|compré|Tense|pret")]
    [InlineData("ayer compre tres manzanas", "compre|compré|Tense|pret")]
    [InlineData("ayer hable con luz", "hable|hablé|Tense|pret")]
    [InlineData("ayer trabaje en el campo", "trabaje|trabajé|Tense|pret")]
    [InlineData("ayer yo compre pan", "compre|compré|Tense|pret")]
    public void AVerbWithoutTheAccentOfItsTenseIsReadAsTheAccentedFormWithANote(string sentence, string notes)
    {
        var result = Analyze(sentence);

        Assert.Equal(string.Empty, Errors(result));
        Assert.Equal(notes, Notes(result));
        var note = Assert.Single(result.Notes!);
        Assert.Equal("pret", result.Words!.Single(w => w.Token.Span == note.Span).Reading!.Tense);
    }

    // The neighbours: a person error stays one (the accented form would change whom the sentence is about), a tense error whose accented form is another person stays
    // one, and correct Spanish gets no note.
    [Theory]
    [InlineData("ayer compro pan", "conj.tense|compro|compré")]
    [InlineData("ella compro pan", "conj.person|compro|compra")]
    [InlineData("usted compro", "conj.person|compro|compra")]
    [InlineData("tú compro pan", "conj.person|compro|compras")]
    [InlineData("cómo se llamo", "conj.person|llamo|llama")]
    [InlineData("ayer como pan", "conj.tense|como|comí")]
    [InlineData("yo compro pan", "")]
    [InlineData("quiero que compre pan", "")]
    [InlineData("compre tres manzanas", "")]
    [InlineData("ayer compré pan", "")]
    [InlineData("ayer comi pan", "")]
    [InlineData("dónde esta el río", "")]
    public void OtherwiseTheWordIsGradedAsBeforeAndHasNoNote(string sentence, string errors)
    {
        var result = Analyze(sentence);

        Assert.Equal(errors, Errors(result));
        Assert.Null(result.Notes);
    }

    [Fact]
    public void ARecastMadeForAnotherMistakeWritesTheAccent()
    {
        var text = TestTokens.From("ayer compre tres manzana");
        var result = Analyzer.Analyze(text);

        Assert.Equal("noun.number|manzana|manzanas", Errors(result));
        Assert.Equal("ayer compré tres manzanas", RecastBuilder.Build(text, result, Words, SpanishLanguagePack.Instance));
    }

    [Theory]
    [InlineData("compré", "comprar", "1", "sg", "pret", "ind", AccentMark.Tense, "«compré», con tilde: es pasado")]
    [InlineData("comprará", "comprar", "3", "sg", "fut", "ind", AccentMark.Tense, "«comprará», con tilde: es futuro")]
    [InlineData("hablé", "hablar", "1", "sg", "pret", "ind", AccentMark.Person, "«hablé», con tilde: es de yo")]
    [InlineData("habló", "hablar", "3", "sg", "pret", "ind", AccentMark.Person, "«habló», con tilde: es de él o ella")]
    public void TheSpanishNoteNamesTheFormAndWhatTheAccentMarks(string accented, string lemma, string person, string number, string tense, string mood, AccentMark marks, string expected)
    {
        var note = new AccentNote(new TextSpan(0, 1), "x", accented, marks, new LexicalFeatures(lemma, "verb", person, number, tense, mood, null));

        Assert.Equal(expected, SpanishAccentNotes.Describe(note));
    }
}
