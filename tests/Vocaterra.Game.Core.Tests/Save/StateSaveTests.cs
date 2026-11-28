using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Tests.Voxel;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Nlp.Core.Determinism;

namespace Vocaterra.Game.Core.Tests.Save;

/// <summary>The per-cell state byte in a save (K01, version 12): the diff lists a chunk whose only change is a state, writes it as runs and rebuilds it exactly; a world with no state saves none; older saves are refused.</summary>
public sealed class StateSaveTests
{
    private const int Ground = VoxelTestSupport.GroundTop + 1;

    private static (VoxelGrid Edited, VoxelGrid Fresh) Stated()
    {
        var fresh = VoxelTestSupport.FlatScene(g => g.SetBlock(20, Ground, 20, BlockId.Planks)).Grid;
        var edited = fresh.Clone();
        edited.SetBlock(20, Ground, 20, BlockId.Planks, 5);
        return (edited, fresh);
    }

    [Fact]
    public void Given_AChunkWhoseOnlyChangeIsAState_When_TheDiffIsTaken_Then_TheChunkIsListedWithItsStateRun()
    {
        var (edited, _) = Stated();

        var edit = Assert.Single(VoxelGridDiff.Capture(edited));

        Assert.Equal(VoxelChunk.Volume, edit.Runs.Sum(static r => r.Length));
        var index = ((Ground & 15) * 16 + (20 & 15)) * 16 + (20 & 15);
        Assert.Equal(new StateRun(index, 1, 5), Assert.Single(edit.States!));
    }

    [Fact]
    public void Given_NeighbouringCellsOfOneState_When_TheDiffIsTaken_Then_TheyShareARun()
    {
        var fresh = VoxelTestSupport.FlatScene().Grid;
        var edited = fresh.Clone();
        foreach (var x in new[] { 21, 22, 23 })
        {
            edited.SetBlock(x, Ground, 20, BlockId.Planks, 2);
        }

        edited.SetBlock(24, Ground, 20, BlockId.Planks, 3);
        edited.SetBlock(25, Ground, 20, BlockId.Planks); // state 0 ends the run and is not saved

        var edit = Assert.Single(VoxelGridDiff.Capture(edited));

        Assert.Equal([3, 1], edit.States!.Select(static r => r.Length));
        Assert.Equal([2, 3], edit.States!.Select(static r => r.State));
    }

    [Fact]
    public void Given_ADiffWithStates_When_AppliedToAFreshBuild_Then_TheWorldIsRebuiltExactly()
    {
        var (edited, fresh) = Stated();
        edited.SetBlock(21, Ground, 20, BlockId.Planks, 9);
        edited.SetBlock(21, Ground + 1, 20, BlockId.Stone);
        var rebuilt = fresh.Clone();

        VoxelGridDiff.Apply(rebuilt, VoxelGridDiff.Capture(edited));

        Assert.Equal(edited.ComputeDigestHex(), rebuilt.ComputeDigestHex());
        Assert.Equal(5, rebuilt.GetState(20, Ground, 20));
        Assert.Equal(9, rebuilt.GetState(21, Ground, 20));
        Assert.Equal(0, rebuilt.GetState(21, Ground + 1, 20));
    }

    [Fact]
    public void Given_AStateRemoved_When_TheDiffIsTaken_Then_TheChunkIsNoLongerChangedAndHasNoStateRuns()
    {
        var (edited, _) = Stated();
        edited.SetBlock(20, Ground, 20, BlockId.Planks); // back to where the scene had it: state 0

        Assert.Empty(VoxelGridDiff.Capture(edited)); // the pristine digest matches again
    }

    [Fact]
    public void Given_AWorldWithNoState_When_TheDiffIsTaken_Then_NoChunkCarriesStateRunsAndTheFileSaysNothingOfThem()
    {
        var fresh = VoxelTestSupport.FlatScene().Grid;
        var edited = fresh.Clone();
        edited.SetBlock(21, Ground, 20, BlockId.Planks);

        var edit = Assert.Single(VoxelGridDiff.Capture(edited));

        Assert.Null(edit.States);
        Assert.DoesNotContain("states", CanonicalJson.Serialize(edit), StringComparison.Ordinal);
    }

