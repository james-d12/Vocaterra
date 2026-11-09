using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Language.Morphology;

namespace Vocaterra.Nlp.Fuzzing.Tests.Languages.Xx;

/// <summary>M87: the surface a host reads from a pack works through the toy pack, with no Spanish registered.</summary>
public sealed class XxPackSurfaceTests
{
    [Fact]
    public void TheToyPackWordsAnOrthographyNoteWithoutAnySpanish()
    {
        var bundle = XxBundle.LoadInTemp();
        var note = new AccentNote(new TextSpan(0, 2), "ko", "kó", AccentMark.Tense, new LexicalFeatures("ko", "verb", "1", "sg", "pres", "ind", null));

        Assert.Equal("kó", bundle.Language.Orthography.Describe(note));
        Assert.False(XxBundle.Registry.TryResolve("es", out _));
    }

    [Fact]
    public void TheToyMorphologyHasTheDefaultParadigmLayoutAndNoGeneratedForms()
    {
        var bundle = XxBundle.LoadInTemp();
        var morphology = bundle.LanguageMorphology;

        Assert.Same(ParadigmLayout.Default, morphology.Paradigm);
        Assert.Equal(6, morphology.Paradigm.Persons.Count);
        Assert.Equal(["pres"], morphology.Paradigm.Tenses);
        var cells = morphology.ParadigmOf("nu");
        Assert.Equal(6, cells.Count);
        Assert.All(cells, static c => Assert.Null(c.Form));
        Assert.Equal(new ParadigmPerson("2", "pl"), cells[4].Person);
    }

    [Fact]
    public void TheToyPackHasItsOwnErrorTagsAndTheLexiconProfileTheLoaderUsed()
    {
        var bundle = XxBundle.LoadInTemp();

        Assert.Equal(3, bundle.Language.ErrorTags.Count);
        Assert.Equal(1, bundle.Language.ErrorTags.Rank("vocab.wrong"));
        Assert.Same(bundle.Language.LexiconProfile, XxLanguagePack.Instance.LexiconProfile);
    }
}
