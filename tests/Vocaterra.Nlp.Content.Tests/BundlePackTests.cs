using System.IO.Compression;
using System.Text;
using Vocaterra.Nlp.ContentBuilder;

namespace Vocaterra.Nlp.Content.Tests;

/// <summary>The archive commands of the ContentBuilder, <c>pack</c> and <c>verify-pack</c>, through <see cref="Program.Run(string[], TextWriter, TextWriter)"/>.</summary>
public sealed class BundlePackTests : IDisposable
{
    private const string WrongHash = "0000000000000000000000000000000000000000000000000000000000000000";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "vocaterra-pack-tests-" + Guid.NewGuid().ToString("N"));

    // The archive commands are packed from a light copy of the bundle (no speech clips, no imported lexicon), so each test packs a small bundle.
    private readonly TempBundle _bundle = new();
    private readonly ContentManifest _manifest;

    public BundlePackTests()
    {
        Directory.CreateDirectory(_folder);
        _manifest = ContentManifest.Compute(_bundle.Directory);
    }

    public static TheoryData<string> WrongCommandLines()
    {
        // Each entry is one command line, words separated by a single space.
        return new TheoryData<string>
        {
            "pack",
            "pack a",
            "pack a b c",
            "verify-pack",
            "verify-pack a b",
            "verify-pack a --sidecar",
        };
    }

    [Fact]
    public void PackWritesTheArchiveAndASidecarHoldingTheBundleHash()
    {
        var archive = Archive();

        var (code, output, error) = Run("pack", _bundle.Directory, archive);

        Assert.Equal(Program.ExitOk, code);
        Assert.Equal(string.Empty, error);
        Assert.Contains($"bundle hash {_manifest.BundleHash}", output, StringComparison.Ordinal);
        Assert.Equal(_manifest.BundleHash + "\n", File.ReadAllText(archive + BundlePack.SidecarSuffix, Encoding.UTF8));

        using var zip = ZipFile.OpenRead(archive);
        var expected = _manifest.Files.Select(static file => file.Path).Append(ContentPaths.Manifest).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(expected, zip.Entries.Select(static entry => entry.FullName).Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void PackingTheSameBundleTwiceGivesTheSameBytes()
    {
        var first = Archive("first.zip");
        var second = Archive("second.zip");

        Assert.Equal(Program.ExitOk, Run("pack", _bundle.Directory, first).Code);
        Assert.Equal(Program.ExitOk, Run("pack", _bundle.Directory, second).Code);

        Assert.Equal(File.ReadAllBytes(first), File.ReadAllBytes(second));
    }

    [Fact]
    public void VerifyPackAcceptsAPackedBundleAndReportsItsHash()
    {
        var archive = Archive();
        Assert.Equal(Program.ExitOk, Run("pack", _bundle.Directory, archive).Code);

        var (code, output, error) = Run("verify-pack", archive);

        Assert.Equal(Program.ExitOk, code);
        Assert.Equal(string.Empty, error);
        Assert.StartsWith("OK: ", output, StringComparison.Ordinal);
        Assert.Contains(_manifest.BundleHash, output, StringComparison.Ordinal);
    }

    [Fact]
    public void VerifyPackRejectsASidecarThatNamesAnotherHash()
    {
        var archive = Archive();
        Assert.Equal(Program.ExitOk, Run("pack", _bundle.Directory, archive).Code);
        File.WriteAllText(archive + BundlePack.SidecarSuffix, WrongHash + "\n", new UTF8Encoding(false));

        var (code, _, error) = Run("verify-pack", archive);

        Assert.Equal(Program.ExitInvalid, code);
        Assert.Contains($"The sidecar names bundle hash {WrongHash}", error, StringComparison.Ordinal);
        Assert.Contains($"hashes to {_manifest.BundleHash}", error, StringComparison.Ordinal);
    }

    [Fact]
    public void VerifyPackRejectsAnArchiveWhoseFileWasChanged()
    {
        // The sidecar of the sound archive is kept; one entry is copied with a byte added, so the loader's hash check fails for that file.
        var sound = Archive("sound.zip");
        Assert.Equal(Program.ExitOk, Run("pack", _bundle.Directory, sound).Code);
        var damaged = Archive("damaged.zip");
        using (var source = ZipFile.OpenRead(sound))
        using (var target = ZipFile.Open(damaged, ZipArchiveMode.Create))
        {
            foreach (var entry in source.Entries)
            {
                var copy = target.CreateEntry(entry.FullName);
                using var input = entry.Open();
                using var output = copy.Open();
                input.CopyTo(output);
                if (entry.FullName == "prompts/prompts.json")
                {
                    output.WriteByte((byte)' ');
                }
            }
        }

        File.Copy(sound + BundlePack.SidecarSuffix, damaged + BundlePack.SidecarSuffix);

        var (code, _, error) = Run("verify-pack", damaged);

        Assert.Equal(Program.ExitInvalid, code);
        Assert.Contains("prompts/prompts.json", error, StringComparison.Ordinal);
    }

    [Fact]
    public void VerifyPackRefusesAnEntryThatWouldLeaveTheFolder()
    {
        var name = "vocaterra-escape-" + Guid.NewGuid().ToString("N") + ".json";
        var archive = Archive("escape.zip");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        using (var writer = new StreamWriter(zip.CreateEntry("../" + name).Open(), new UTF8Encoding(false)))
        {
            writer.Write("{}");
        }

        File.WriteAllText(archive + BundlePack.SidecarSuffix, WrongHash + "\n", new UTF8Encoding(false));

        var (code, _, error) = Run("verify-pack", archive);

        Assert.Equal(Program.ExitInvalid, code);
        Assert.Contains("Unsafe entry name", error, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(Path.GetTempPath(), name)));
    }

    [Fact]
    public void PackRefusesAnInvalidBundleAndWritesNothing()
    {
        using var bundle = new TempBundle();
        bundle.Touch("prompts/prompts.json");
        var archive = Archive();

        var (code, output, error) = Run("pack", bundle.Directory, archive);

        Assert.Equal(Program.ExitInvalid, code);
        Assert.Equal(string.Empty, output);
        Assert.Contains("prompts/prompts.json", error, StringComparison.Ordinal);
        Assert.False(File.Exists(archive));
        Assert.False(File.Exists(archive + BundlePack.SidecarSuffix));
    }

    [Fact]
    public void VerifyPackRejectsAMissingSidecarAndAFileThatIsNotAnArchive()
    {
        var archive = Archive();
        Assert.Equal(Program.ExitOk, Run("pack", _bundle.Directory, archive).Code);
        File.Delete(archive + BundlePack.SidecarSuffix);

        var (missingCode, _, missingError) = Run("verify-pack", archive);

        Assert.Equal(Program.ExitInvalid, missingCode);
        Assert.Contains("Sidecar not found", missingError, StringComparison.Ordinal);

        var notZip = Archive("notes.zip");
        File.WriteAllText(notZip, "not a zip archive", new UTF8Encoding(false));
        File.WriteAllText(notZip + BundlePack.SidecarSuffix, WrongHash + "\n", new UTF8Encoding(false));

        var (notZipCode, _, notZipError) = Run("verify-pack", notZip);

        Assert.Equal(Program.ExitInvalid, notZipCode);
        Assert.Contains("Not a zip archive", notZipError, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(WrongCommandLines))]
    public void PackCommandsRejectAWrongCommandLine(string commandLine)
    {
        var (code, _, _) = Run(commandLine.Split(' '));

        Assert.Equal(Program.ExitUsage, code);
    }

    public void Dispose()
    {
        _bundle.Dispose();
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temporary directory must not fail a test.
        }
    }

    private string Archive(string name = "bundle.zip")
    {
        return Path.Combine(_folder, name);
    }

    private static (int Code, string Output, string Error) Run(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = Program.Run(args, output, error);
        return (code, output.ToString().Replace("\r\n", "\n", StringComparison.Ordinal), error.ToString().Replace("\r\n", "\n", StringComparison.Ordinal));
    }
}
