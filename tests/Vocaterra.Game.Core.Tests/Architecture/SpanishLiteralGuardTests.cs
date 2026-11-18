using System.Text.RegularExpressions;
using Vocaterra.Game.Core.Language.Words;

namespace Vocaterra.Game.Core.Tests.Architecture;

/// <summary>
/// T02 (D-game-27, G4 and G5): the Spanish words the game uses are data (<c>game/words.json</c> and the bundle's files), not code. A Spanish word of that data
/// that appears as a string literal of <c>Vocaterra.Game.Core</c>, <c>Vocaterra.Game</c> or <c>Vocaterra.Host</c> fails here. The allow-list names each
/// literal the code keeps on purpose, with its reason: a phrase id (a key of <c>phrases.json</c>), a developer's diagnostic line, or an English word that
/// happens to be a Spanish word too. Comments and the tests are not the target.
/// </summary>
public sealed class SpanishLiteralGuardTests
{
    private static readonly string[] Projects = ["Vocaterra.Game.Core", "Vocaterra.Game", "Vocaterra.Host"];

    // The literals the code keeps, by file (relative to src/) and the literal as written (without its quotes, "*" for every literal of the file), each with
    // the reason. Kept: a phrase id (a key of phrases.json), a log or validator line, the English word yes/no, a JSON key, a locale code. T02b moved every
    // other Spanish word the code tested into game/words.json; an entry added here needs a reason that is not a Spanish word the game tests.
    private static readonly Dictionary<(string File, string Literal), string> Allowed = new()
    {
        [("Vocaterra.Game/Startup/LaunchOptions.cs", "*")] = "the launch options' usage text and examples: a developer's test aids, never a learner's words",
        [("Vocaterra.Game.Core/Content/GameDataValidator.cs", "*")] = "validator messages naming a file's problem (a developer's text, not learner-facing)",
        [("Vocaterra.Game.Core/Language/Checks/CheckBattery.cs", "*")] = "the check battery's console log line (the scripted runs compare the log; developer text)",
        [("Vocaterra.Game.Core/Language/Learner/LearnerSummary.cs", "*")] = "the learner summary's log line (developer text)",
        [("Vocaterra.Game.Core/Language/Phrases/PhraseProgress.cs", "*")] = "the phrase progress log and dump lines (developer text)",
        [("Vocaterra.Game.Core/Content/LocaleBundle.cs", "en")] = "the default locale code (an ISO code, not a Spanish word)",
        [("Vocaterra.Game.Core/Language/Words/GameWords.cs", "no")] = "the JSON key of classAnswers.no (a key, not a word the game says)",
        [("Vocaterra.Game.Core/Reporting/CrashSource.cs", "no")] = "the English yes/no of the crash report's table",
        [("Vocaterra.Game.Core/Reporting/ReportMarkdown.cs", "no")] = "the English yes/no of the report's Markdown",
        [("Vocaterra.Game.Core/Npc/NpcHostEvents.cs", "hola")] = "PhraseIds.Hello: a phrase id (a key of phrases.json)",
        [("Vocaterra.Game.Core/Npc/NpcHostEvents.cs", "gracias")] = "PhraseIds.Thanks: a phrase id (a key of phrases.json)",
        [("Vocaterra.Game.Core/Npc/NpcHostEvents.cs", "ayuda")] = "PhraseIds.Help: a phrase id (a key of phrases.json)",
        [("Vocaterra.Game.Core/Items/InventoryPanel.cs", "bolsa")] = "the catalogue lemma that names the inventory panel (D-game-35 (b), I04b): a lookup key into game/nouns.json, which holds the word, its article and its English; nothing is tested against it",
        [("Vocaterra.Game.Core/Npc/NpcHostEvents.cs", "no")] = "PhraseIds.No: a phrase id (a key of phrases.json)",
    };

    [Fact]
    public void NoSpanishWordOfTheDataIsAStringLiteralOfTheGameCode()
    {
        var words = DataWords();
        Assert.True(words.Count > 20, "the data's words were not read");

        var offences = new List<string>();
        var files = 0;
        foreach (var file in Sources())
        {
            files++;
            var relative = Path.GetRelativePath(Path.Combine(Root(), "src"), file).Replace('\\', '/');
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].TrimStart().StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                var code = lines[i].Contains("//", StringComparison.Ordinal) ? lines[i][..lines[i].IndexOf("//", StringComparison.Ordinal)] : lines[i];
                foreach (Match match in Literal.Matches(code))
                {
                    var text = match.Groups[1].Value;
                    if (!IsDataWord(text, words) || Allowed.ContainsKey((relative, text)) || Allowed.ContainsKey((relative, "*")))
                    {
                        continue;
                    }

                    offences.Add($"{relative}:{i + 1} has the literal \"{text}\", a word of game/words.json");
                }
            }
        }

        Assert.True(files > 100, "the sources were not found");
        Assert.Empty(offences);
    }

    [Fact]
    public void EveryAllowedLiteralIsStillInTheCodeWithAReason()
    {
        var root = Path.Combine(Root(), "src");
        foreach (var ((file, literal), reason) in Allowed)
        {
            Assert.False(string.IsNullOrWhiteSpace(reason), $"{file} \"{literal}\" has no reason");
            Assert.True(File.Exists(Path.Combine(root, file)), $"{file} is not a source file");
            Assert.True(literal == "*" || File.ReadAllText(Path.Combine(root, file)).Contains($"\"{literal}\"", StringComparison.Ordinal), $"{file} no longer has \"{literal}\"");
        }
    }

    // The words of the data: each value of game/words.json (the grammar check's list and the matching lists) whole, and each of its letter-runs.
    private static HashSet<string> DataWords()
    {
        var words = new HashSet<string>(StringComparer.Ordinal);
        var parsed = GameWords.Parse(File.ReadAllText(Path.Combine(Root(), "content", "game-v1", "game", "words.json")));
        foreach (var (_, text) in parsed.All().Concat(parsed.MatchingEntries())) // the matching lists too (T02b): the words a line is read by
        {
            var whole = Normal(text);
            if (whole.Length >= 2)
            {
                words.Add(whole);
            }

            foreach (Match run in Runs.Matches(text))
            {
                var token = run.Value.ToLowerInvariant();
                if (token.Length >= 2)
                {
                    words.Add(token);
                }
            }
        }

        return words;
    }

    // A literal is a data word when it is one (whole, after its punctuation) or, when it has an accent, one of its letter-runs is.
    private static bool IsDataWord(string literal, HashSet<string> words)
    {
        var whole = Normal(literal);
        if (whole.Length >= 2 && words.Contains(whole))
        {
            return true;
        }

        return Runs.Matches(literal).Any(run => run.Value.Any(static c => c > 127 && char.IsLetter(c)) && words.Contains(run.Value.ToLowerInvariant()));
    }

    private static string Normal(string text) => text.Trim().Trim('.', ',', ';', ':', '!', '?', '¡', '¿').ToLowerInvariant();

    private static readonly Regex Literal = new("\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.CultureInvariant);

    private static readonly Regex Runs = new(@"\p{L}+", RegexOptions.CultureInvariant);

    private static IEnumerable<string> Sources() =>
        Projects.SelectMany(project => Directory.EnumerateFiles(Path.Combine(Root(), "src", project), "*.cs", SearchOption.AllDirectories))
            .Where(static f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static string Root()
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
