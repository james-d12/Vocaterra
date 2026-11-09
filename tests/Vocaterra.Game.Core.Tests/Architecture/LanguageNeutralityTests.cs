using System.Text.RegularExpressions;
using Vocaterra.Game.Core.Session;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Es;

namespace Vocaterra.Game.Core.Tests.Architecture;

/// <summary>
/// T01 (D-game-27): the game is language-neutral in code. Outside <see cref="GameLanguages"/>, no source file of <c>Vocaterra.Game.Core</c>, <c>Vocaterra.Game</c> or
/// <c>Vocaterra.Host</c> names the Spanish pack's namespace or one of the <c>Spanish*</c> types of the core or the pack; the game goes through the loaded bundle's
/// neutral members (<c>Language</c>, <c>LanguageMorphology</c>). Spanish words in comments and data, and the Spanish literals still in code (T02's), are not the target.
/// </summary>
public sealed class LanguageNeutralityTests
{
    private static readonly string[] Projects = ["Vocaterra.Game.Core", "Vocaterra.Game", "Vocaterra.Host"];

    [Fact]
    public void NoSourceOutsideGameLanguagesNamesTheSpanishPackOrASpanishType()
    {
        var spanishTypes = new[] { typeof(SpanishLanguagePack).Assembly, typeof(ContentBundle).Assembly }
            .SelectMany(static assembly => assembly.GetTypes())
            .Select(static type => type.Name)
            .Where(static name => name.StartsWith("Spanish", StringComparison.Ordinal))
            .Append("SpanishMorphology") // the extension method of SpanishContent
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains("SpanishNumbers", spanishTypes);
        var pattern = new Regex(@"\bVocaterra\.Nlp\.Es\b|\b(" + string.Join('|', spanishTypes.Select(Regex.Escape)) + @")\b");

        var offences = new List<string>();
        var files = 0;
        foreach (var file in Sources())
        {
            files++;
            if (Path.GetFileName(file) == "GameLanguages.cs")
            {
                continue;
            }

            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var code = lines[i].Contains("//", StringComparison.Ordinal) ? lines[i][..lines[i].IndexOf("//", StringComparison.Ordinal)] : lines[i];
                if (pattern.Match(code) is { Success: true } match)
                {
                    offences.Add($"{Path.GetRelativePath(Root(), file)}:{i + 1} names {match.Value}");
                }
            }
        }

        Assert.True(files > 100, "the sources were not found");
        Assert.Empty(offences);
    }

    [Fact]
    public void OnlyGameCoreReferencesTheSpanishPackProject()
    {
        foreach (var project in Projects.Where(static p => p != "Vocaterra.Game.Core"))
        {
            var text = File.ReadAllText(Path.Combine(Root(), "src", project, project + ".csproj"));
            Assert.DoesNotContain("Vocaterra.Nlp.Es", text, StringComparison.Ordinal);
        }
    }

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
