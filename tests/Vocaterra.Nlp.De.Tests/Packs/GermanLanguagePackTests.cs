namespace Vocaterra.Nlp.De.Tests.Packs;

/// <summary>Tests for <see cref="GermanLanguagePack"/>, <see cref="GermanContent"/> and <see cref="GermanLexicon"/>: the surface, the registration and the placeholders.</summary>
public class GermanLanguagePackTests
{
    private static readonly GermanLanguagePack Pack = GermanLanguagePack.Instance;

    [Fact]
    public void TheLanguageIsGermanWithFourCasesAndNoV2UntilM40()
    {
        Assert.Equal("de", Pack.LanguageId);
        Assert.Equal(["nom", "acc", "dat", "gen"], Pack.GrammaticalCases);
        Assert.False(Pack.SupportsV2Order);
    }

    [Fact]
    public void TheErrorTagsAreTheV1TagsPlusOrthCapital()
    {
        Assert.True(Pack.ErrorTags.IsKnown(ErrorTags.OrthCapital));
        Assert.True(Pack.ErrorTags.IsKnown(ErrorTags.ConjPerson));
        Assert.Equal(ErrorTagSet.V1.Count + 1, Pack.ErrorTags.Count);
        Assert.Equal(ErrorTagSet.V1.All, Pack.ErrorTags.All.Take(ErrorTagSet.V1.Count));
        Assert.False(ErrorTagSet.V1.IsKnown(ErrorTags.OrthCapital));
    }

    [Fact]
    public void ThePoliteWordIsBitteInAllThreePositionsAndTheSubjectIsNeverDropped()
    {
        var defaults = Pack.GrammarDefaults;

        Assert.False(defaults.OptionalSubjectPronoun);
        Assert.Equal(["bitte"], defaults.TrailingPoliteWords);
        Assert.Equal(PolitePositions.Trailing | PolitePositions.Leading | PolitePositions.AfterFiniteVerb, defaults.PolitePositions);
        Assert.Contains("ich", defaults.SubjectPronouns);
        Assert.Equal(["bitte"], Pack.Lint.PolitePhrase);
    }

    [Fact]
    public void TheTextMembersAreTheGermanClasses()
    {
        Assert.Same(GermanNormalizer.Instance, Pack.Normalizer);
        Assert.Same(GermanFolding.Instance, Pack.Folding);
        Assert.Same(GermanPhoneticKey.Instance, Pack.PhoneticKey);
        Assert.Same(GermanPunctuation.Instance, Pack.Punctuation);
        Assert.Same(GermanRecastPolicy.Instance, Pack.Recast);
        Assert.Null(Pack.G2p);
    }

    [Fact]
    public void ThePlaceholdersSayWhichMilestoneWritesThem()
    {
        var morphology = Assert.Throws<NotSupportedException>(() => Pack.CreateMorphology(GermanLexicon.Empty));
        var rules = Assert.Throws<NotSupportedException>(() => Pack.CreateAnalysisRules(GermanLexicon.Empty, null!));
        var functions = Assert.Throws<NotSupportedException>(() => Pack.CreateTemplateFunctions(GermanLexicon.Empty, null!));

        Assert.Contains("M38", morphology.Message, StringComparison.Ordinal);
        Assert.Contains("M39", rules.Message, StringComparison.Ordinal);
        Assert.Contains("M40", functions.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WithGermanRegistersThePackUnderDe()
    {
        var packs = LanguagePacks.Default.WithGerman();

        Assert.True(packs.TryResolve("de-AT", out var resolved));
        Assert.Same(Pack, resolved);
        Assert.True(GermanContent.Options.LanguagePacks.TryResolve("de", out var viaOptions));
        Assert.Same(Pack, viaOptions);
    }

    [Fact]
    public void TheProfileReadsTheRealGermanLexiconWithoutAnEntryRefused()
    {
        var lexicon = TestSupport.FullLexicon;

        Assert.True(lexicon.Entries.Count > 100_000);
        Assert.Same(GermanFolding.Instance, lexicon.Folding);
    }

    [Fact]
    public void TheLexiconProfileListsTheGermanFeatureValues()
    {
        var profile = Pack.LexiconProfile;

        Assert.Equal(["m", "f", "n"], profile.Genders);
        Assert.Equal(["nom", "acc", "dat", "gen"], profile.Cases);
        Assert.Contains("part2-sein", profile.VerbForms);
        Assert.Contains("mixed", profile.Declensions);
        Assert.Contains("zurück", profile.Particles);
    }

    [Fact]
    public void ThePunctuationAndNumbersMembersDoNotThrow()
    {
        Assert.Equal("Hallo, Welt!", Pack.Punctuation.Fix("Hallo , Welt !"));
        Assert.Equal("zwölf", Pack.Numbers.ToWords(12));
    }
}
