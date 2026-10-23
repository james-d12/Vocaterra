using System.Text;

namespace Vocaterra.Nlp.Content.Tests;

/// <summary>CR-007: host-owned files in a bundle are hashed by the manifest, listed in provenance, and readable as their verified bytes.</summary>
public class HostFileTests
{
    private const string HostPath = "game/nouns.json";
    private const string HostText = "{\"nouns\":[\"espada\",\"escudo\"]}\n";

    // A light bundle with one extra host file that the loader does not parse, listed in provenance and in the manifest.
    private static TempBundle BundleWithHostFile()
    {
        var bundle = new TempBundle();
        bundle.Write(HostPath, HostText);
        bundle.Edit(
            "provenance.json",
            "{ \"path\": \"lexicon/game.json\", ",
            "{ \"path\": \"game/nouns.json\", \"generatedBy\": \"hand-authored\", \"approvedBy\": null, \"approvedOn\": null },\n    { \"path\": \"lexicon/game.json\", ");
        bundle.Rehash();
        return bundle;
    }

    [Fact]
    public void AnExtraHashedFileIsAHostFileWhoseBytesAreReturned()
    {
        using var bundle = BundleWithHostFile();

        var loaded = ContentLoader.Load(bundle.Directory);

        Assert.Equal([HostPath], loaded.HostFilePaths);
        Assert.True(loaded.TryReadHostFile(HostPath, out var bytes));
        Assert.Equal(Encoding.UTF8.GetBytes(HostText), bytes.ToArray());
        Assert.Contains(loaded.Manifest.Files, file => file.Path == HostPath);
        Assert.Contains(loaded.Provenance.Items, item => item.Path == HostPath);
    }

    [Fact]
    public void TheCommittedBundleHasNoHostFiles()
    {
        Assert.Empty(TestSupport.Bundle.HostFilePaths);
        Assert.False(TestSupport.Bundle.TryReadHostFile("ATTRIBUTION.md", out _));
    }

    [Fact]
    public void ATamperedHostFileFailsTheLoad()
    {
        using var bundle = BundleWithHostFile();
        File.WriteAllText(bundle.PathOf(HostPath), "{\"nouns\":[\"lanza\"]}\n", new UTF8Encoding(false));

        var ex = Assert.Throws<ContentValidationException>(() => ContentLoader.Load(bundle.Directory));

        Assert.Contains(ex.Problems, problem => problem.StartsWith(HostPath + ": hash mismatch", StringComparison.Ordinal));
    }

    [Fact]
    public void AHostFileChangedAfterTheLoadStillReadsItsVerifiedBytes()
    {
        using var bundle = BundleWithHostFile();
        var loaded = ContentLoader.Load(bundle.Directory);

        File.WriteAllText(bundle.PathOf(HostPath), "changed after the load", new UTF8Encoding(false));

        Assert.True(loaded.TryReadHostFile(HostPath, out var bytes));
        Assert.Equal(Encoding.UTF8.GetBytes(HostText), bytes.ToArray());
    }

    [Fact]
    public void AnUnlistedPathIsRefused()
    {
        using var bundle = BundleWithHostFile();
        var loaded = ContentLoader.Load(bundle.Directory);
        bundle.Write("game/other.json", "{}");

        Assert.False(loaded.TryReadHostFile("game/other.json", out _));
        Assert.False(loaded.TryReadHostFile("game/missing.json", out _));
    }

    [Fact]
    public void ALoaderOwnedFileIsRefused()
    {
        using var bundle = BundleWithHostFile();
        var loaded = ContentLoader.Load(bundle.Directory);

        Assert.False(loaded.TryReadHostFile("lexicon/game.json", out _));
        Assert.False(loaded.TryReadHostFile("prompts/prompts.json", out _));
    }

    [Theory]
    [InlineData("../manifest.json")]
    [InlineData("game/../game/nouns.json")]
    [InlineData("game/./nouns.json")]
    [InlineData("game//nouns.json")]
    [InlineData("/game/nouns.json")]
    [InlineData("game\\nouns.json")]
    [InlineData("C:game/nouns.json")]
    [InlineData("game/nouns.json/")]
    [InlineData("")]
    public void APathThatIsNotRelativeWithForwardSlashesIsRefused(string path)
    {
        using var bundle = BundleWithHostFile();
        var loaded = ContentLoader.Load(bundle.Directory);

        Assert.False(loaded.TryReadHostFile(path, out var bytes));
        Assert.True(bytes.IsEmpty);
    }

    [Fact]
    public void AnAbsolutePathToTheRealFileIsRefused()
    {
        using var bundle = BundleWithHostFile();
        var loaded = ContentLoader.Load(bundle.Directory);

        Assert.False(loaded.TryReadHostFile(bundle.PathOf(HostPath), out _));
    }

    [Fact]
    public void WithoutHashVerificationNoHostFileIsExposed()
    {
        using var bundle = BundleWithHostFile();
        var loaded = ContentLoader.Load(bundle.Directory, new ContentLoadOptions { VerifyHashes = false });

        Assert.Empty(loaded.HostFilePaths);
        Assert.False(loaded.TryReadHostFile(HostPath, out _));
    }
}
