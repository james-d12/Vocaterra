using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Voxel;

/// <summary>The built scene is at rest (water-plan 4.5): nothing is scheduled when it is made, and many steps change nothing in it.</summary>
public sealed class BuiltSceneAtRestTests
{
    [Fact]
    public void Given_TheBuiltScene_When_ASimulationStartsOnIt_Then_NothingWaitsToFlow()
    {
        var sim = VoxelSimulation.Create(VoxelTestSupport.BuildRoute(42), TestItems.Catalogue);

        Assert.Equal(0, sim.Flow.Queue.Count);
    }

    [Fact]
    public void Given_TheBuiltScene_When_ManyStepsRunWithNoEdits_Then_EveryBlockAndLevelIsAsBuilt()
    {
        var scene = VoxelTestSupport.BuildRoute(42);
        var sim = VoxelSimulation.Create(scene, TestItems.Catalogue);
        var built = sim.Grid.ComputeDigestHex();
        var withLevels = sim.Grid.Chunks.Where(static chunk => chunk.HasWaterLevels).Select(static chunk => chunk.Coord).ToList(); // the falls and the rapids (H07a)
        var edits = sim.Grid.EditCount;

        VoxelTestSupport.Run(sim, 60 * 30);

        Assert.Equal(built, sim.Grid.ComputeDigestHex());
        Assert.Equal(edits, sim.Grid.EditCount);
        Assert.Equal(0, sim.Flow.Queue.Count);
        Assert.Equal(withLevels, sim.Grid.Chunks.Where(static chunk => chunk.HasWaterLevels).Select(static chunk => chunk.Coord).ToList());
    }
}
