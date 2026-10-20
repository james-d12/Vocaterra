using System.Text;

namespace Vocaterra.Nlp.Asr.Tests;

/// <summary>Tests for the dither-free runtime copy of a Vosk model, using a fake model directory.</summary>
public sealed class VoskRuntimeModelTests : IDisposable
{
    private readonly DirectoryInfo _work = Directory.CreateTempSubdirectory("vocaterra-vosk-rt-");

    public void Dispose()
    {
        _work.Delete(recursive: true);
    }

    [Fact]
    public void PatchRemovesExistingDitherAndAppendsZeroDither()
    {
        var patched = VoskRuntimeModel.PatchMfccConfig("--use-energy=false\r\n--dither=1.0\r\n\r\n  --dither = 0.5\r\n--num-ceps=40");

        Assert.Equal("--use-energy=false\n--num-ceps=40\n--dither=0.0\n", patched);
    }

    [Fact]
    public void PatchOfAConfigWithoutDitherJustAppendsIt()
    {
        Assert.Equal("--low-freq=20\n--dither=0.0\n", VoskRuntimeModel.PatchMfccConfig("--low-freq=20\n"));
    }

    [Fact]
    public void PrepareLinksEveryFileExceptThePatchedConfig()
    {
        var model = CreateFakeModel("--use-energy=false\n--dither=1.0\n");
        var root = Path.Combine(_work.FullName, "runtime");

        var runtime = VoskRuntimeModel.Prepare(model, "hash-a", root);

        Assert.StartsWith(root, runtime, StringComparison.Ordinal);
        Assert.Equal("--use-energy=false\n--dither=0.0\n", File.ReadAllText(Path.Combine(runtime, "conf", "mfcc.conf")));
        Assert.Equal("graph-bytes", File.ReadAllText(Path.Combine(runtime, "graph", "Gr.fst")));
        Assert.Equal("model-bytes", File.ReadAllText(Path.Combine(runtime, "am", "final.mdl")));
        Assert.Equal("--use-energy=false\n--dither=1.0\n", File.ReadAllText(Path.Combine(model, "conf", "mfcc.conf")));
    }

    [Fact]
    public void PrepareIsRepeatableAndSharesTheDirectoryForEqualModels()
    {
        var model = CreateFakeModel("--use-energy=false\n");
        var root = Path.Combine(_work.FullName, "runtime");

        var first = VoskRuntimeModel.Prepare(model, "hash-a", root);
        var second = VoskRuntimeModel.Prepare(model, "hash-a", root);
        var other = VoskRuntimeModel.Prepare(model, "hash-b", root);

        Assert.Equal(first, second);
        Assert.NotEqual(first, other);
        Assert.Equal(2, Directory.GetDirectories(root).Length);
    }

    [Fact]
    public void PrepareRebuildsADirectoryWhoseFilesWentAway()
    {
        var model = CreateFakeModel("--use-energy=false\n");
        var root = Path.Combine(_work.FullName, "runtime");
        var runtime = VoskRuntimeModel.Prepare(model, "hash-a", root);
        File.Delete(Path.Combine(runtime, "graph", "Gr.fst"));

        var again = VoskRuntimeModel.Prepare(model, "hash-a", root);

        Assert.Equal(runtime, again);
        Assert.Equal("graph-bytes", File.ReadAllText(Path.Combine(again, "graph", "Gr.fst")));
    }

    [Fact]
    public void PrepareLeavesNoStagingDirectoriesBehind()
    {
        var model = CreateFakeModel("--use-energy=false\n");
        var root = Path.Combine(_work.FullName, "runtime");

        VoskRuntimeModel.Prepare(model, "hash-a", root);

        Assert.DoesNotContain(Directory.GetDirectories(root), d => d.Contains(".tmp", StringComparison.Ordinal));
    }

    [Fact]
    public void PrepareReturnsTheModelUnchangedWhenThereIsNoMfccConfig()
    {
        var model = Path.Combine(_work.FullName, "odd-model");
        Directory.CreateDirectory(model);
        File.WriteAllText(Path.Combine(model, "final.mdl"), "x");

        Assert.Equal(model, VoskRuntimeModel.Prepare(model, "hash-a", Path.Combine(_work.FullName, "runtime")));
    }

    private string CreateFakeModel(string mfcc)
    {
        var model = Path.Combine(_work.FullName, "fake-model");
        Directory.CreateDirectory(Path.Combine(model, "conf"));
        Directory.CreateDirectory(Path.Combine(model, "graph"));
        Directory.CreateDirectory(Path.Combine(model, "am"));
        File.WriteAllText(Path.Combine(model, "conf", "mfcc.conf"), mfcc, new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(model, "graph", "Gr.fst"), "graph-bytes");
        File.WriteAllText(Path.Combine(model, "am", "final.mdl"), "model-bytes");
        return model;
    }
}
