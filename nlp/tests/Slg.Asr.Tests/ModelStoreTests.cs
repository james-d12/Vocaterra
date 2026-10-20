using System.Text;
using Slg.Asr.Models;
using Slg.Core.Determinism;

namespace Slg.Asr.Tests;

/// <summary>Tests for <see cref="ModelStore"/> against throw-away model directories.</summary>
public sealed class ModelStoreTests : IDisposable
{
    private const string ModelId = "tiny-model";

    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("slg-models-");

    public void Dispose()
    {
        _root.Delete(recursive: true);
    }

    [Fact]
    public void VerifyReportsOkForAnUntouchedModel()
    {
        var store = CreateStore(out _);

        var report = store.Verify(ModelId);

        Assert.Equal(ModelStatus.Ok, report.Status);
        Assert.True(report.IsOk);
        Assert.Equal(["a.bin", "sub/b.txt"], report.Files.Select(f => f.Path).ToArray());
        Assert.All(report.Files, f => Assert.Equal(FileStatus.Ok, f.Status));
    }

    [Fact]
    public void VerifyDetectsATamperedFile()
    {
        var store = CreateStore(out _);
        File.WriteAllBytes(store.ResolvePath(ModelId, "sub/b.txt"), Encoding.UTF8.GetBytes("tampered contents"));

        var report = store.Verify(ModelId);

        Assert.Equal(ModelStatus.Corrupt, report.Status);
        Assert.Equal(FileStatus.Ok, report.Files[0].Status);
        var bad = report.Files[1];
        Assert.Equal(FileStatus.Corrupt, bad.Status);
        Assert.NotEqual(bad.ExpectedSha256, bad.ActualSha256);
    }

    [Fact]
    public void VerifyDetectsATamperedFileOfTheSameLength()
    {
        var store = CreateStore(out _);
        var path = store.ResolvePath(ModelId, "a.bin");
        var bytes = File.ReadAllBytes(path);
        bytes[0] ^= 0xFF;
        File.WriteAllBytes(path, bytes);

        var report = store.Verify(ModelId);

        Assert.Equal(ModelStatus.Corrupt, report.Status);
        Assert.Equal(FileStatus.Corrupt, report.Files[0].Status);
        Assert.NotNull(report.Files[0].ActualSha256);
    }

    [Fact]
    public void VerifyReturnsMissingInsteadOfThrowing()
    {
        var store = CreateStore(out _);
        File.Delete(store.ResolvePath(ModelId, "a.bin"));

        var report = store.Verify(ModelId);

        Assert.Equal(ModelStatus.Missing, report.Status);
        Assert.Equal(FileStatus.Missing, report.Files[0].Status);
        Assert.Equal(FileStatus.Ok, report.Files[1].Status);
    }

    [Fact]
    public void VerifyReturnsMissingForAnAbsentDirectoryAndForUnknownIds()
    {
        var store = CreateStore(out _);
        Directory.Delete(store.ModelDirectory(ModelId), recursive: true);

        Assert.Equal(ModelStatus.Missing, store.Verify(ModelId).Status);
        Assert.Equal(ModelStatus.Missing, store.Verify("no-such-model").Status);
        Assert.Equal(ModelStatus.Missing, store.Verify("../escape").Status);
    }

    [Fact]
    public void CorruptOutranksMissing()
    {
        var store = CreateStore(out _);
        File.Delete(store.ResolvePath(ModelId, "a.bin"));
        File.WriteAllText(store.ResolvePath(ModelId, "sub/b.txt"), "tampered");

        Assert.Equal(ModelStatus.Corrupt, store.Verify(ModelId).Status);
    }

    [Fact]
    public void UnpinnedFilesAreReportedAsSuch()
    {
        _ = CreateStore(out var files);
        var manifest = new ModelManifest(1, [Entry(files.Select(f => f with { Sha256 = null, Bytes = null }).ToArray())]);
        var unpinned = new ModelStore(_root.FullName, manifest);

        var report = unpinned.Verify(ModelId);

        Assert.Equal(ModelStatus.Unpinned, report.Status);
        Assert.All(report.Files, f => Assert.Equal(FileStatus.Unpinned, f.Status));
        Assert.Throws<InvalidOperationException>(() => unpinned.ModelHash(ModelId));
    }

    [Fact]
    public void ModelHashCombinesTheSortedFileHashes()
    {
        var store = CreateStore(out var files);

        var expected = DeterministicHash.Combine(files.Select(f => f.Sha256!).Order(StringComparer.Ordinal).ToArray());

        Assert.Equal(expected, store.ModelHash(ModelId));
    }

    [Fact]
    public void ModelHashDoesNotDependOnTheOrderOfTheFileList()
    {
        var store = CreateStore(out var files);
        var reversed = new ModelStore(_root.FullName, new ModelManifest(1, [Entry(files.Reverse().ToArray())]));

        Assert.Equal(store.ModelHash(ModelId), reversed.ModelHash(ModelId));
    }

