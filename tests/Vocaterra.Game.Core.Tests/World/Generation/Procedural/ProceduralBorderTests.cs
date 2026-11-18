using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World.Generation.Procedural;

namespace Vocaterra.Game.Core.Tests.World.Generation.Procedural;

/// <summary>The ground the passes leave (B03): no biome's border makes a cliff, beyond what the height model's own bound (B01: 9 blocks between neighbouring columns) allows.</summary>
public sealed class ProceduralBorderTests
{
    // B01's bound for the raw ground is 9. The edge of the water may cut a bank down by more, so pairs by the water are left to the water's own tests.
    private const int Bound = 9;

    [Fact]
    public void Given_ThreeSeedsOverTheirFeatures_When_TheShapedGroundIsRead_Then_NoStepBetweenNeighbouringColumnsOfDifferentBiomesPassesTheBound()
    {
        var (pairs, biggest, where) = (0, 0, "");
        foreach (var seed in ProceduralTestWorld.Seeds)
        {
            var source = ProceduralTestWorld.Source(seed);
            foreach (var feature in new[] { Feature.Forest, Feature.Coast, Feature.River, Feature.Mountain, Feature.Plain })
            {
                var (cx, cz) = ProceduralTestWorld.Find(seed, feature);
                for (var dz = -1; dz <= 1; dz++)
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        var column = source.ShapedColumn(cx + dx, cz + dz);
                        for (var z = (cz + dz) * 16; z < ((cz + dz) * 16) + 16; z++)
                        {
                            for (var x = (cx + dx) * 16; x < ((cx + dx) * 16) + 16; x++)
                            {
                                foreach (var (ox, oz) in new[] { (1, 0), (0, 1) })
                                {
                                    if (column.BiomeAt(x, z).Row.Index == column.BiomeAt(x + ox, z + oz).Row.Index)
                                    {
                                        continue;
                                    }

                                    if (BesideWater(column, x, z) || BesideWater(column, x + ox, z + oz))
                                    {
                                        continue;
                                    }

                                    pairs++;
                                    var step = Math.Abs(column.HeightAt(x, z) - column.HeightAt(x + ox, z + oz));
                                    if (step > biggest)
                                    {
                                        (biggest, where) = (step, $"seed {seed}, {feature}, at {x}, {z}");
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        Assert.True(pairs > 200, $"{pairs} neighbouring pairs of different biomes were compared");
        Assert.True(biggest <= Bound, $"the ground steps {biggest} blocks across a biome border ({where}); the bound is {Bound}");
    }

    private static bool BesideWater(ColumnContext column, int x, int z)
    {
        var sea = TestGame.Data.WorldGen.Vertical.SeaLevel;
        return column.HeightAt(x, z) < sea || column.HeightAt(x + 1, z) < sea || column.HeightAt(x - 1, z) < sea || column.HeightAt(x, z + 1) < sea || column.HeightAt(x, z - 1) < sea;
    }
}
