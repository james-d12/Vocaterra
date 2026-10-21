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

    private static string SimulationDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Vocaterra.sln")))
        {
            directory = directory.Parent;
        }

        return directory is null
            ? throw new InvalidOperationException("Vocaterra.sln was not found above the test assembly.")
            : Path.Combine(directory.FullName, "src", "Vocaterra.Simulation");
    }
}
