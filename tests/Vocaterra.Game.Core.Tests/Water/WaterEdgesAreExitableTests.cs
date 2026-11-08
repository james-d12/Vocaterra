using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Water;

/// <summary>
/// Every water edge of the shipped world can be left (water-plan H12): the lake, the river, the ponds and the waterfall's pool have no bank that a body in the water
/// cannot climb (a floor more than a step above shallow water, or more than the hop out lifts above deep water). The owner's answer (D-game-26) fixes the world, not the swim rules.
/// </summary>
public sealed class WaterEdgesAreExitableTests
{
    [Fact]
    public void Given_TheShippedWorld_When_ItsWaterEdgesAreScanned_Then_EveryOneCanBeLeftByWalkingOrTheHopOut()
    {
        var edges = UnexitableEdges.Scan(TestGame.Route(TestGame.Seed).Grid);

        Assert.True(edges.Count == 0, $"{edges.Count} water edges cannot be left:{Environment.NewLine}{string.Join(Environment.NewLine, UnexitableEdges.Stretches(edges))}");
    }
}
