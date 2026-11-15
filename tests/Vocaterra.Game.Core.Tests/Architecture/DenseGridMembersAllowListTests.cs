using System.Text.RegularExpressions;

namespace Vocaterra.Game.Core.Tests.Architecture;

/// <summary>
/// The dense members of <c>VoxelGrid</c> (B04a, biomes plan 3.7): <c>Chunks</c>, <c>ChunksX</c>, <c>ChunksY</c>, <c>ChunksZ</c>, <c>BlocksOfKind</c>, <c>ComputeDigest</c> and
/// <c>ComputeDigestHex</c> are valid on bounded grids only (a streamed grid refuses them), so every use in <c>src/</c> is listed here with the milestone that removes it.
/// A use that is not listed fails; a listed use that has gone fails too, so an entry leaves this list in the milestone that makes it unnecessary.
/// </summary>
public sealed class DenseGridMembersAllowListTests
{
    // A use is a dense member read off something named like a grid (grid, Grid, _grid, scene.Grid) or off the diff's edited, fresh and target grids.
    private static readonly Regex Use = new(@"(?<![\w])(?:\w*[Gg]rid|edited|fresh|target)\.(Chunks[XYZ]?|BlocksOfKind|ComputeDigest(?:Hex)?)\b", RegexOptions.Compiled);

    // file (from the repository root) -> member -> why the use stays and which later milestone removes it. Empty since B06: the fire rings and the land rules read the region template
    // (Scene.Template), so nothing in src enumerates a grid's chunks. A new entry must name a milestone after B06 that removes it.
    private static readonly Dictionary<string, Dictionary<string, string>> Allowed = new();

    [Fact]
    public void Given_TheSources_When_TheDenseGridMembersAreSearched_Then_EveryUseIsListedWithTheMilestoneThatRemovesIt()
    {
        var root = RepositoryRoot();
        var found = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
            if (relative.Contains("/obj/", StringComparison.Ordinal) || relative.Contains("/bin/", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var line in File.ReadAllLines(file))
            {
                var cut = line.IndexOf("//", StringComparison.Ordinal);
                foreach (Match match in Use.Matches(cut < 0 ? line : line[..cut]))
                {
                    if (!found.TryGetValue(relative, out var members))
                    {
                        found[relative] = members = new SortedSet<string>(StringComparer.Ordinal);
                    }

                    members.Add(match.Groups[1].Value);
                }
            }
        }

        var unlisted = found.SelectMany(f => f.Value.Select(m => $"{f.Key}: {m}")).Where(use => !Listed(use)).ToList();
        Assert.True(unlisted.Count == 0, "Dense grid members used without an allow-list entry (a streamed grid refuses them; read by coordinate instead, or list the use with the milestone that removes it):\n" + string.Join('\n', unlisted));

        var gone = Allowed.SelectMany(f => f.Value.Select(m => (File: f.Key, Member: m.Key))).Where(e => !found.TryGetValue(e.File, out var members) || !members.Contains(e.Member)).ToList();
        Assert.True(gone.Count == 0, "Allow-list entries whose use has gone (delete them):\n" + string.Join('\n', gone.Select(e => $"{e.File}: {e.Member}")));
    }

    [Fact]
    public void Given_TheAllowList_When_ItsReasonsAreRead_Then_EachNamesALaterMilestoneThanB06ThatRemovesIt() =>
        Assert.All(Allowed.SelectMany(f => f.Value), entry => Assert.Matches(@"^B(0[7-9]|[1-9]\d): ", entry.Value));

    [Theory]
    [InlineData("B05: a stale tag", false)]
    [InlineData("B06: the milestone that already landed", false)]
    [InlineData("B07: a milestone still to come", true)]
    [InlineData("B12: a later one", true)]
    [InlineData("later", false)]
    public void Given_AReason_When_ItIsChecked_Then_OnlyAMilestoneAfterB06IsAccepted(string reason, bool accepted) =>
        Assert.Equal(accepted, System.Text.RegularExpressions.Regex.IsMatch(reason, @"^B(0[7-9]|[1-9]\d): "));

    [Fact]
    public void Given_ThePatternOfTheDenseMembers_When_ItIsRunOverNewUses_Then_ItStillFindsThem()
    {
        // The list is empty, so the guard must still fail on a new use of a dense member: the pattern has to see each of them off a grid-like name.
        foreach (var line in new[] { "var a = grid.BlocksOfKind(kind);", "var b = scene.Grid.Chunks;", "var c = _grid.ChunksX;", "var d = world.Grid.ComputeDigestHex();", "var e = fresh.ComputeDigest();" })
        {
            Assert.Matches(Use, line);
        }

        Assert.DoesNotMatch(Use, "var f = template.Of(kind);");
    }

    private static bool Listed(string use)
    {
        var split = use.IndexOf(": ", StringComparison.Ordinal);
        return Allowed.TryGetValue(use[..split], out var members) && members.ContainsKey(use[(split + 2)..]);
    }

    private static string RepositoryRoot()
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
