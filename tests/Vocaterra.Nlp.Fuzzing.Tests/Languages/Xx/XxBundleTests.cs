using Vocaterra.Nlp.Content;

namespace Vocaterra.Nlp.Fuzzing.Tests.Languages.Xx;

/// <summary>The toy bundle loads through the loader with the toy registry, and the registry holds no Spanish pack.</summary>
public sealed class XxBundleTests
{
    [Fact]
    public void TheToyBundleLoadsWithItsOwnPackAndTheHashesVerified()
    {
        var bundle = XxBundle.LoadInTemp();

        Assert.Equal("xx", bundle.LanguageId);
        Assert.Same(XxLanguagePack.Instance, bundle.Language);
        Assert.Equal("xx", bundle.Manifest.Language);
        Assert.Equal(3, bundle.Lexicon.Entries.Count);
    }

    [Fact]
    public void TheToyRegistryHoldsNoSpanishPack()
    {
        Assert.False(XxBundle.Registry.TryResolve("es", out _));
        Assert.False(XxBundle.Registry.TryResolve("es-MX", out _));
        Assert.True(XxBundle.Registry.TryResolve("XX", out var pack));
        Assert.Same(XxLanguagePack.Instance, pack);
    }

    [Fact]
    public void ALoadWithTheDefaultRegistryIsRefusedBecauseTheToyLanguageIsNotRegistered()
    {
        var directory = Path.Combine(Path.GetTempPath(), "vocaterra-xx-refused-" + Guid.NewGuid().ToString("N"));
        try
        {
            XxBundle.WriteTo(directory);

            var ex = Assert.Throws<ContentValidationException>(() => ContentLoader.Load(directory));

            Assert.Contains("no language pack is registered for language 'xx'", string.Join('\n', ex.Problems));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
