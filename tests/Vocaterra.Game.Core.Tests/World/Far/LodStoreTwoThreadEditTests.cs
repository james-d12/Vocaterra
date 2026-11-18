using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Lod;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Far;

/// <summary>Tests split out of LodStoreTests (B28), so they run beside the others: TwoThreadsEditingChunks.</summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class LodStoreTwoThreadEditTests(ITestOutputHelper output) : LodStoreTestsBase(output)
{
    [Fact]
    public void Given_TwoThreadsEditingChunksOfTheSameStackWhileTheRingIngestsIt_When_TheyAreDone_Then_BothEditsAreInAndTheStackIsStillTheGenerators()
    {
        var world = WorldOf(42, new LodStore(new MemoryDisk()), treeLevels: 1);
        var edit = HouseChunk();
        for (var round = 0; round < 40; round++)
        {
            var sx = round;
            Parallel.Invoke(
                () => world.IngestStack(sx, 3, (cx, cy, cz) => GeneratorChunk(cx - (2 * sx), cy, cz - 6)),
                () => world.IngestChunk(new VoxelChunkCoord(2 * sx, 4, 6), edit),
                () => world.IngestChunk(new VoxelChunkCoord((2 * sx) + 1, 4, 6), edit));
            var section = world.Store.Get(new LodSectionKey(0, sx, 2, 3))!;
            Assert.Equal(BlockId.Planks, section.BlockAt(LodSection.Index(7, 3, 5)));
            Assert.Equal(BlockId.Planks, section.BlockAt(LodSection.Index(16 + 7, 3, 5)));
            Assert.Equal(BlockId.Timber, world.Store.Get(new LodSectionKey(0, sx, 1, 3))!.BlockAt(LodSection.Index(9, 9, 9)));
        }
    }
}
