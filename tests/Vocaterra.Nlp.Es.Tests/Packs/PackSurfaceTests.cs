using Vocaterra.Nlp.Language.Morphology;

namespace Vocaterra.Nlp.Es.Tests.Packs;

/// <summary>M87: what a host reads from the Spanish pack equals what it read from the Spanish classes before.</summary>
public class PackSurfaceTests
{
    private static readonly SpanishLanguagePack Pack = SpanishLanguagePack.Instance;

    [Theory]
    [InlineData("compré", "comprar", "1", "sg", "pret", "ind", AccentMark.Tense)]
    [InlineData("comprará", "comprar", "3", "sg", "fut", "ind", AccentMark.Tense)]
    [InlineData("hablé", "hablar", "1", "sg", "pret", "ind", AccentMark.Person)]
    [InlineData("hablaste", "hablar", "2", "sg", "pret", "ind", AccentMark.Person)]
    [InlineData("hablamos", "hablar", "1", "pl", "pret", "ind", AccentMark.Person)]
    [InlineData("hablen", "hablar", "3", "pl", "pres", "sub", AccentMark.Tense)]
    [InlineData("hable", "hablar", "3", "sg", "pres", "imp", AccentMark.Tense)]
    [InlineData("compraría", "comprar", "3", "pl", "cond", "ind", AccentMark.Tense)]
    public void TheOrthographyNotesAreTheSpanishAccentNotes(string accented, string lemma, string person, string number, string tense, string mood, AccentMark marks)
    {
        var note = new AccentNote(new TextSpan(0, 1), "x", accented, marks, new LexicalFeatures(lemma, "verb", person, number, tense, mood, null));

        Assert.Equal(SpanishAccentNotes.Describe(note), Pack.Orthography.Describe(note));
        Assert.Equal(SpanishAccentNotes.Describe(note), new SpanishOrthographyNotes().Describe(note));
    }

    [Fact]
    public void TheSpanishParadigmIsTheFiveRowsAndThreeTensesTheBookShows()
    {
        var morphology = Pack.CreateMorphology(SpanishLexicon.Seed);

        Assert.Equal(
            [("1", "sg"), ("2", "sg"), ("3", "sg"), ("1", "pl"), ("3", "pl")],
            morphology.Paradigm.Persons.Select(static p => (p.Person, p.Number)));
        Assert.Equal(["pres", "pret", "fut"], morphology.Paradigm.Tenses);
    }

    [Fact]
    public void TheParadigmCellsAreWhatConjugateGivesForEveryPersonAndTense()
    {
        var morphology = Pack.CreateMorphology(SpanishLexicon.Seed);
        var cells = morphology.ParadigmOf("comer");

        Assert.Equal(15, cells.Count);
        foreach (var cell in cells)
        {
            Assert.Equal(morphology.Conjugate("comer", cell.Person.Person, cell.Person.Number, cell.Tense), cell.Form);
        }

        Assert.Equal(["como", "comes", "come", "comemos", "comen"], cells.Take(5).Select(static c => c.Form));
        Assert.Equal(["comí", "comiste", "comió", "comimos", "comieron"], cells.Skip(5).Take(5).Select(static c => c.Form));
    }

    [Fact]
    public void TheConcreteGeneratorAndTheInterfaceAgree()
    {
        IMorphology morphology = new MorphologyGenerator(SpanishLexicon.Seed);

        Assert.Same(((MorphologyGenerator)morphology).Paradigm, morphology.Paradigm);
        Assert.Equal("las", morphology.Article(Definiteness.Definite, "f", "pl", "manzana"));
        Assert.Equal("manzanas", morphology.Pluralize("manzana"));
    }
}
