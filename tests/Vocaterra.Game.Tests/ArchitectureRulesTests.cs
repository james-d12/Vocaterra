using System.Text.RegularExpressions;

namespace Vocaterra.Game.Tests;

/// <summary>The layering and determinism rules of plan section 3, checked on the sources and the project file of <c>Vocaterra.Game</c>.</summary>
public sealed class ArchitectureRulesTests
{
    private static readonly string[] BannedTokens =
    [
        "System.Random", "new Random", "Random.Shared", "Guid.NewGuid", "DateTime", "DateTimeOffset", "Stopwatch", "Environment.TickCount",
        "TimeProvider", "Thread.Sleep", "Microsoft.Xna", "FontStashSharp", "using Godot",
    ];

    [Fact]
    public void TheGameLayerReadsNoClockDrawsNoRandomNumbersAndNamesNoEngine()
    {
        var files = Sources().ToList();
        Assert.NotEmpty(files);
        foreach (var file in files)
        {
            var code = string.Join('\n', File.ReadAllLines(file).Select(StripComment));
            foreach (var token in BannedTokens)
            {
                Assert.False(code.Contains(token, StringComparison.Ordinal), $"{Path.GetFileName(file)} uses {token}.");
            }
        }
    }

    [Fact]
    public void TheProjectReferencesTheSimulationAndTheCoreProjectsOnly()
    {
        // The language core (the Vocaterra.Nlp.* projects in src/) is referenced by project (D-game-7, R01); the game layer takes no package
        // of its own and never copies core source.
        var project = File.ReadAllText(Path.Combine(GameRoot(), "src", "Vocaterra.Game", "Vocaterra.Game.csproj"));

        var projects = Regex.Matches(project, "<ProjectReference Include=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
        var core = projects.Where(path => path != "../Vocaterra.Simulation/Vocaterra.Simulation.csproj").ToList();
        Assert.NotEmpty(core);
        Assert.All(core, path => Assert.Matches("^\\.\\./Vocaterra\\.Nlp\\.[A-Za-z]+/Vocaterra\\.Nlp\\.[A-Za-z]+\\.csproj$", path));
        Assert.DoesNotContain("<PackageReference", project, StringComparison.Ordinal);
        Assert.DoesNotContain("external", project, StringComparison.Ordinal);
    }

    [Fact]
    public void TheProjectsThatRunTheRecognizerReferenceVoskDirectlyAndKeepSherpasRuntime()
    {
        // build/Vosk.targets (which copies libvosk.so) is imported only into a project that references Vosk itself, and the ONNX Runtime package's
        // native library would overwrite the one sherpa-onnx (Kokoro) needs, so the same projects exclude its native assets.
        var props = File.ReadAllText(Path.Combine(GameRoot(), "Directory.Packages.props"));
        Assert.Contains("<PackageVersion Include=\"Vosk\" Version=\"0.3.38\" />", props, StringComparison.Ordinal);
        foreach (var project in new[]
        {
            Path.Combine("src", "Vocaterra.MonoGame", "Vocaterra.MonoGame.csproj"),
            Path.Combine("tests", "Vocaterra.Game.Tests", "Vocaterra.Game.Tests.csproj"),
            Path.Combine("tests", "Vocaterra.MonoGame.Tests", "Vocaterra.MonoGame.Tests.csproj"),
        })
        {
            var text = File.ReadAllText(Path.Combine(GameRoot(), project));
            Assert.Contains("<PackageReference Include=\"Vosk\" />", text, StringComparison.Ordinal);
            Assert.Contains("<PackageReference Include=\"Microsoft.ML.OnnxRuntime\" ExcludeAssets=\"native\" />", text, StringComparison.Ordinal);
        }
    }

    private static IEnumerable<string> Sources() =>
        Directory.EnumerateFiles(Path.Combine(GameRoot(), "src", "Vocaterra.Game"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static string StripComment(string line)
    {
        var index = line.IndexOf("//", StringComparison.Ordinal);
        return index < 0 ? line : line[..index];
    }

    /// <summary>The repository root (it holds <c>Directory.Packages.props</c>): the first ancestor of the test
    /// output that holds <c>content/game-v1</c>.</summary>
    private static string GameRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "content", "game-v1", "manifest.json")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("content/game-v1 was not found above the test output folder.");
    }
}
