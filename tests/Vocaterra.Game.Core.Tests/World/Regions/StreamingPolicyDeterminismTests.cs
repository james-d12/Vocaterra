using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Core.World.Streaming;

namespace Vocaterra.Game.Core.Tests.World.Regions;

/// <summary>
/// Residency never leaks into the result (B06, the plan's risk 6): the same walk and the same edits under different streaming policies (how far to load, how far to keep, whether to evict at all) give the
/// same state digest and the same save.
/// </summary>
public sealed class StreamingPolicyDeterminismTests
{
    // The player's walk, out of the valley through the pass and round the ring and back, in block positions.
    private static readonly (int X, int Y, int Z)[] Walk =
    [
        (200, 70, 200), (320, 70, 215), (380, 60, 208), (420, 60, 208), (470, 70, 208), (600, 80, 260), (700, 90, 300), (520, 80, 90), (100, 60, -100), (-300, 70, 50), (200, 60, -250), (200, 70, 200),
    ];

    // The edits: a step of the walk and a cell, in the valley, at its border, outside, far outside and under it; the last puts back what an earlier one changed.
    private static readonly (int Step, int X, int Y, int Z, BlockId? Block)[] Edits =
    [
        (0, 210, 62, 200, BlockId.Planks),
        (2, 386, 60, 210, BlockId.Planks),
        (3, 440, 70, 208, BlockId.Planks),
        (4, 475, 72, 215, BlockId.Stone),
        (5, 610, 85, 255, BlockId.Planks),
        (6, 440, 70, 208, null), // puts back what step 3 replaced: that chunk is unchanged again
        (7, 515, 30, 95, BlockId.Planks),
        (8, 95, -10, -95, BlockId.Planks),
        (9, -300, 40, 50, BlockId.Stone),
        (11, 205, 61, 201, BlockId.Planks),
    ];

    private static (string Digest, IReadOnlyList<string> Save, int Resident) Run(StreamPolicy policy, bool evict)
    {
        var scene = RegionTestWorld.StreamedScene();
        var grid = scene.Grid;
        var streamer = new ChunkStreamer(policy, grid.Limits);
        var header = WorldStateHeader.For(TestGame.Bundle, 42, scene.TemplateDigest);
        var original = new Dictionary<(int, int, int), BlockId>();
        for (var step = 0; step < Walk.Length; step++)
        {
            var (x, y, z) = Walk[step];
            var player = ChunkStreamer.ChunkOf(x, y, z);
            foreach (var chunk in streamer.Wanted(player))
            {
                _ = grid.GetChunk(chunk);
            }

            if (evict)
            {
                foreach (var chunk in streamer.Evictable(grid.ResidentCoordinates(), player))
                {
                    _ = grid.TryEvictChunk(chunk);
                }
            }

            foreach (var (_, ex, ey, ez, block) in Edits.Where(e => e.Step == step))
            {
                if (!original.ContainsKey((ex, ey, ez)))
                {
                    original[(ex, ey, ez)] = grid.GetBlock(ex, ey, ez);
                }

                _ = grid.SetBlock(ex, ey, ez, block ?? original[(ex, ey, ez)]);
            }
        }

        var save = VoxelGridDiff.Capture(grid)
            .Select(static e => $"{e.X},{e.Y},{e.Z}:{string.Join(';', e.Runs)}|{(e.Water is null ? string.Empty : string.Join(';', e.Water))}")
            .ToList();
        return (WorldStateDigest.ComputeHex(grid, header), save, grid.ResidentChunkCount);
    }

    [Fact]
    public void Given_TheSameWalkAndEdits_When_ItRunsUnderDifferentPolicies_Then_TheStateDigestAndTheSaveAreIdentical()
    {
        var tight = Run(new StreamPolicy(2, 2, 1, 1), evict: true);
        var wide = Run(new StreamPolicy(5, 8, 2, 2), evict: true);
        var keepAll = Run(new StreamPolicy(3, 100, 4, 4), evict: false);

        Assert.NotEqual(tight.Resident, wide.Resident); // the policies really did keep different chunks
        Assert.NotEqual(wide.Resident, keepAll.Resident);
        Assert.Equal(tight.Digest, wide.Digest);
        Assert.Equal(tight.Digest, keepAll.Digest);
        Assert.Equal(tight.Save, wide.Save);
        Assert.Equal(tight.Save, keepAll.Save);
        Assert.NotEmpty(tight.Save);
        Assert.Equal(8, tight.Save.Count); // the edited chunks: step 3's was put back, and some edits share a chunk
    }

    [Fact]
    public void Given_NoEdits_When_AWalkIsStreamedUnderOnePolicy_Then_TheStateDigestIsTheFreshWorldsWhateverWasLoaded()
    {
        var fresh = RegionTestWorld.StreamedScene();
        var header = WorldStateHeader.For(TestGame.Bundle, 42, fresh.TemplateDigest);
        var before = WorldStateDigest.ComputeHex(fresh.Grid, header);

        foreach (var chunk in new ChunkStreamer(new StreamPolicy(4, 4, 2, 2), fresh.Grid.Limits).Wanted(new VoxelChunkCoord(30, 3, 13)))
        {
            _ = fresh.Grid.GetChunk(chunk);
        }

        Assert.True(fresh.Grid.ResidentChunkCount > 100);
        Assert.Equal(before, WorldStateDigest.ComputeHex(fresh.Grid, header));
    }
}
