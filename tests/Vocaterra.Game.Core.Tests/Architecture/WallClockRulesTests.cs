using System.Text.RegularExpressions;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.Architecture;

/// <summary>
/// No test outside the timing collections asserts on a wall-clock measurement (B29b, the throughput review's section 2: a raw <c>Stopwatch</c> assert failed two of four gates under load, and a wall-clock <c>Settle</c>
/// failed CI on a slow runner). A test that checks speed holds a CPU-time budget in a <c>TimingCollection</c> class (<c>CpuTime</c>); a test that checks an outcome may hold a wall-clock limit only as a hang guard
/// (<c>HangGuard</c>: generous, multiplied by 4 under CI=true, its expiry reported as a hang). The rule is a scan of every test source (the test projects, this one included):
/// <list type="number">
/// <item>a file with no <c>TimingCollection</c> class may not read a wall clock (<c>Stopwatch</c>, <c>Environment.TickCount</c>, <c>.Elapsed</c>, <c>DateTime.Now</c> or <c>UtcNow</c>, <c>MonotonicClock</c>) unless it is named in <see cref="ClockReadAllowed"/> with a reason (a clock handed to code under test, a time that is only reported);</item>
/// <item>no line asserts on a clock reading, allow-listed file or not, unless the line goes through <c>HangGuard</c>;</item>
/// <item>no wall-clock-limited wait (<c>Settle</c>, <c>SettleNear</c>, <c>WaitForOutcomes</c>, <c>WaitUntilReady</c>, <c>WaitForExit</c>, <c>Wait</c> or <c>Join</c> with a literal time) is written with a literal limit: its limit goes through <c>HangGuard</c>;</item>
/// <item>an allow-list entry whose file no longer reads a clock is stale and fails.</item>
/// </list>
/// </summary>
public sealed class WallClockRulesTests(ITestOutputHelper output)
{
    private static readonly Regex ClockRead = new(@"\bStopwatch\b|\bEnvironment\.TickCount|\.Elapsed(Milliseconds|Ticks)?\b|\bDateTime(Offset)?\.(Utc)?Now\b|\bMonotonicClock\b|\bTimeProvider\.System\b", RegexOptions.Compiled);

    private static readonly Regex StringLiteral = new("\"(\\\\.|[^\"\\\\])*\"", RegexOptions.Compiled);

    private static readonly Regex AssertLine = new(@"\bAssert\.(True|False|InRange|Equal|NotEqual)\b", RegexOptions.Compiled);

    // A wait whose limit is a literal (a number, TimeSpan.From..., or a named constant ending in Milliseconds): the literal is the wall-clock bound the rule keeps out of an outcome test.
    private static readonly Regex LiteralWait = new(
        @"\b(WaitUntilReady|WaitForOutcomes|WaitForExit|SettleNear)\(\s*(\d|TimeSpan\.)|\.(Wait|Join)\(\s*(\d|TimeSpan\.From)|\.Settle\([^;]*,\s*\d[\d_]*\s*\)", RegexOptions.Compiled);

    private static readonly string[] SkippedFolders = [$"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"];

    /// <summary>Files that read a clock without asserting on it, each with its one-line reason (paths relative to <c>tests/</c>, with forward slashes).</summary>
    private static readonly Dictionary<string, string> ClockReadAllowed = new(StringComparer.Ordinal)
    {
        ["Vocaterra.Game.Core.Tests/TestSupport/HangGuard.cs"] = "the hang guard itself: the one place a test reads the tick count",
        ["Vocaterra.Game.Core.Tests/Language/Speech/SpokenModelTests.cs"] = "hands Environment.TickCount64 to the turn worker as its clock (code under test takes a clock function); nothing asserts on it",
        ["Vocaterra.Game.Core.Tests/World/Options/TerrainWorldTests.cs"] = "reports the milliseconds the chunks of each terrain option's extreme took to generate (Q02); the assert is on the chunk count, not on the time",
        ["Vocaterra.Game.Core.Tests/Voxel/WaterAtRestTests.cs"] = "reports the slowest water step in milliseconds; the assert is on the step's update count, not its time",
        ["Vocaterra.Game.Tests/Component/VillageFrontendTests.cs"] = "reports the milliseconds a frame takes; the assert is on the bytes allocated, not the time",
        ["Vocaterra.Game.Tests/Component/CheckBatteryTests.cs"] = "hands MonotonicClock.Milliseconds to the check battery as its clock (code under test takes a clock function); nothing asserts on it",
        ["Vocaterra.Game.Tests/Graphics/World/MeshingRig.cs"] = "hands MonotonicClock.Milliseconds to the mesh pipeline as its frame clock (code under test takes the time); nothing asserts on it",
        ["Vocaterra.Nlp.Tests/Asr/SherpaTests.cs"] = "reports the model's load and decode times in the test output; nothing asserts on them",
        ["Vocaterra.Nlp.Tests/Response/Speech/SynthesizerTests.cs"] = "reports the synthesis time in the test output; nothing asserts on it",
        ["Vocaterra.Nlp.Fuzzing.Tests/PronunciationFuzzTests.cs"] = "the fuzz suite reports the stage times it measured (CI only); the asserts are on counts and values",
    };

