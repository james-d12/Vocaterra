using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Core.World.Regions;
using Vocaterra.Game.Core.World.Streaming;

namespace Vocaterra.Game.Core.Tests.Voxel;

/// <summary>HighestBlock from the column's band (B40): the answer is what the layer-by-layer reading gave, for columns of seeds 42 and 2026, untouched and edited above and in the ground.</summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class HighestBlockFromTheBandTests
{
    // The generator without the hint: it makes every layer from the top down, as HighestBlock did before B40.
    private sealed class EveryLayer(IChunkSource source) : IChunkGenerator
    {
        public ChunkContent? Generate(VoxelChunkCoord coord) => GridResidency.ContentOf(source.Generate(coord));
    }

    [Theory]
    [InlineData(42UL)]
    [InlineData(2026UL)]
    public void Given_ColumnsOfAWorldWithEdits_When_TheHighestBlockIsAsked_Then_TheBandAndTheLayerByLayerReadingAgree(ulong seed)
    {
        var world = WorldFactory.Compose(TestGame.Data, seed, RegionCatalog.Empty);
        var vertical = TestGame.Data.WorldGen.Vertical;
        var limits = WorldLimits.Symmetric(WorldLimits.DefaultHalfExtent, vertical.MinY, vertical.MaxY);
        var fast = WorldFactory.StreamedGrid(TestGame.Data, world.Source);
        var slow = new VoxelGrid(limits, new EveryLayer(world.Source));
        var compared = 0;
        var index = 0;
        for (var z = -1800; z <= 1800; z += 223)
        {
            for (var x = -1800; x <= 1800; x += 241)
            {
                var answer = slow.HighestBlock(x, z);
                Assert.Equal(answer, fast.HighestBlock(x, z));
                compared++;
                index++;
                if (index % 4 == 0)
                {
                    // a block far above the ground, in a chunk the band calls air: it must be found
                    var y = Math.Min(vertical.MaxY - 2, answer + 40);
                    Assert.Equal(slow.SetBlock(x, y, z, BlockId.Stone), fast.SetBlock(x, y, z, BlockId.Stone));
                }
                else if (index % 4 == 1 && answer > vertical.MinY)
                {
                    // the top block dug out
                    _ = slow.SetBlock(x, answer, z, BlockId.Air);
                    _ = fast.SetBlock(x, answer, z, BlockId.Air);
                }

                Assert.Equal(slow.HighestBlock(x, z), fast.HighestBlock(x, z));
            }
        }

        Assert.True(compared > 200);
    }
}
