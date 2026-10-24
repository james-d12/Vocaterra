using System.Text.RegularExpressions;

namespace Vocaterra.MonoGame.Tests;

/// <summary>The layering rules of plan section 3, checked on the front-end's sources and project file.</summary>
public sealed class ArchitectureRulesTests
{
    private static readonly string[] BannedEverywhere =
    [
        "System.Random", "new Random", "Random.Shared", "Guid.NewGuid", "DateTime.Now", "DateTime.UtcNow", "DateTimeOffset.Now", "DateTimeOffset.UtcNow",
    ];

    private static readonly string[] EngineNamespaces = ["Microsoft.Xna", "FontStashSharp"];

    [Fact]
    public void NoSourceUsesSystemRandomAGuidOrTheWallClock()
    {
        foreach (var file in Sources(string.Empty))
        {
            var code = Code(file);
            foreach (var token in BannedEverywhere.Where(_ => Path.GetFileName(file) != "WallClock.cs")) // the session log's time stamps (S11)
            {
                Assert.False(code.Contains(token, StringComparison.Ordinal), $"{Path.GetFileName(file)} uses {token}.");
            }
        }
    }

    [Fact]
    public void OnlyTheRealFrameClockReadsATimer()
    {
        foreach (var file in Sources(string.Empty).Where(f => Path.GetFileName(f) != "FrameClock.cs"))
        {
            Assert.False(Code(file).Contains("Stopwatch", StringComparison.Ordinal), $"{Path.GetFileName(file)} reads a timer; only FrameClock.cs may.");
        }
    }

    private static IEnumerable<string> Sources(string folder) =>
        Directory.EnumerateFiles(Repo.Path("src", "Vocaterra.MonoGame", folder), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static string Code(string file) => string.Join('\n', File.ReadAllLines(file).Select(StripComment));

    private static string StripComment(string line)
    {
        var index = line.IndexOf("//", StringComparison.Ordinal);
        return index < 0 ? line : line[..index];
    }
}
