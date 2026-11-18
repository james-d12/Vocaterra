using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.World.Generation.Procedural;

/// <summary>Caller-owned window arrays (B40): a column made with a scratch that other columns (of other places) have already dirtied equals the column made with fresh arrays, byte for byte.</summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class ColumnScratchTests
{
    [Theory]
    [InlineData(42UL, Feature.Forest, Feature.Mountain)]
    [InlineData(2026UL, Feature.River, Feature.Coast)]
    public void Given_AScratchUsedByOtherColumns_When_ColumnsAreMadeThroughIt_Then_TheyEqualColumnsMadeWithFreshArrays(ulong seed, Feature first, Feature second)
    {
        var source = ProceduralTestWorld.Source(seed);
        var scratch = source.CreateScratch();
        foreach (var feature in new[] { first, second, first })
        {
            var (cx, cz) = ProceduralTestWorld.Find(seed, feature);
            var (minX, minZ) = (((cx >> 2) << 2), ((cz >> 2) << 2));
            var group = source.ShapeGroup(minX, minZ, 4);
            foreach (var (x, z) in new[] { (minX, minZ), (cx, cz), (minX + 3, minZ + 3) })
            {
                var fresh = source.GenerateLayers(x, z, source.MinChunkY, source.MaxChunkY - 1, group);
                var reused = source.GenerateLayers(x, z, source.MinChunkY, source.MaxChunkY - 1, group, scratch);
                Assert.Equal(fresh.Count, reused.Count);
                for (var i = 0; i < fresh.Count; i++)
                {
                    Assert.Equal(fresh[i].ComputeDigestHex(), reused[i].ComputeDigestHex());
                }
            }
        }
    }
}
