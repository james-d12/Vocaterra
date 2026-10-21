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
    public void TheProjectReferencesTheSimulationAndOnlyCorePackages()
    {
        var project = File.ReadAllText(Path.Combine(RepoRoot(), "src", "Vocaterra.Game", "Vocaterra.Game.csproj"));

        var projects = Regex.Matches(project, "<ProjectReference Include=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(["../Vocaterra.Simulation/Vocaterra.Simulation.csproj"], projects);
        var packages = Regex.Matches(project, "<PackageReference Include=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
        Assert.NotEmpty(packages);
        Assert.All(packages, name => Assert.StartsWith("Vocaterra.Nlp.", name, StringComparison.Ordinal));
        Assert.DoesNotContain("external", project, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSimulationStillTakesNoPackages()
    {
        var project = File.ReadAllText(Path.Combine(RepoRoot(), "src", "Vocaterra.Simulation", "Vocaterra.Simulation.csproj"));

        Assert.DoesNotContain("PackageReference", project, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectReference", project, StringComparison.Ordinal);
    }

    private static IEnumerable<string> Sources() =>
        Directory.EnumerateFiles(Path.Combine(RepoRoot(), "src", "Vocaterra.Game"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static string StripComment(string line)
    {
        var index = line.IndexOf("//", StringComparison.Ordinal);
        return index < 0 ? line : line[..index];
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Vocaterra.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Vocaterra.sln was not found above the test output folder.");
    }
}
