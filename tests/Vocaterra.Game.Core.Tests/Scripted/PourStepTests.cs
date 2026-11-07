using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Scripted;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Voxel;
using static Vocaterra.Game.Core.Tests.Voxel.VoxelTestSupport;

namespace Vocaterra.Game.Core.Tests.Scripted;

/// <summary>The scripted <c>pour</c> step (water-plan H06): it sets a water source and the water then flows from it.</summary>
public sealed class PourStepTests
{
    private const int Ground = GroundTop + 1;

    private static (VoxelSimulation Sim, List<string> Log) Go(params VoxelStep[] steps)
    {
        var sim = FlatSim();
        var log = new List<string>();
        var script = new VoxelScript(sim, new LookControl(), steps, static _ => null, log.Add);
        var commands = new List<VoxelCommand>();
        for (var tick = 0; tick < 10 && !script.IsDone; tick++)
        {
            script.Fill(commands);
            sim.Step(commands);
        }

        Assert.True(script.IsDone);
        return (sim, log);
    }

    [Fact]
    public void Given_APourStep_When_TheScriptRuns_Then_ASourceStandsInTheCellAndItIsLogged()
    {
        var (sim, log) = Go(VoxelStep.Pour(20, Ground, 20));

        Assert.Equal(BlockId.Water, sim.Grid.GetBlock(20, Ground, 20));
        Assert.True(sim.Grid.GetWater(20, Ground, 20).IsSource);
        Assert.Equal("pour 20,10,20: a water source where there was air", Assert.Single(log));
    }

    [Fact]
    public void Given_APourStep_When_TheSimulationRuns_Then_TheWaterSpreads()
    {
        var (sim, _) = Go(VoxelStep.Pour(20, Ground, 20));

        Run(sim, 60 * 5);

        Assert.Equal(WaterLevel.Flowing(7), sim.Grid.GetWater(27, Ground, 20));
    }

    [Fact]
    public void Given_APourIntoStone_When_TheScriptRuns_Then_ItIsRefusedAndNothingChanges()
    {
        var (sim, log) = Go(VoxelStep.Pour(20, GroundTop, 20));

        Assert.Equal(BlockId.Stone, sim.Grid.GetBlock(20, GroundTop, 20));
        Assert.StartsWith("pour 20,9,20: refused", Assert.Single(log), StringComparison.Ordinal);
        Assert.Equal(0, sim.Flow.Queue.Count);
    }

    [Fact]
    public void Given_PourStepsAmongOthers_When_TheScriptRuns_Then_AllOfThemHappenInOrder()
    {
        var (sim, log) = Go(VoxelStep.Pour(20, Ground, 20), VoxelStep.Look(90, 0), VoxelStep.Pour(40, Ground, 40));

        Assert.Equal(BlockId.Water, sim.Grid.GetBlock(40, Ground, 40));
        Assert.Equal(3, log.Count);
        Assert.StartsWith("pour 20", log[0], StringComparison.Ordinal);
        Assert.StartsWith("look", log[1], StringComparison.Ordinal);
        Assert.StartsWith("pour 40", log[2], StringComparison.Ordinal);
        Assert.Equal(0, sim.Inventory.Count(ItemId.Wood));
    }
}
