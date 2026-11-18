using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World.Climate;
using Vocaterra.Game.Core.World.Generation.Procedural;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Generation.Procedural;

/// <summary>Tests split out of DrainageNetworkTests (B28), so they run beside the others: CaveEntrancesOfASeed.</summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class DrainageNetworkCaveEntranceTests(ITestOutputHelper output) : DrainageNetworkTestsBase(output)
{
    [Theory]
    [MemberData(nameof(Seeds))]
    public void Given_TheCaveEntrancesOfASeed_When_TheirColumnsAreCheckedAgainstTheRiversAndLakes_Then_NoPassageCutsIntoACarvedColumn(ulong seed)
    {
        // B11b's entrances keep clear of water; with B02c's rivers and lakes (carved down to a bed of their own, in the foothills as well as the lowland) no passage may run in a river's or a lake's bed or banks either.
        var model = new HeightModel(World, seed);
        var climate = new ClimateSampler(World, seed);
        var sites = ProceduralTestWorld.Source(seed).World.Entrances!;
        var checkedNodes = 0;
        for (var sz = -10; sz < 10; sz++)
        {
            for (var sx = -10; sx < 10; sx++)
            {
                if (sites.Site(sx, sz) is not { } ramp)
                {
                    continue;
                }

                for (var i = 0; i <= ramp.Last; i++)
                {
                    var node = ramp.Nodes[i];
                    var shape = model.Shape(node.X, node.Z, climate.At(node.X, node.Z));
                    checkedNodes++;
                    Assert.True(!shape.Water && shape.RiverWeight == 0 && shape.LakeWeight == 0, $"a passage of seed {seed} runs through ({node.X}, {node.Z}), a column of {(shape.Water ? "water" : "a river's or lake's bank")}");
                }
            }
        }

        Output.WriteLine($"seed {seed}: {checkedNodes} passage nodes, none in a carved column");
        Assert.True(checkedNodes > 50, $"{checkedNodes} nodes");
    }
}
