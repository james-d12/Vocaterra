using System.Diagnostics;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;
using Vocaterra.Nlp.Core.Determinism;
using Xunit.Abstractions;
using static Vocaterra.Game.Core.Tests.Voxel.StreamedGridSupport;

namespace Vocaterra.Game.Core.Tests.Save;

/// <summary>B05: a save lists the changed chunks by chunk coordinate and restores them over a world built as a new game builds it.</summary>
public sealed class WorldEditsSaveTests(ITestOutputHelper output)
{
    private static string SaveOf(Vocaterra.Game.Core.Session.VoxelSession session) => SaveGame.Save(session);

    [Fact]
    public void Given_BlocksAndWaterEditedOnTheValley_When_SavedAndLoaded_Then_TheWorldComesBackExactly()
    {
        var session = TestGame.NewSession();
        var grid = session.Scene.Grid;
        grid.SetBlock(50, 5, 2, BlockId.Torch);
        grid.SetBlock(19, 5, 19, BlockId.Planks);
        grid.SetBlock(100, 20, 100, BlockId.Water);
        grid.SetWater(100, 20, 100, WaterLevel.Flowing(3));
        var edits = SaveGame.Parse(SaveOf(session)).Chunks;

        var loaded = SaveGame.Load(SaveOf(session), TestGame.Data, TestGame.Route, credit: session.Credit, bumpSession: false);

        Assert.Equal(3, edits.Count);
        Assert.All(edits, static edit => Assert.Equal(VoxelChunk.Volume, edit.Runs.Sum(static r => r.Length)));
        Assert.Equal(grid.ComputeDigestHex(), loaded.Scene.Grid.ComputeDigestHex());
        Assert.Equal(BlockId.Torch, loaded.Scene.Grid.GetBlock(50, 5, 2));
        Assert.Equal(WaterLevel.Flowing(3), loaded.Scene.Grid.GetWater(100, 20, 100));
        Assert.Equal(session.Simulation.ComputeStateDigest(), loaded.Simulation.ComputeStateDigest());
        Assert.Equal(session.Simulation.Inventory.Slots, loaded.Simulation.Inventory.Slots);
        Assert.Equal(CanonicalJson.Serialize(edits), CanonicalJson.Serialize(SaveGame.Parse(SaveOf(loaded)).Chunks)); // saved again from the restored world, the same chunks
    }

    [Fact]
    public void Given_AnEditThatIsUndone_When_TheGameIsSaved_Then_TheChunkIsNotInTheSave()
    {
        var session = TestGame.NewSession();
        var grid = session.Scene.Grid;
        var before = SaveGame.Capture(session).StateDigest;
        var original = grid.GetBlock(50, 5, 2);

        grid.SetBlock(50, 5, 2, BlockId.Torch);
        Assert.NotEmpty(SaveGame.Capture(session).Chunks);
        grid.SetBlock(50, 5, 2, original);

        var document = SaveGame.Capture(session);
        Assert.Empty(document.Chunks);
        Assert.Equal(before, document.StateDigest);
    }

    [Fact]
    public void Given_ASavedGame_When_ItIsLoaded_Then_NoEditedChunkIsLostAndNoOtherChunkIsWritten()
    {
        var session = TestGame.NewSession();
        session.Scene.Grid.SetBlock(50, 5, 2, BlockId.Torch);

        var loaded = SaveGame.Load(SaveOf(session), TestGame.Data, TestGame.Route, credit: session.Credit);

        Assert.Equal([new VoxelChunkCoord(3, 0, 0)], loaded.Scene.Grid.ChangedChunks().Select(static c => c.Coord));
    }

    [Fact]
    public void Given_AStreamedGridEditedBelowZero_When_TheDiffIsCapturedAndApplied_Then_AnotherGridHoldsTheSameWorld()
    {
        var generator = new CountingGenerator(static coord => coord.Y <= 0 ? Floor(BlockId.Stone) : null);
        var edited = new VoxelGrid(WorldLimits.Default, generator);
        edited.SetBlock(-20, -40, 33, BlockId.Dirt); // chunk (-2, -3, 2)
        edited.SetBlock(5, 2, 5, BlockId.Torch);
        _ = edited.GetChunk(new VoxelChunkCoord(9, 0, 9)); // resident and unchanged: not in the diff

        var diff = VoxelGridDiff.Capture(edited);
        var regenerated = new VoxelGrid(WorldLimits.Default, new CountingGenerator(static coord => coord.Y <= 0 ? Floor(BlockId.Stone) : null));
        VoxelGridDiff.Apply(regenerated, CanonicalJson.Deserialize<List<ChunkEdit>>(CanonicalJson.Serialize(diff))); // the hook B06 fills: the chunk source regenerates a chunk on first touch, then the edit is written over it

        Assert.Equal([new VoxelChunkCoord(-2, -3, 2), new VoxelChunkCoord(0, 0, 0)], diff.Select(static e => new VoxelChunkCoord(e.X, e.Y, e.Z)));
        Assert.Equal(BlockId.Dirt, regenerated.GetBlock(-20, -40, 33));
        Assert.Equal(BlockId.Torch, regenerated.GetBlock(5, 2, 5));
        var header = WorldStateHeader.Unkeyed(1);
        Assert.Equal(WorldStateDigest.ComputeHex(edited, header), WorldStateDigest.ComputeHex(regenerated, header));
    }

    [Fact]
    public void Given_ARestoredWorld_When_TheDigestDisagrees_Then_TheSaveIsRefused()
    {
        var session = TestGame.NewSession();
        session.Scene.Grid.SetBlock(50, 5, 2, BlockId.Torch);
        var document = SaveGame.Parse(SaveOf(session));
        var other = document with { Chunks = [] }; // the edit is lost

        _ = Assert.Throws<SaveException>(() => SaveGame.Restore(other, TestGame.Data, TestGame.Route()));
    }

    [Fact]
    public void Given_TheValley_When_ItIsSavedAndLoaded_Then_TheTimesAreReported()
    {
        var session = TestGame.NewSession();
        session.Scene.Grid.SetBlock(50, 5, 2, BlockId.Torch);
        _ = SaveOf(session); // warms the code paths

        var watch = Stopwatch.StartNew();
        var text = SaveOf(session);
        var saved = watch.Elapsed;
        watch.Restart();
        _ = SaveGame.Load(text, TestGame.Data, TestGame.Route, credit: session.Credit);
        var loaded = watch.Elapsed;

        output.WriteLine($"save {saved.TotalMilliseconds:F1} ms, load (scene build + restore) {loaded.TotalMilliseconds:F1} ms, {text.Length:N0} characters");
        Assert.True(saved < TimeSpan.FromSeconds(2), $"The save took {saved.TotalMilliseconds:F0} ms.");
    }
}
