using System.Text.RegularExpressions;

namespace Vocaterra.Simulation.Tests;

/// <summary>The determinism and dependency rules of plan section 3, checked on the simulation's sources.</summary>
public sealed class DeterminismRulesTests
{
    private static readonly string[] BannedTokens =
    [
        "System.Random", "new Random", "Random.Shared", "Guid.NewGuid", "DateTime", "DateTimeOffset", "Stopwatch", "Environment.TickCount",
        "Math.Sin", "Math.Cos", "Math.Tan", "Math.Pow", "Math.Exp", "Math.Log", "Math.Sqrt", "MathF.", "Math.Atan", "Math.Floor", "Math.Round",
        "using Microsoft.Xna", "using Godot",
    ];

    [Fact]
    public void SourcesUseNoClockNoSystemRandomAndNoPlatformDependentMath()
    {
        var files = SimulationSources().ToList();
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
    public void ProjectHasNoPackageOrProjectReferences()
    {
        var csproj = File.ReadAllText(Path.Combine(SimulationDirectory(), "Vocaterra.Simulation.csproj"));
        Assert.DoesNotContain("PackageReference", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectReference", csproj, StringComparison.Ordinal);
        Assert.Contains("<TargetFrameworks>net8.0;net10.0</TargetFrameworks>", csproj, StringComparison.Ordinal);
    }

    [Fact]
    public void AssemblyReferencesOnlyTheFramework()
    {
        var references = typeof(GameSimulation).Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();
        Assert.All(references, name => Assert.True(
            name == "System.Runtime" || name.StartsWith("System.", StringComparison.Ordinal) || name == "netstandard" || name == "mscorlib",
            $"Unexpected reference {name}."));
    }

    private static IEnumerable<string> SimulationSources() =>
        Directory.EnumerateFiles(SimulationDirectory(), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static string StripComment(string line) => Regex.Replace(line, @"//.*$", string.Empty);

    /// <summary>The project folder, found by walking up from the test output to the game's folder (the one that holds <c>content/game-v1</c>).</summary>
    private static string SimulationDirectory()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "content", "game-v1", "manifest.json")))
            {
                return Path.Combine(directory.FullName, "src", "Vocaterra.Simulation");
            }
        }

        throw new InvalidOperationException("content/game-v1 was not found above the test assembly.");
    }
}
