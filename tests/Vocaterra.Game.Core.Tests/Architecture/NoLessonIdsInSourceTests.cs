using System.Text.RegularExpressions;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Architecture;

/// <summary>
/// The pass's lock rests on the lessons being data (B08b, D-game-34): no file under <c>src/</c> names the id of a lesson of the shipped data as a string literal, so the pass's required
/// lessons are the list in <c>game/world.json</c> and a new lesson is a row of data. The one allowance is the lesson runner's own two constants (<c>LessonRunner.Classes.cs</c>, L01), which predate the pass: the id of the introduction and of the first class that the old saves' split of the first lesson needs.
/// </summary>
public sealed partial class NoLessonIdsInSourceTests
{
    [Fact]
    public void Given_TheShippedLessons_When_TheSourceIsScanned_Then_NoFileNamesOneAsAStringLiteral()
    {
        var ids = TestGame.Data.Lessons.Lessons.Select(static l => l.Id).ToList();
        Assert.True(ids.Count >= 12);
        var found = new List<string>();
        foreach (var file in Directory.EnumerateFiles(Repo.Path("src"), "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(Repo.Root, file).Replace('\\', '/');
            if (relative.Contains("/obj/", StringComparison.Ordinal) || relative.Contains("/bin/", StringComparison.Ordinal) || relative.EndsWith("Language/Lessons/LessonRunner.Classes.cs", StringComparison.Ordinal))
            {
                continue;
            }

            var number = 0;
            foreach (var line in File.ReadLines(file))
            {
                number++;
                var code = Comment().Replace(line, string.Empty);
                found.AddRange(Quoted().Matches(code).Select(static m => m.Groups[1].Value).Where(ids.Contains).Select(id => $"{relative}:{number}: \"{id}\""));
            }
        }

        Assert.True(found.Count == 0, "A lesson's id is named in the code; the pass's lessons are the list in game/world.json:\n" + string.Join("\n", found));
    }

    [Fact]
    public void Given_ALineOfCode_When_ItIsScanned_Then_AQuotedIdIsFoundAndCommentsAndLongerWordsPass()
    {
        Assert.Single(Quoted().Matches("if (id == \"first-words\") {"));
        Assert.DoesNotContain(Quoted().Matches("var x = \"first-words-2\";"), static m => m.Groups[1].Value == "first-words");
        Assert.Equal(string.Empty, Comment().Replace("  // \"plains\" is a lesson", string.Empty).Trim());
    }

    [GeneratedRegex("\"([^\"\\\\]*)\"")]
    private static partial Regex Quoted();

    [GeneratedRegex("^\\s*(//|///).*$|\\s//\\s.*$")]
    private static partial Regex Comment();
}