    [Fact]
    public void NoTestOutsideTheTimingCollectionsAssertsOnAWallClockMeasurement()
    {
        var root = Path.Combine(TestSupport.Repo.Root, "tests");
        var files = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !SkippedFolders.Any(s => f.Contains(s, StringComparison.Ordinal)))
            .Select(f => (Full: f, Relative: Path.GetRelativePath(root, f).Replace('\\', '/')))
            .ToList();
        Assert.True(files.Count > 500, $"only {files.Count} test sources found");

        var unlisted = new List<string>();
        var asserts = new List<string>();
        var literalWaits = new List<string>();
        var used = new HashSet<string>(StringComparer.Ordinal);
        var timingFiles = 0;
        foreach (var (full, relative) in files)
        {
            var lines = File.ReadAllLines(full);
            if (lines.Any(static l => l.Contains("TimingCollection.Name", StringComparison.Ordinal) && l.TrimStart().StartsWith("[Collection(", StringComparison.Ordinal)))
            {
                timingFiles++;
                continue; // a file with a timing class holds its budgets in CPU time there (CpuTime); the budgets' own tests are the timing step's
            }

            var reads = false;
            for (var i = 0; i < lines.Length; i++)
            {
                var line = StripComment(lines[i]);
                var number = i + 1;
                if (ClockRead.IsMatch(line))
                {
                    reads = true;
                    if (AssertLine.IsMatch(line) && !line.Contains("HangGuard", StringComparison.Ordinal))
                    {
                        asserts.Add($"{relative}:{number}: {lines[i].Trim()}");
                    }
                }

                if (LiteralWait.IsMatch(line) && !line.Contains("HangGuard", StringComparison.Ordinal) && !relative.EndsWith("WallClockRulesTests.cs", StringComparison.Ordinal))
                {
                    literalWaits.Add($"{relative}:{number}: {lines[i].Trim()}");
                }
            }

            if (reads)
            {
                _ = used.Add(relative);
                if (!ClockReadAllowed.ContainsKey(relative))
                {
                    unlisted.Add(relative);
                }
            }
        }

        output.WriteLine($"{files.Count} test sources scanned, {timingFiles} with a timing class (skipped), {used.Count} read a clock outside them");
        Assert.True(unlisted.Count == 0, "These test files read a wall clock outside the timing collections. A speed check is a CPU-time budget in a TimingCollection class (CpuTime); a wait is a hang guard (HangGuard); a clock handed to code under test or a time only reported is named in ClockReadAllowed with a reason:\n" + string.Join('\n', unlisted));
        Assert.True(asserts.Count == 0, "These lines assert on a wall-clock measurement outside the timing collections (move the check into a timing class as a CPU-time budget, or make it a hang guard through HangGuard):\n" + string.Join('\n', asserts));
        Assert.True(literalWaits.Count == 0, "These waits have a literal wall-clock limit (scale it with HangGuard.Scale or use HangGuard.Settled, so CI's slower machines and a loaded one do not fail an outcome test):\n" + string.Join('\n', literalWaits));
        var stale = ClockReadAllowed.Keys.Where(k => !used.Contains(k)).ToList();
        Assert.True(stale.Count == 0, "These ClockReadAllowed entries no longer read a wall clock (or the file is gone, or now has a timing class); remove them:\n" + string.Join('\n', stale));
    }

    private static string StripComment(string line)
    {
        line = StringLiteral.Replace(line, "\"\""); // a clock named inside a string (a message, a banned-token list) is not a read
        var at = line.IndexOf("//", StringComparison.Ordinal);
        return at < 0 ? line : line[..at];
    }
}
