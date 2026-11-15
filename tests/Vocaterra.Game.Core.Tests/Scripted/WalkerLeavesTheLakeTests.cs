using Vocaterra.Game.Core.Scripted;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Water;

namespace Vocaterra.Game.Core.Tests.Scripted;

/// <summary>
/// The scripted walker in the shipped lake (water-plan H11): it swims out and hops back onto a bank a swimmer can climb, and a walk that ends in the water lets the body
/// sink. The bank at 212,262 stood two blocks above the lake's one-block-deep shelf, so no body could leave there (a wade jumps 1.3 blocks, the hop-out needs water at the
/// waist); H12 reshaped the banks (<c>TerrainGenerator.BankHeight</c>) and the walker now leaves the lake there.
/// </summary>
public sealed class WalkerLeavesTheLakeTests
{
    private static (VoxelSimulation Sim, List<string> Log) Go(params VoxelStep[] steps)
    {
        var sim = VoxelSimulation.Create(TestGame.Route(TestGame.Seed), TestItems.Catalogue);
        var log = new List<string>();
        var script = new VoxelScript(sim, new LookControl(), steps, static _ => null, log.Add);
        var commands = new List<VoxelCommand>();
        for (var tick = 0; tick < 30000 && !script.IsDone; tick++)
        {
            script.Fill(commands);
            sim.Step(commands);
        }

        Assert.True(script.IsDone);
        return (sim, log);
    }

    [Fact]
    public void Given_ASwimInTheLake_When_TheWalkerReturnsToTheBank_Then_ItClimbsOutAndArrives()
    {
        var (sim, log) = Go(VoxelStep.Teleport(244, 247), VoxelStep.WalkTo(244, 253), VoxelStep.WalkTo(244, 247));

        Assert.DoesNotContain(log, static l => l.Contains("gave up", StringComparison.Ordinal));
        Assert.Contains(log, static l => l.StartsWith("walk-to 244,247: arrived", StringComparison.Ordinal));
        Assert.False(Immersion.Of(sim.Grid, sim.Player).Body);
    }

    [Fact]
    public void Given_ADiveTargetInTheWater_When_TheWalkerArrives_Then_ItDoesNotRiseToTheSurfaceOnItsOwn()
    {
        var (sim, log) = Go(VoxelStep.Teleport(244, 247), VoxelStep.WalkTo(244, 253));
        for (var i = 0; i < 300; i++)
        {
            sim.Step([]);
        }

        Assert.Contains(log, static l => l.StartsWith("walk-to 244,253: arrived", StringComparison.Ordinal));
        Assert.True(Immersion.Of(sim.Grid, sim.Player).Eye, "the body sank to the lake bed under water");
    }

    [Fact]
    public void Given_TheBankAt212_262_When_TheWalkerSwimsOutAndReturnsToIt_Then_ItWalksOutOfTheLake()
    {
        var (sim, log) = Go(VoxelStep.Teleport(212, 262), VoxelStep.WalkTo(212, 288), VoxelStep.WalkTo(212, 262));

        Assert.DoesNotContain(log, static l => l.Contains("gave up", StringComparison.Ordinal));
        Assert.Contains(log, static l => l.StartsWith("walk-to 212,262: arrived", StringComparison.Ordinal));
        Assert.False(Immersion.Of(sim.Grid, sim.Player).Feet, "the body is out of the water");
    }
}
