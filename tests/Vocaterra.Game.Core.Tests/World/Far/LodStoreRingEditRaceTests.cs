using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Lod;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Far;

/// <summary>Tests split out of LodStoreTests (B28), so they run beside the others: TheRingsStackAndAnEditsChunk.</summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class LodStoreRingEditRaceTests(ITestOutputHelper output) : LodStoreTestsBase(output)
{
    [Fact]
    public void Given_TheRingsStackAndAnEditsChunkOfTheSameFootprint_When_TheyAreIngestedOnTwoThreadsManyTimes_Then_TheRealBlocksAndTheEditBothSurviveInMemoryAndOnDisk()
    {
        // B07f, C1: the edit's relight used to write back the stack it had read before the ring's real sections arrived, over them.
        var disk = new MemoryDisk { SaveDelayMilliseconds = 1 };
        var world = WorldOf(42, new LodStore(disk), treeLevels: 1);
        var (min, max) = world.SyRange(0);
        var edit = HouseChunk();
        var reference = WorldOf(42, new LodStore(), treeLevels: 1);
        reference.IngestStack(0, 0, GeneratorChunk);
        reference.IngestChunk(new VoxelChunkCoord(0, 4, 0), edit);

        for (var round = 0; round < 90; round++)
        {
            var (sx, sz) = (round, 7);
            var coord = new VoxelChunkCoord(2 * sx, 4, 2 * sz); // a chunk of the second section from the ground (cy 4 is section 2)
            using var go = new ManualResetEventSlim();
            Exception? failure = null;
            Thread Start(Action work) => new(() =>
            {
                try
                {
                    go.Wait();
                    work();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });
            var ring = Start(() =>
            {
                Thread.Sleep(round); // the ring's ingest begins a millisecond later each round, so it overlaps every point of the edit's relight
                world.IngestStack(sx, sz, (cx, cy, cz) => GeneratorChunk(cx - (2 * sx), cy, cz - (2 * sz)));
            });
            var player = Start(() => world.IngestChunk(coord, edit));
            ring.Start();
            player.Start();
            go.Set();
            ring.Join();
            player.Join();
            Assert.Null(failure);

            for (var sy = min; sy <= max; sy++)
            {
                var key = new LodSectionKey(0, sx, sy, sz);
                var entry = world.Store.Entry(key)!;
                Assert.Equal(LodOrigin.Real, entry.Origin);
                Assert.Equal(0xFF, entry.RealMask);
                var here = world.Store.Get(key)!;
                AssertSameBlocks(reference.Store.Get(new LodSectionKey(0, 0, sy, 0))!, here, $"round {round} section {sy}");
                var saved = disk.Files[key];
                Assert.Equal(LodOrigin.Real, (LodOrigin)saved[0]);
                AssertSameBlocks(here, LodSection.Unpack(saved[3..]), $"round {round} section {sy} on disk");
            }

            Assert.Equal(1 << 0, world.Store.Entry(new LodSectionKey(0, sx, 2, sz))!.EditedMask & 1);
        }
    }
}
