using System.Text.RegularExpressions;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Architecture;

/// <summary>
/// The procedural world rests on a biome being a row of data (B01, D-game-34): no file under <c>src/</c> names the id of a biome of the shipped data as a string literal, so a new biome
/// is a row of <c>game/biomes.json</c> and no consumer switches on one. There is no allow-list: where a biome's id would equal a word the code uses (the valley's pass names, a scene file's
/// keys), the biome is named another way.
/// </summary>
public sealed partial class NoBiomeIdsInSourceTests
{
    [Fact]
    public void Given_TheShippedBiomes_When_TheSourceIsScanned_Then_NoFileNamesOneAsAStringLiteral()
    {
        var ids = TestGame.Data.Biomes.Rows.Select(static r => r.Id).ToList();
        Assert.True(ids.Count >= 10);
        var found = new List<string>();
        foreach (var file in Directory.EnumerateFiles(Repo.Path("src"), "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(Repo.Root, file).Replace('\\', '/');
            if (relative.Contains("/obj/", StringComparison.Ordinal) || relative.Contains("/bin/", StringComparison.Ordinal))
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

        Assert.True(found.Count == 0, "A biome's id is named in the code; a biome is a row of game/biomes.json:\n" + string.Join("\n", found));
    }

    [Fact]
    public void Given_ALineOfCode_When_ItIsScanned_Then_AQuotedIdIsFoundAndCommentsAndLongerWordsPass()
    {
        Assert.Single(Quoted().Matches("if (id == \"plains\") {"));
        Assert.DoesNotContain(Quoted().Matches("var x = \"plainsong\";"), static m => m.Groups[1].Value == "plains");
        Assert.Equal(string.Empty, Comment().Replace("  // \"plains\" is a biome", string.Empty).Trim());
    }

    [GeneratedRegex("\"([^\"\\\\]*)\"")]
    private static partial Regex Quoted();

    [GeneratedRegex("^\\s*(//|///).*$|\\s//\\s.*$")]
    private static partial Regex Comment();
}