    [Fact]
    public void ModelHashChangesWhenAPinnedHashChanges()
    {
        var store = CreateStore(out var files);
        var altered = files.ToArray();
        altered[0] = altered[0] with { Sha256 = new string('0', 64) };
        var other = new ModelStore(_root.FullName, new ModelManifest(1, [Entry(altered)]));

        Assert.NotEqual(store.ModelHash(ModelId), other.ModelHash(ModelId));
    }

    [Fact]
    public void ModelHashThrowsForUnknownIds()
    {
        var store = CreateStore(out _);

        Assert.Throws<KeyNotFoundException>(() => store.ModelHash("no-such-model"));
    }

    [Fact]
    public void HashDirectoryEqualsTheManifestModelHashWhenAllFilesAreListed()
    {
        var store = CreateStore(out _);

        Assert.Equal(store.ModelHash(ModelId), ModelStore.HashDirectory(store.ModelDirectory(ModelId)));
    }

    [Fact]
    public void HashDirectoryThrowsForAnAbsentDirectory()
    {
        Assert.Throws<DirectoryNotFoundException>(() => ModelStore.HashDirectory(Path.Combine(_root.FullName, "absent")));
    }

    [Fact]
    public void ModelStoreReadsTheManifestFromTheRootDirectory()
    {
        var store = CreateStore(out _);
        File.WriteAllText(Path.Combine(_root.FullName, ModelStore.ManifestFileName), store.Manifest.ToJson());

        var reloaded = new ModelStore(_root.FullName);

        Assert.Equal(store.Manifest, reloaded.Manifest);
        Assert.Equal(store.ModelHash(ModelId), reloaded.ModelHash(ModelId));
    }

    [Fact]
    public void ModelStoreWithoutAManifestFileThrows()
    {
        Assert.Throws<FileNotFoundException>(() => new ModelStore(_root.FullName));
    }

    [Fact]
    public void FromEnvironmentIsNullWhenTheVariableIsUnsetOrBlank()
    {
        Assert.Null(ModelStore.FromEnvironment(_ => null));
        Assert.Null(ModelStore.FromEnvironment(_ => string.Empty));
        Assert.Null(ModelStore.FromEnvironment(_ => "   "));
    }

    [Fact]
    public void FromEnvironmentUsesTheDirectoryAndItsManifest()
    {
        var store = CreateStore(out _);
        File.WriteAllText(Path.Combine(_root.FullName, ModelStore.ManifestFileName), store.Manifest.ToJson());

        var fromEnvironment = ModelStore.FromEnvironment(name => name == "SLG_MODELS_DIR" ? _root.FullName : null);

        Assert.NotNull(fromEnvironment);
        Assert.Equal(_root.FullName, fromEnvironment.RootDirectory);
        Assert.Equal(ModelStatus.Ok, fromEnvironment.Verify(ModelId).Status);
    }

    [Fact]
    public void FromEnvironmentWithoutAManifestFileHasAnEmptyManifest()
    {
        var fromEnvironment = ModelStore.FromEnvironment(_ => _root.FullName);

        Assert.NotNull(fromEnvironment);
        Assert.Empty(fromEnvironment.Manifest.Models);
        Assert.Equal(ModelStatus.Missing, fromEnvironment.Verify(ModelId).Status);
    }

    [Fact]
    public void PathsOutsideTheModelDirectoryAreRejected()
    {
        var store = CreateStore(out _);

        Assert.Throws<ArgumentException>(() => store.ModelDirectory("../x"));
        Assert.Throws<ArgumentException>(() => store.ResolvePath(ModelId, "../x"));
        Assert.Throws<ArgumentException>(() => store.ResolvePath(ModelId, "/etc/passwd"));
    }

    private static ModelEntry Entry(ModelFile[] files)
    {
        return new ModelEntry(ModelId, ModelRoles.AsrOpen, "Tiny", "1", "MIT", "https://example.test/tiny.zip", null, files, null);
    }

    private ModelStore CreateStore(out ModelFile[] files)
    {
        var directory = Path.Combine(_root.FullName, ModelId);
        Directory.CreateDirectory(Path.Combine(directory, "sub"));
        var a = Encoding.UTF8.GetBytes("first file contents");
        var b = Encoding.UTF8.GetBytes("second file contents, a bit longer");
        File.WriteAllBytes(Path.Combine(directory, "a.bin"), a);
        File.WriteAllBytes(Path.Combine(directory, "sub", "b.txt"), b);
        files =
        [
            new ModelFile("a.bin", DeterministicHash.Sha256Hex(a), a.Length),
            new ModelFile("sub/b.txt", DeterministicHash.Sha256Hex(b), b.Length),
        ];
        return new ModelStore(_root.FullName, new ModelManifest(1, [Entry(files)]));
    }
}
