using System.Text.RegularExpressions;

namespace Vocaterra.Game.Core.Tests.Architecture;

/// <summary>The layering and determinism rules of plan section 3, checked on the sources and the project file of <c>Vocaterra.Game.Core</c>.</summary>
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

    /// <summary>
    /// Threads, timers, file IO and environment reads: banned from Game.Core except in the files below (B24, review A1). The game's logic is a pure function of its inputs; what must
    /// run beside the frame or touch the disk is named here with its reason, so a new use fails this test until someone says why it belongs.
    /// </summary>
    private static readonly (string Pattern, string What)[] BannedConcurrencyAndIo =
    [
        (@"\bnew Thread\b|\bThread\.(?!Sleep)|\bThreadPool\b|\bThreadLocal\b|\bThreadStatic\b", "a thread"),
        (@"\bParallel\.|\bTask\.|\bnew Task\b|\bTask<|\bPLINQ\b|\.AsParallel\(|\basync\b|\bawait\b", "a task or a parallel loop"),
        (@"\bPeriodicTimer\b|\bTimer\b|\bSystem\.Timers\b", "a timer"),
        (@"\bFile\.|\bDirectory\.|\bFileStream\b|\bStreamWriter\b|\bStreamReader\b|\bFileInfo\b|\bDirectoryInfo\b", "file IO"),
        (@"\bEnvironment\.|\bAppContext\.BaseDirectory\b|\bProcess\.", "an environment read"),
    ];

    /// <summary>The files allowed to use what <see cref="BannedConcurrencyAndIo"/> bans, each with its one-line reason (paths relative to <c>src/Vocaterra.Game.Core</c>).</summary>
    private static readonly Dictionary<string, string> ConcurrencyAndIoAllowed = new(StringComparer.Ordinal)
    {
        ["Session/TurnWorker.cs"] = "the turn worker's own thread runs the recognizer and the language engine off the game thread, and reads the recorded WAV it is handed",
        ["World/Generation/ValleyUnderground.cs"] = "the bounded valley's underground is laid column by column on at most two threads (B24a, the two-worker rule); the result is identical at any thread count (tested)",
        ["Save/SaveFile.cs"] = "the save file: written to a temporary file and moved over the old one, read back whole",
        ["Content/LocaleBundle.cs"] = "loads the locale bundle from its folder, beside the game bundle",
        ["Session/GameBundle.cs"] = "finds the game bundle's folder: the environment variable, then the folders above the program",
        ["Language/Speech/SpeechEngine.cs"] = "finds the model store and the manifest (environment variables, the cache folder) and checks the model folders exist",
        ["Reporting/BuildCommit.cs"] = "reads the build's commit from the environment (a function the caller can replace in tests)",
        ["Sound/SoundMixer.cs"] = "opens the optional sound log the mixer is asked to write",
        ["World/Layout/SceneLayout.cs"] = "SceneLayout.Load reads a layout file for the dev options and tests; Parse, which the game uses, takes text",
    };

    [Fact]
    public void GameCoreUsesNoThreadTimerFileOrEnvironmentExceptWhereNamedWithAReason()
    {
        var root = Path.Combine(GameRoot(), "src", "Vocaterra.Game.Core");
        var found = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (var file in Sources())
        {
            var relative = Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
            foreach (var line in File.ReadLines(file))
            {
                var code = StripStrings(StripComment(line));
                foreach (var (pattern, what) in BannedConcurrencyAndIo)
                {
                    if (Regex.IsMatch(code, pattern))
                    {
                        if (!found.TryGetValue(relative, out var kinds))
                        {
                            found[relative] = kinds = [];
                        }

                        _ = kinds.Add(what);
                    }
                }
            }
        }

        var unlisted = found.Where(f => !ConcurrencyAndIoAllowed.ContainsKey(f.Key)).Select(f => $"{f.Key}: {string.Join(", ", f.Value)}").ToList();
        Assert.True(unlisted.Count == 0, "Game.Core may not use these (name the file in ConcurrencyAndIoAllowed with a reason, or remove the use):\n" + string.Join("\n", unlisted));
        var stale = ConcurrencyAndIoAllowed.Keys.Where(k => !found.ContainsKey(k)).ToList();
        Assert.True(stale.Count == 0, "These allow-list entries no longer use anything banned; remove them: " + string.Join(", ", stale));
    }

    [Fact]
    public void TheConcurrencyAndIoScan_FindsEachBannedKindInCodeAndNotInCommentsOrStrings()
    {
        foreach (var code in new[] { "var t = new Thread(Run);", "ThreadPool.QueueUserWorkItem(x);", "Parallel.For(0, 1, i => { });", "Task.Run(() => 1);", "await x;", "var t = new Timer(c);", "File.ReadAllText(p);", "Directory.Exists(p);", "Environment.GetEnvironmentVariable(n);" })
        {
            Assert.Contains(BannedConcurrencyAndIo, b => Regex.IsMatch(code, b.Pattern));
        }

        foreach (var quiet in new[] { "// a Thread and a File.", "var s = \"Parallel.For and File.Exists\";", "var task = 1; var threads = 2; var File = 3;" })
        {
            var stripped = StripStrings(StripComment(quiet));
            Assert.DoesNotContain(BannedConcurrencyAndIo, b => Regex.IsMatch(stripped, b.Pattern));
        }
    }

    private static string StripStrings(string code) => Regex.Replace(code, @"""(?:[^""\\]|\\.)*""", "\"\"");

    [Fact]
    public void TheProjectReferencesTheCoreProjectsOnly()
    {
        // The language core (the Vocaterra.Nlp.* projects in src/) is referenced by project (D-game-7, R01); the game layer takes no package
        // of its own and never copies core source.
        var project = File.ReadAllText(Path.Combine(GameRoot(), "src", "Vocaterra.Game.Core", "Vocaterra.Game.Core.csproj"));

        var projects = Regex.Matches(project, "<ProjectReference Include=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
        var core = projects;
        Assert.NotEmpty(core);
        // The core is the one engine project Vocaterra.Nlp (M52, ADR 0012), or a Vocaterra.Nlp.<Name> project for a pack or a tool.
        Assert.All(core, path => Assert.Matches("^\\.\\./Vocaterra\\.Nlp(\\.[A-Za-z]+)?/Vocaterra\\.Nlp(\\.[A-Za-z]+)?\\.csproj$", path));
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
            Path.Combine("src", "Vocaterra.Game", "Vocaterra.Game.csproj"),
            Path.Combine("tests", "Vocaterra.Game.Core.Tests", "Vocaterra.Game.Core.Tests.csproj"),
            Path.Combine("tests", "Vocaterra.Game.Tests", "Vocaterra.Game.Tests.csproj"),
        })
        {
            var text = File.ReadAllText(Path.Combine(GameRoot(), project));
            Assert.Contains("<PackageReference Include=\"Vosk\" />", text, StringComparison.Ordinal);
            Assert.Contains("<PackageReference Include=\"Microsoft.ML.OnnxRuntime\" ExcludeAssets=\"native\" />", text, StringComparison.Ordinal);
        }
    }

    private static IEnumerable<string> Sources() =>
        Directory.EnumerateFiles(Path.Combine(GameRoot(), "src", "Vocaterra.Game.Core"), "*.cs", SearchOption.AllDirectories)
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
