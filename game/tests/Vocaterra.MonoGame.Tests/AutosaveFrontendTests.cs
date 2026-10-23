using Vocaterra.MonoGame.Frontend;

namespace Vocaterra.MonoGame.Tests;

/// <summary>The autosave's file (X05, R04): the atomic write leaves the old save when a write fails, and the flags of the command line.</summary>
public sealed class AutosaveFrontendTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "vocaterra-x05-save-" + Path.GetRandomFileName());

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
            // Nothing was made.
        }
        catch (UnauthorizedAccessException)
        {
            // Nothing was made.
        }
    }

    [Fact]
    public void AFailedWriteLeavesTheOldSaveIntact()
    {
        Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, "autosave.save");
        SaveFile.Write(path, "{\"save\":\"the good one\"}");

        // The temporary file cannot be created: a folder stands where it would be.
        Directory.CreateDirectory(path + ".tmp");

        Assert.ThrowsAny<Exception>(() => SaveFile.Write(path, "{\"save\":\"the new one\"}"));
        Assert.Equal("{\"save\":\"the good one\"}", SaveFile.Read(path));
    }

    [Fact]
    public void AWriteIntoAVanishedFolderFailsAndCreatesNothing()
    {
        Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, "crash.save");
        SaveFile.Write(path, "{\"save\":1}");

        // The save's folder is removed under the write: the write fails, the earlier file is not touched.
        Directory.Delete(_folder, recursive: true);

        Assert.ThrowsAny<Exception>(() => SaveFile.Write(path, "{\"save\":2}"));
        Assert.False(Directory.Exists(_folder));
    }

    [Fact]
    public void AStaleTemporaryFileDoesNotStopTheNextWrite()
    {
        Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, "autosave.save");
        File.WriteAllText(path + ".tmp", "a half-written save from a crash");

        SaveFile.Write(path, "{\"save\":3}");

        Assert.Equal("{\"save\":3}", SaveFile.Read(path));
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void NoAutosaveAndTheDebugCrashFlagAreRead()
    {
        Assert.True(LaunchOptions.TryParse(["--no-autosave", "--screenshot", "x.png"], out var off, out _));
        Assert.True(off.NoAutosave);
        Assert.Null(off.DebugCrashAfter);

        Assert.True(LaunchOptions.TryParse(["--screenshot", "x.png"], out var on, out _));
        Assert.False(on.NoAutosave);

        Assert.True(LaunchOptions.TryParse(["--debug-crash-after", "4680", "--screenshot", "x.png"], out var crash, out var error), error);
        Assert.Equal(4680L, crash.DebugCrashAfter);
    }

    [Fact]
    public void TheDebugCrashFlagRefusesANegativeOrMissingTick()
    {
        Assert.False(LaunchOptions.TryParse(["--debug-crash-after", "-1"], out _, out var negative));
        Assert.Contains("--debug-crash-after needs a tick", negative, StringComparison.Ordinal);

        Assert.False(LaunchOptions.TryParse(["--debug-crash-after"], out _, out var missing));
        Assert.Contains("--debug-crash-after needs a value", missing, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDebugCrashFlagIsNotInTheUsage()
    {
        Assert.DoesNotContain("--debug-crash-after", LaunchOptions.Usage, StringComparison.Ordinal);
        Assert.Contains("--no-autosave", LaunchOptions.Usage, StringComparison.Ordinal);
    }
}
