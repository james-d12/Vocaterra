using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Tests.Voxel;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;

namespace Vocaterra.Game.Core.Tests.Simulation;

/// <summary>The save's parts of the simulation (S10): the scene diff lists only the edited chunks and rebuilds them exactly, and the simulation's state (tick, body, hotbar, gathering, inventory) restores to the same digest and the same continuation.</summary>
public sealed class VoxelSaveStateTests
{
    private static Scene Route(ulong seed = 42) => VoxelTestSupport.BuildRoute(seed);

    [Fact]
    public void AnUneditedWorldHasNoChunksInItsDiff()
    {
        var edits = VoxelGridDiff.Capture(Route().Grid);

        Assert.Empty(edits);
    }

    [Fact]
    public void TheDiffListsOnlyTheChunksThatWereEditedAndInChunkOrder()
    {
        var scene = Route();
        scene.Grid.SetBlock(50, 5, 2, BlockId.Torch); // chunk (3, 0, 0)
        scene.Grid.SetBlock(19, 5, 19, BlockId.Torch); // chunk (1, 0, 1)

        var edits = VoxelGridDiff.Capture(scene.Grid);

        Assert.Equal(2, edits.Count);
        // Sorted by layer, then row, then column: chunk (3, 0, 0) comes before chunk (1, 0, 1).
        Assert.Equal(new VoxelChunkCoord(3, 0, 0), new VoxelChunkCoord(edits[0].X, edits[0].Y, edits[0].Z));
        Assert.Equal(new VoxelChunkCoord(1, 0, 1), new VoxelChunkCoord(edits[1].X, edits[1].Y, edits[1].Z));
    }

    [Fact]
    public void EachEditedChunkIsRunLengthEncodedAndItsRunsAddUpToAChunk()
    {
        var scene = Route();
        scene.Grid.SetBlock(50, 5, 2, BlockId.Torch);

        var edit = Assert.Single(VoxelGridDiff.Capture(scene.Grid));

        Assert.True(edit.Runs.Count < VoxelChunk.Volume / 8, $"{edit.Runs.Count} runs");
        Assert.Equal(VoxelChunk.Volume, edit.Runs.Sum(static run => run.Length));
    }

    [Fact]
    public void ApplyingTheDiffToAFreshBuildRebuildsTheEditedWorldExactly()
    {
        var edited = Route();
        foreach (var (x, y, z) in new[] { (50, 5, 2), (19, 5, 19), (30, 12, 80), (5, 9, 40) })
        {
            edited.Grid.SetBlock(x, y, z, BlockId.Torch);
        }

        var rebuilt = Route();
        VoxelGridDiff.Apply(rebuilt.Grid, VoxelGridDiff.Capture(edited.Grid));

        Assert.Equal(edited.Grid.ComputeDigestHex(), rebuilt.Grid.ComputeDigestHex());
    }

    [Fact]
    public void ADiffThatDoesNotFitTheGridIsRefused()
    {
        var grid = Route().Grid;
        var air = new BlockRun(BlockId.Air, VoxelChunk.Volume);

        Assert.Throws<ArgumentException>(() => VoxelGridDiff.Apply(grid, [new ChunkEdit(grid.ChunksX, 0, 0, [air])])); // east of the grid
        Assert.Throws<ArgumentException>(() => VoxelGridDiff.Apply(grid, [new ChunkEdit(0, -1, 0, [air])])); // below the grid
        Assert.Throws<ArgumentException>(() => VoxelGridDiff.Apply(grid, [new ChunkEdit(0, 0, 0, [new BlockRun(BlockId.Air, 10)])]));
        Assert.Throws<ArgumentException>(() => VoxelGridDiff.Apply(grid, [new ChunkEdit(0, 0, 0, [new BlockRun(BlockId.Air, 0), air])]));
        Assert.Throws<ArgumentException>(() => VoxelGridDiff.Apply(grid, [new ChunkEdit(0, 0, 0, [air]), new ChunkEdit(0, 0, 0, [air])]));
        Assert.Throws<ArgumentException>(() => VoxelGridDiff.Apply(grid, [new ChunkEdit(1, 0, 0, [new BlockRun((BlockId)999, VoxelChunk.Volume)])]));
    }

    [Fact]
    public void TheSimulationRestoresToTheSameDigestAndContinuesTheSameWay()
    {
        var sim = VoxelSimulation.Create(Route(), TestItems.Catalogue);
        VoxelTestSupport.Run(sim, 90, VoxelCommand.Move(1, 0), VoxelCommand.Look(Angles.FromDegrees(120), 0));
        VoxelTestSupport.Run(sim, 1, VoxelCommand.SelectSlot(3));
        var state = sim.CaptureState();

        var restoredScene = Route();
        VoxelGridDiff.Apply(restoredScene.Grid, VoxelGridDiff.Capture(sim.Grid));
        var restored = VoxelSimulation.Create(restoredScene, TestItems.Catalogue);
        restored.Restore(state);

        Assert.Equal(sim.ComputeStateDigest(), restored.ComputeStateDigest());
        Assert.Equal(sim.Tick, restored.Tick);
        Assert.Equal(sim.SelectedSlot, restored.SelectedSlot);

        VoxelTestSupport.Run(sim, 120, VoxelCommand.Move(0, 1));
        VoxelTestSupport.Run(restored, 120, VoxelCommand.Move(0, 1));
        Assert.Equal(sim.ComputeStateDigest(), restored.ComputeStateDigest());
    }

    [Fact]
    public void ASimulationStateThatIsNotPossibleIsRefused()
    {
        var sim = VoxelSimulation.Create(Route(), TestItems.Catalogue);
        var state = sim.CaptureState();

        Assert.Throws<ArgumentOutOfRangeException>(() => sim.Restore(state with { SelectedSlot = Inventory.HotbarSize }));
        Assert.Throws<ArgumentOutOfRangeException>(() => sim.Restore(state with { Tick = -1 }));
        Assert.Throws<ArgumentException>(() => sim.Restore(state with { Slots = state.Slots.Take(3).ToArray() }));
    }

    [Fact]
    public void TheAirIsSavedWithTheStateAndTheRunContinuesTheSameWay()
    {
        var sim = VoxelSimulation.Create(Route(), TestItems.Catalogue);
        sim.Breath.Restore(400);
        var state = sim.CaptureState();
        var restored = VoxelSimulation.Create(Route(), TestItems.Catalogue);
        restored.Restore(state);

        Assert.Equal(400, state.Air);
        Assert.Equal(400, restored.Breath.Air);
        Assert.Equal(sim.ComputeStateDigest(), restored.ComputeStateDigest());

        VoxelTestSupport.Run(sim, 60);
        VoxelTestSupport.Run(restored, 60);
        Assert.Equal(sim.Breath.Air, restored.Breath.Air);
        Assert.Equal(sim.ComputeStateDigest(), restored.ComputeStateDigest());
    }

    [Fact]
    public void AStateWithoutAirIsAFullBreathAndTheDigestOfADryRunIsUnchangedByIt()
    {
        var sim = VoxelSimulation.Create(Route(), TestItems.Catalogue);
        var state = sim.CaptureState();

        Assert.Equal(Vocaterra.Game.Core.Water.Breath.MaxAir, state.Air);
        sim.Breath.Restore(10);
        Assert.NotEqual(VoxelSimulation.Create(Route(), TestItems.Catalogue).ComputeStateDigest(), sim.ComputeStateDigest());
    }
}
