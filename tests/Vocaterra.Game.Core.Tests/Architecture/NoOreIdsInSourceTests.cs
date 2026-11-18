using System.Text.RegularExpressions;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Architecture;

/// <summary>
/// The underground rests on an ore and a rock being rows of data (B11, D-game-34), as a biome is: no file under <c>src/</c> names the id or the block of an ore, or the deep rock or a blob's rock, of the
/// shipped <c>game/underground.json</c> as a string literal, except the block table that owns the names (<c>BlockDefinition</c>), and nothing under <c>World/</c> names an ore's <c>BlockId</c>. So a new
/// ore or rock is a row of the file and a block, and no pass switches on one.
/// </summary>
public sealed partial class NoOreIdsInSourceTests
{
    private const string BlockTable = "src/Vocaterra.Game.Core/Voxel/BlockDefinition.cs";

    [Fact]
    public void Given_TheShippedUnderground_When_TheSourceIsScanned_Then_NoFileButTheBlockTableNamesAnOreOrARockAsAStringLiteral()
    {
        var underground = TestGame.Data.Underground!;
        var names = underground.Ores.SelectMany(static o => new[] { o.Id, o.Block })
            .Concat(underground.Strata.Blobs.Select(static b => b.Block))
            .Append(underground.Strata.Deep)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        Assert.True(names.Count >= 9);
        var found = new List<string>();
        foreach (var file in Directory.EnumerateFiles(Repo.Path("src"), "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(Repo.Root, file).Replace('\\', '/');
            if (relative.Contains("/obj/", StringComparison.Ordinal) || relative.Contains("/bin/", StringComparison.Ordinal) || relative == BlockTable)
            {
                continue;
            }

            var number = 0;
            foreach (var line in File.ReadLines(file))
            {
                number++;
                var code = Comment().Replace(line, string.Empty);
                found.AddRange(Quoted().Matches(code).Select(static m => m.Groups[1].Value).Where(names.Contains).Select(name => $"{relative}:{number}: \"{name}\""));
            }
        }

        Assert.True(found.Count == 0, "An ore's or a rock's id or block is named in the code; it is a row of game/underground.json:\n" + string.Join("\n", found));
    }

    [Fact]
    public void Given_TheWorldFolders_When_TheyAreScanned_Then_NoOreBlockIdIsNamed()
    {
        var found = new List<string>();
        foreach (var file in Directory.EnumerateFiles(Repo.Path("src", "Vocaterra.Game.Core", "World"), "*.cs", SearchOption.AllDirectories))
        {
            var number = 0;
            foreach (var line in File.ReadLines(file))
            {
                number++;
                var code = Comment().Replace(line, string.Empty);
                found.AddRange(OreBlock().Matches(code).Select(m => $"{Path.GetRelativePath(Repo.Root, file)}:{number}: {m.Value}"));
            }
        }

        Assert.True(found.Count == 0, "The world's generation names an ore's block id:\n" + string.Join("\n", found));
    }

    [Fact]
    public void Given_ALineOfCode_When_ItIsScanned_Then_AQuotedIdAndAnOreBlockAreFoundAndCommentsPass()
    {
        Assert.Single(Quoted().Matches("if (name == \"iron-ore\") {"));
        Assert.Single(OreBlock().Matches("var b = BlockId.TinOre;"));
        Assert.Empty(OreBlock().Matches("var b = BlockId.Stone;"));
        Assert.Equal(string.Empty, Comment().Replace("  // BlockId.IronOre", string.Empty).Trim());
    }

    [GeneratedRegex("\"([^\"\\\\]*)\"")]
    private static partial Regex Quoted();

    [GeneratedRegex("\\bBlockId\\.(IronOre|TinOre|CopperOre)\\b")]
    private static partial Regex OreBlock();

    [GeneratedRegex("^\\s*(//|///).*$|\\s//\\s.*$")]
    private static partial Regex Comment();
}
