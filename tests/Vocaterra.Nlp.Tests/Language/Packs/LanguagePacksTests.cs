namespace Vocaterra.Nlp.Language.Packs.Tests;

/// <summary>The registry: the default set, custom sets and resolution by the primary subtag of a BCP-47 tag.</summary>
public class LanguagePacksTests
{
    /// <summary>A pack under another id, built from the Spanish members, for the registry tests.</summary>
    internal sealed class StubPack(string id) : ILanguagePack
    {
        public string LanguageId => id;

        public ITextNormalizer Normalizer => SpanishLanguagePack.Instance.Normalizer;

        public ILexiconFolding Folding => SpanishLanguagePack.Instance.Folding;

        public INumberWords Numbers => SpanishLanguagePack.Instance.Numbers;

        public IPhoneticKey PhoneticKey => SpanishLanguagePack.Instance.PhoneticKey;

        public IPunctuation Punctuation => SpanishLanguagePack.Instance.Punctuation;

        public IG2p? G2p => null;

        public GrammarDefaults GrammarDefaults => SpanishLanguagePack.Instance.GrammarDefaults;

        public LintVocabulary Lint => SpanishLanguagePack.Instance.Lint;

        public RepairLines RepairFallback => SpanishLanguagePack.Instance.RepairFallback;

        public IRecastPolicy Recast => SpanishLanguagePack.Instance.Recast;
    }

    [Fact]
    public void TheDefaultRegistryHoldsSpanishOnly()
    {
        Assert.Equal(["es"], LanguagePacks.Default.Languages);
        Assert.Empty(LanguagePacks.Empty.Languages);
        Assert.True(LanguagePacks.Default.TryResolve("es", out var pack));
        Assert.Same(SpanishLanguagePack.Instance, pack);
    }

    [Theory]
    [InlineData("es")]
    [InlineData("ES")]
    [InlineData("Es")]
    [InlineData("es-MX")]
    [InlineData("es-mx")]
    [InlineData("es-419")]
    [InlineData("es-Latn-MX")]
    public void ASpanishTagResolvesByItsPrimarySubtag(string tag)
    {
        Assert.True(LanguagePacks.Default.TryResolve(tag, out var pack));
        Assert.Same(SpanishLanguagePack.Instance, pack);
    }

    [Theory]
    [InlineData("xx")]
    [InlineData("de")]
    [InlineData("eso")]
    [InlineData("e")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("es_MX")]
    [InlineData("es-")]
    [InlineData("-es")]
    [InlineData("es--MX")]
    [InlineData("es-toolongsubtag")]
    [InlineData("12")]
    [InlineData("es MX")]
    [InlineData("é")]
    [InlineData("ès")]
    public void ATagWithoutAPackOrNotWellFormedDoesNotResolve(string? tag)
    {
        Assert.False(LanguagePacks.Default.TryResolve(tag, out _));
    }

    [Theory]
    [InlineData("es", "es")]
    [InlineData("DE-de", "de")]
    [InlineData("zh-Hant-TW", "zh")]
    [InlineData("fil", "fil")]
    [InlineData("i-klingon", null)]
    [InlineData("de-1", "de")]
    public void ThePrimarySubtagIsReadFromAWellFormedTag(string tag, string? expected)
    {
        Assert.Equal(expected is not null, LanguagePacks.TryGetPrimarySubtag(tag, out var primary));
        Assert.Equal(expected ?? string.Empty, primary);
    }

    [Fact]
    public void AllCapitalLetterOrdinaryComparisonIsInvariant()
    {
        // The Turkish dotless i must not make "TI" and "ti" differ from the ordinal ASCII fold.
        var registry = LanguagePacks.Of(new StubPack("ti"));
        Assert.True(registry.TryResolve("TI", out _));
        Assert.True(registry.TryResolve("Ti-ET", out _));
    }

    [Fact]
    public void WithBuildsANewRegistryAndLeavesTheOldOneUnchanged()
    {
        var german = new StubPack("de");
        var custom = LanguagePacks.Default.With(german);

        Assert.Equal(["de", "es"], custom.Languages);
        Assert.Equal(["es"], LanguagePacks.Default.Languages);
        Assert.True(custom.TryResolve("de-DE", out var found));
        Assert.Same(german, found);
        Assert.True(custom.TryResolve("es-MX", out found));
        Assert.Same(SpanishLanguagePack.Instance, found);
    }

    [Fact]
    public void ALaterPackReplacesAnEarlierOneWithTheSameId()
    {
        var replacement = new StubPack("es");

        var custom = LanguagePacks.Of(SpanishLanguagePack.Instance, replacement);

        Assert.True(custom.TryResolve("es", out var found));
        Assert.Same(replacement, found);
        Assert.Equal(["es"], custom.Languages);
    }

    [Theory]
    [InlineData("")]
    [InlineData("e")]
    [InlineData("ES")]
    [InlineData("es-MX")]
    [InlineData("e1")]
    public void APackWithABadIdIsRefused(string id)
    {
        Assert.Throws<ArgumentException>(() => LanguagePacks.Empty.With(new StubPack(id)));
    }
}
