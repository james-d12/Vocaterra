using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Tests.Voxel;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Nlp.Core.Determinism;

namespace Vocaterra.Game.Core.Tests.Save;

/// <summary>Water that is not source in a save (water-plan H06): the scene diff lists a chunk whose levels alone changed, writes them as runs and rebuilds them exactly; a mid-flood session saves and loads to the same state.</summary>
public sealed class WaterSaveTests
{
    private const int Ground = VoxelTestSupport.GroundTop + 1;

    private static (VoxelGrid Edited, VoxelGrid Fresh) Flooded()
    {
        var fresh = VoxelTestSupport.FlatScene(g => g.SetBlock(20, Ground, 20, BlockId.Water)).Grid;
        var edited = fresh.Clone();
        edited.SetWater(20, Ground, 20, WaterLevel.Flowing(3));
        return (edited, fresh);
    }

    [Fact]
    public void Given_AChunkWhoseOnlyChangeIsALevel_When_TheDiffIsTaken_Then_TheChunkIsListedWithItsWaterRun()
    {
        var (edited, fresh) = Flooded();

        var edit = Assert.Single(VoxelGridDiff.Capture(edited));

        Assert.Equal(VoxelChunk.Volume, edit.Runs.Sum(static r => r.Length));
        var run = Assert.Single(edit.Water!);
        var index = ((Ground & 15) * 16 + (20 & 15)) * 16 + (20 & 15);
        Assert.Equal(new WaterRun(index, 1, WaterLevel.Flowing(3).Encoded), run);
    }

    [Fact]
    public void Given_NeighbouringCellsOfOneLevel_When_TheDiffIsTaken_Then_TheyShareARun()
    {
        var fresh = VoxelTestSupport.FlatScene().Grid;
        var edited = fresh.Clone();
        foreach (var x in new[] { 21, 22, 23 })
        {
            edited.SetBlock(x, Ground, 20, BlockId.Water);
            edited.SetWater(x, Ground, 20, WaterLevel.Flowing(2));
        }

        edited.SetBlock(24, Ground, 20, BlockId.Water);
        edited.SetWater(24, Ground, 20, WaterLevel.Flowing(3));

        var edit = Assert.Single(VoxelGridDiff.Capture(edited));

        Assert.Equal([3, 1], edit.Water!.Select(static r => r.Length));
        Assert.Equal([2, 3], edit.Water!.Select(static r => r.Level));
    }

    [Fact]
    public void Given_ADiffWithLevels_When_AppliedToAFreshBuild_Then_TheWorldIsRebuiltExactly()
    {
        var (edited, fresh) = Flooded();
        edited.SetBlock(21, Ground, 20, BlockId.Water);
        edited.SetWater(21, Ground, 20, WaterLevel.Flowing(1, falling: true));
        edited.SetBlock(21, Ground + 1, 20, BlockId.Planks);
        var rebuilt = fresh.Clone();

        VoxelGridDiff.Apply(rebuilt, VoxelGridDiff.Capture(edited));

        Assert.Equal(edited.ComputeDigestHex(), rebuilt.ComputeDigestHex());
        Assert.Equal(WaterLevel.Flowing(3), rebuilt.GetWater(20, Ground, 20));
        Assert.Equal(WaterLevel.Flowing(1, falling: true), rebuilt.GetWater(21, Ground, 20));
    }

    [Fact]
    public void Given_AWorldWithOnlySourceWater_When_TheDiffIsTaken_Then_NoChunkCarriesWaterRuns()
    {
        var fresh = VoxelTestSupport.FlatScene(g => g.SetBlock(20, Ground, 20, BlockId.Water)).Grid;
        var edited = fresh.Clone();
        edited.SetBlock(21, Ground, 20, BlockId.Planks);

        var edit = Assert.Single(VoxelGridDiff.Capture(edited));

        Assert.Null(edit.Water);
        Assert.DoesNotContain("water", CanonicalJson.Serialize(edit), StringComparison.Ordinal); // left out of the file, as saves were before
    }

    [Fact]
    public void Given_AChunkEditWithWaterRuns_When_WrittenAndRead_Then_ItComesBackEqual()
    {
        var (edited, fresh) = Flooded();
        var edit = Assert.Single(VoxelGridDiff.Capture(edited));

        var back = CanonicalJson.Deserialize<ChunkEdit>(CanonicalJson.Serialize(edit));

        Assert.Equal((edit.X, edit.Y, edit.Z), (back.X, back.Y, back.Z));
        Assert.Equal(edit.Water, back.Water);
        Assert.Equal(edit.Runs, back.Runs);
    }

