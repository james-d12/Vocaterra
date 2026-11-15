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

    // file (from the repository root) -> member -> why the use stays and which milestone removes it.
    private static readonly Dictionary<string, Dictionary<string, string>> Allowed = new()
    {
        ["src/Vocaterra.Game.Core/Save/SaveGame.cs"] = new() { ["ChunksX"] = "B05: the save's WorldDims are the dense chunk counts; saves version 9 key edits by chunk coordinate", ["ChunksY"] = "B05: as ChunksX", ["ChunksZ"] = "B05: as ChunksX" },
        ["src/Vocaterra.Game.Core/Save/SaveMigration.cs"] = new() { ["ChunksX"] = "B05: decodes the dense chunk index of a version 8 save", ["ChunksY"] = "B05: as ChunksX", ["ChunksZ"] = "B05: as ChunksX" },
        ["src/Vocaterra.Game.Core/Save/VoxelGridDiff.cs"] = new() { ["Chunks"] = "B05: the diff against a pristine copy walks the dense chunk list; the changed-chunk digest replaces it" },
        ["src/Vocaterra.Game.Core/Simulation/VoxelSimulation.cs"] = new() { ["ComputeDigest"] = "B05: the state digest hashes the whole grid; it becomes the digest of the changed chunks" },
        ["src/Vocaterra.Game.Core/Needs/FireRings.cs"] = new() { ["BlocksOfKind"] = "B06: fire rings come from the region template, not from a scan of the world" },
        ["src/Vocaterra.Game.Core/Session/VoxelSession.cs"] = new() { ["BlocksOfKind"] = "B06: the fire rings again, from the region template" },
        ["src/Vocaterra.Game.Core/Land/LandRules.cs"] = new() { ["BlocksOfKind"] = "B06: the protection mask is built from the valley's blocks; the region template supplies them" },
        ["src/Vocaterra.Game/Graphics/Weather/ColumnCover.cs"] = new() { ["ChunksX"] = "B04b: the rain cover's tiles are a dense array", ["ChunksY"] = "B04b: as ChunksX", ["ChunksZ"] = "B04b: as ChunksX" },
        ["src/Vocaterra.Game/Graphics/World/ChunkVersionTracker.cs"] = new() { ["Chunks"] = "B04b: the tracker's arrays are indexed by the dense chunk index", ["ChunksX"] = "B04b: as Chunks", ["ChunksY"] = "B04b: as Chunks", ["ChunksZ"] = "B04b: as Chunks" },
        ["src/Vocaterra.Game/Graphics/World/ChunkViewSet.cs"] = new() { ["Chunks"] = "B04b: the view set counts and indexes the dense chunk list", ["ChunksX"] = "B04b: as Chunks", ["ChunksY"] = "B04b: as Chunks", ["ChunksZ"] = "B04b: as Chunks" },
        ["src/Vocaterra.Game/Graphics/World/VoxelRenderer.cs"] = new() { ["Chunks"] = "B04b: the renderer's chunk arrays are indexed by the dense chunk index" },
        ["src/Vocaterra.Game/VoxelGame.cs"] = new() { ["ComputeDigestHex"] = "B04b: the start-up line prints the dense world digest" },
    };

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
    public void Given_TheAllowList_When_ItsReasonsAreRead_Then_EachNamesTheMilestoneThatRemovesIt() =>
        Assert.All(Allowed.SelectMany(f => f.Value), entry => Assert.Matches("^B0(4b|5|6): ", entry.Value));

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
