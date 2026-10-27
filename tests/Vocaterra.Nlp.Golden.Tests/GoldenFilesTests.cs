
namespace Vocaterra.Nlp.Golden.Tests;

/// <summary>The golden layout per language (M35): Spanish keeps the old paths, another language has its own folder. Pure path logic; no file is written.</summary>
public class GoldenFilesTests
{
    [Fact]
    public void ASpanishGoldenKeepsItsOldPath()
    {
        Assert.Equal("e2e-villager-request.golden.json", GoldenFiles.RelativePath("es", "e2e-villager-request.golden.json"));
        Assert.Equal("e2e-villager-request.golden.json", GoldenFiles.RelativePath(null, "e2e-villager-request.golden.json"));
        Assert.Equal("e2e-villager-request.golden.json", GoldenFiles.RelativePath("", "e2e-villager-request.golden.json"));
    }

    [Fact]
    public void AnotherLanguageGoldenLivesInItsOwnFolder()
    {
        Assert.Equal("de/e2e-villager-request.golden.json", GoldenFiles.RelativePath("de", "e2e-villager-request.golden.json"));
        Assert.Equal(
            Path.Combine(AppContext.BaseDirectory, "Data", "de", "g2p.golden.json"),
            GoldenFiles.RunPath("de", "g2p.golden.json"));
    }

    [Fact]
    public void ALanguageIdThatIsAPathIsRefused()
    {
        Assert.Throws<ArgumentException>(() => GoldenFiles.RelativePath("../es", "x.json"));
        Assert.Throws<ArgumentException>(() => GoldenFiles.RelativePath("de/x", "x.json"));
    }
}