    [Fact]
    public void Given_BadWaterRuns_When_Applied_Then_TheDiffIsRefused()
    {
        var (edited, fresh) = Flooded();
        var good = Assert.Single(VoxelGridDiff.Capture(edited));
        var index = good.Water![0].Start;

        foreach (var bad in new[]
        {
            new WaterRun(index, 0, 3), // empty
            new WaterRun(index, 1, 0), // source is no run
            new WaterRun(index, 1, 99), // not an encoding
            new WaterRun(index, 1, 8), // a falling source
            new WaterRun(VoxelChunk.Volume - 1, 2, 3), // out of the chunk
            new WaterRun(index + 1, 1, 3), // a cell that is not water
        })
        {
            var target = fresh.Clone();
            Assert.Throws<ArgumentException>(() => VoxelGridDiff.Apply(target, [good with { Water = [bad] }]));
        }

        Assert.Throws<ArgumentException>(() => VoxelGridDiff.Apply(fresh.Clone(), [good with { Water = [new WaterRun(index, 1, 3), new WaterRun(index, 1, 3)] }]));
    }

    [Fact]
    public void Given_AFloodInProgress_When_TheSessionIsSavedAndLoaded_Then_TheStateIsTheSameAndTheFloodGoesOnAlike()
    {
        var session = TestGame.NewSession();
        var cell = PourSpot(session);
        Assert.True(session.Simulation.PourWater(cell));
        for (var i = 0; i < 50; i++)
        {
            session.Step([]);
        }

        Assert.True(session.Simulation.Flow.Queue.Count > 0, "the flood is under way");
        var text = SaveGame.Save(session);
        var document = SaveGame.Parse(text);
        var loaded = SaveGame.Load(text, TestGame.Data, TestGame.Route, bumpSession: false);

        Assert.Equal(SaveGame.CurrentVersion, document.Version);
        Assert.NotNull(document.Flow);
        Assert.Contains(document.Chunks, static c => c.Water is not null);
        Assert.Equal(session.Simulation.ComputeStateDigest(), loaded.Simulation.ComputeStateDigest());
        Assert.Equal(session.Simulation.Flow.Queue.Snapshot(), loaded.Simulation.Flow.Queue.Snapshot());
        var again = SaveGame.Parse(SaveGame.Save(loaded));
        Assert.Equal(CanonicalJson.Serialize(document.Chunks), CanonicalJson.Serialize(again.Chunks));
        Assert.Equal(document.Flow, again.Flow);

        for (var i = 0; i < 400; i++)
        {
            session.Step([]);
            loaded.Step([]);
        }

        Assert.Equal(session.Simulation.ComputeStateDigest(), loaded.Simulation.ComputeStateDigest());
        Assert.Equal(session.Simulation.Flow.Queue.Snapshot(), loaded.Simulation.Flow.Queue.Snapshot());
    }

    [Fact]
    public void Given_ADrySession_When_Saved_Then_TheFileHasNoFlowOrAirAndLoadsBack()
    {
        var session = TestGame.NewSession();
        var text = SaveGame.Save(session);

        var document = SaveGame.Parse(text);

        Assert.Null(document.Flow);
        Assert.Null(document.Air);
        Assert.All(document.Chunks, static c => Assert.Null(c.Water));
        Assert.Equal(session.Simulation.ComputeStateDigest(), SaveGame.Load(text, TestGame.Data, TestGame.Route, bumpSession: false).Simulation.ComputeStateDigest());
    }

    [Fact]
    public void Given_ASwimmerShortOfAir_When_Saved_Then_TheAirComesBack()
    {
        var session = TestGame.NewSession();
        session.Simulation.Breath.Restore(400);

        var text = SaveGame.Save(session);
        var loaded = SaveGame.Load(text, TestGame.Data, TestGame.Route, bumpSession: false);

        Assert.Equal(400, SaveGame.Parse(text).Air);
        Assert.Equal(400, loaded.Simulation.Breath.Air);
    }

    // A cell of open air on the ground a few blocks from the spawn, where a pour has room to spread and the land rules allow it (the trail from the ledge is protected, Y09).
    private static BlockPos PourSpot(Vocaterra.Game.Core.Session.VoxelSession session)
    {
        var grid = session.Scene.Grid;
        var feet = session.Scene.Spawn.Feet;
        for (var d = 3; d < 12; d++)
        {
            var spot = new BlockPos(feet.X + d, feet.Y, feet.Z);
            if (grid.GetBlock(spot.X, spot.Y, spot.Z) == BlockId.Air && grid.IsSolid(spot.X, spot.Y - 1, spot.Z) && !session.Land.Rules.IsProtected(spot.X, spot.Z))
            {
                return spot;
            }
        }

        throw new InvalidOperationException("no open ground near the spawn");
    }
}