    [Fact]
    public void Given_AChunkEditWithStateRuns_When_WrittenAndRead_Then_ItComesBackEqual()
    {
        var (edited, _) = Stated();
        var edit = Assert.Single(VoxelGridDiff.Capture(edited));

        var back = CanonicalJson.Deserialize<ChunkEdit>(CanonicalJson.Serialize(edit));

        Assert.Equal(edit.States, back.States);
        Assert.Equal(edit.Runs, back.Runs);
    }

    [Fact]
    public void Given_BadStateRuns_When_Applied_Then_TheDiffIsRefused()
    {
        var (edited, fresh) = Stated();
        var good = Assert.Single(VoxelGridDiff.Capture(edited));
        var index = good.States![0].Start;

        foreach (var bad in new[]
        {
            new StateRun(index, 0, 3), // empty
            new StateRun(index, 1, 0), // state 0 is no run
            new StateRun(index, 1, 256), // not a byte
            new StateRun(VoxelChunk.Volume - 1, 2, 3), // out of the chunk
            new StateRun(index + 1, 1, 3), // a cell that is air
        })
        {
            Assert.Throws<ArgumentException>(() => VoxelGridDiff.Apply(fresh.Clone(), [good with { States = [bad] }]));
        }

        Assert.Throws<ArgumentException>(() => VoxelGridDiff.Apply(fresh.Clone(), [good with { States = [new StateRun(index, 1, 3), new StateRun(index, 1, 3)] }]));
    }

    [Fact]
    public void Given_ASessionWithAStatedBlock_When_SavedAndLoaded_Then_TheStateAndTheDigestComeBack()
    {
        var session = TestGame.NewSession();
        var feet = session.Scene.Spawn.Feet;
        var (x, y, z) = (feet.X + 6, feet.Y + 3, feet.Z);
        Assert.Equal(BlockId.Air, session.Scene.Grid.GetBlock(x, y, z));
        Assert.True(session.Scene.Grid.SetBlock(x, y, z, BlockId.Planks, 11));

        var text = SaveGame.Save(session);
        var document = SaveGame.Parse(text);
        var loaded = SaveGame.Load(text, TestGame.Data, TestGame.Route, bumpSession: false);

        Assert.Equal(SaveGame.CurrentVersion, document.Version);
        Assert.Contains(document.Chunks, static c => c.States is not null);
        Assert.Equal(11, loaded.Scene.Grid.GetState(x, y, z));
        Assert.Equal(session.Simulation.ComputeStateDigest(), loaded.Simulation.ComputeStateDigest());
        Assert.Equal(CanonicalJson.Serialize(document.Chunks), CanonicalJson.Serialize(SaveGame.Parse(SaveGame.Save(loaded)).Chunks));
    }

    [Fact]
    public void Given_ASessionWithNoState_When_Saved_Then_NoChunkHasStateRuns()
    {
        var session = TestGame.NewSession();
        Assert.True(session.Scene.Grid.SetBlock(session.Scene.Spawn.Feet.X + 6, session.Scene.Spawn.Feet.Y + 3, session.Scene.Spawn.Feet.Z, BlockId.Planks));

        var text = SaveGame.Save(session);

        Assert.All(SaveGame.Parse(text).Chunks, static c => Assert.Null(c.States));
        Assert.DoesNotContain("\"states\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_ASaveOfVersion11_When_ItIsRead_Then_ItIsRefused()
    {
        var text = SaveFixture.Read("save-v12.json").Replace("\"version\":12,", "\"version\":11,", StringComparison.Ordinal);

        var error = Assert.Throws<SaveException>(() => SaveGame.Parse(text));

        Assert.Equal("The save is format version 11; the oldest this game loads is 12 (it writes 12), and older saves are not migrated.", error.Message);
        Assert.Equal(12, SaveGame.CurrentVersion);
        Assert.Equal(12, SaveGame.OldestReadableVersion);
    }
}
