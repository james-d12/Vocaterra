using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Generation.Procedural;

namespace Vocaterra.Game.Core.Tests.World.Generation.Procedural;

/// <summary>Tests split out of ProceduralChunkSourceTests (B28), so they run beside the others: AnyOrderOnOneSource.</summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class ProceduralChunkSourceOrderTests : ProceduralChunkSourceTestsBase
{
    [Theory]
    [MemberData(nameof(Places))]
    public void Given_APlaceAndItsNeighbours_When_TheyAreGeneratedInAnyOrderOnOneSourceAndOneByOneOnOthers_Then_EveryChunkEqualsTheSameChunkGeneratedAlone(ulong seed, Feature feature)
    {
        var (cx, cz) = ProceduralTestWorld.Find(seed, feature);
        var coords = Neighbourhood(cx, cz, feature == Feature.Mountain ? 0 : -4, feature == Feature.Mountain ? 19 : 7);
        var among = new ProceduralChunkSource(TestGame.Data.WorldGen, TestGame.Data.Biomes, seed, null, TestGame.Data.Underground);
        var generated = new Dictionary<VoxelChunkCoord, string>();
        foreach (var coord in Shuffled(coords, seed ^ 0x5eed))
        {
            generated[coord] = among.Generate(coord).ComputeDigestHex();
        }

        foreach (var coord in coords)
        {
            var alone = new ProceduralChunkSource(TestGame.Data.WorldGen, TestGame.Data.Biomes, seed, null, TestGame.Data.Underground).Generate(coord);
            Assert.Equal(alone.ComputeDigestHex(), generated[coord]);
            Assert.Equal(coord, alone.Coord);
        }
    }
}
