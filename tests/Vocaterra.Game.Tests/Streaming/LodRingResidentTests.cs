using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Lod;
using Vocaterra.Game.Core.World.Streaming;
using Vocaterra.Game.Streaming;

namespace Vocaterra.Game.Tests.Streaming;

/// <summary>B38: a footprint whose chunks the voxel zone holds takes them from the zone instead of generating them again, and the store ends up with the same sections; the ring's jobs sit on the tiles' priority scale.</summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class LodRingResidentTests
{
    // Open land some way from the valley, where no region owns a column of the footprint or the ones round it.
    private static (int Sx, int Sz) OpenFootprint(int index)
    {
        var composition = StreamedWorlds.Composition;
        var scene = StreamedWorlds.Scene();
        var found = 0;
        for (var d = 20; d < 200; d += 7)
        {
            var (sx, sz) = (((int)scene.Spawn.Feet.X >> 5) + d, ((int)scene.Spawn.Feet.Z >> 5) - d);
            var owned = false;
            for (var cz = (2 * sz) - 2; cz <= (2 * sz) + 3 && !owned; cz++)
            {
                for (var cx = (2 * sx) - 2; cx <= (2 * sx) + 3 && !owned; cx++)
                {
                    owned = composition.World.Regions.ColumnOwner(cx, cz) is not null;
                }
            }

            if (!owned && found++ == index)
            {
                return (sx, sz);
            }
        }

        throw new InvalidOperationException("no open footprint");
    }

    // The zone as the residency leaves it round a footprint: each of its four columns holds its band and a layer over and under; the first column holds every layer, as the near zone does.
    private static void FillZone(VoxelGrid grid, int sx, int sz)
    {
        var world = StreamedWorlds.Composition.World;
        var bands = new ColumnBandCache(world.Bands);
        for (var k = 0; k < 4; k++)
        {
            var (cx, cz) = ((2 * sx) + (k & 1), (2 * sz) + (k >> 1));
            var band = bands.Get(cx, cz);
            var (lo, hi) = k == 0 ? (world.Source.MinChunkY, world.Source.MaxChunkY - 1) : (band.MinLayer - 1, band.MaxLayer + 1);
            foreach (var chunk in world.GenerateLayers(cx, cz, lo, hi))
            {
                _ = GridResidency.Install(grid, chunk);
            }
        }
    }

    private static string Fingerprint(LodWorld far, int sx, int sz)
    {
        using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
        var (min, max) = far.SyRange(0);
        for (var sy = min; sy <= max; sy++)
        {
            var entry = far.Store.Entry(new LodSectionKey(0, sx, sy, sz));
            Assert.NotNull(entry);
            hash.AppendData(entry!.Packed);
            hash.AppendData([(byte)entry.Origin, entry.RealMask]);
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static void RunUntilIdle(LodRing ring, JobPool pool, double x, double z, int radius)
    {
        var until = HangGuard.DeadlineAfter(3_600_000);
        do
        {
            ring.Update(x, z, radius);
            _ = pool.RunInline(null);
        }
        while ((!ring.HasPlan || !ring.IsIdle) && !HangGuard.Passed(until));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Given_AFootprintWhoseChunksTheZoneHolds_When_TheRingMakesIt_Then_ItTakesThemFromTheZoneAndTheStoredSectionsAreTheSameAsGeneratingThemAll(int index)
    {
        var world = StreamedWorlds.Composition.World;
        var (sx, sz) = OpenFootprint(index);
        var scene = StreamedWorlds.Scene();
        FillZone(scene.Grid, sx, sz);
        var (x, z) = ((sx * 32) + 16.0, (sz * 32) + 16.0);

        using var pool = new JobPool(0);
        var generated = world.Far.FarWorld(new LodStore(), 2);
        var plain = new LodRing(generated, world, pool, new ColumnBandCache(world.Bands));
        RunUntilIdle(plain, pool, x, z, 20);
        var taken = world.Far.FarWorld(new LodStore(), 2);
        var fromZone = new LodRing(taken, world, pool, new ColumnBandCache(world.Bands), scene.Grid);
        RunUntilIdle(fromZone, pool, x, z, 20);

        Assert.Equal(1, plain.Done);
        Assert.Equal(1, fromZone.Done);
        Assert.Equal((0L, 0L), plain.Reused);
        Assert.True(fromZone.Reused.Resident >= 12, $"{fromZone.Reused.Resident} layers taken from the zone");
        Assert.True(fromZone.Reused.Air > 0, "the layers over the bands are known to be air");
        Assert.Equal(Fingerprint(generated, sx, sz), Fingerprint(taken, sx, sz));
    }

    [Fact]
    public void Given_AChunkTheZoneHoldsThatWasEdited_When_TheFootprintIsTaken_Then_ThatChunkIsLeftOutAndTheRestAreCopies()
    {
        var world = StreamedWorlds.Composition.World;
        var (sx, sz) = OpenFootprint(0);
        var scene = StreamedWorlds.Scene();
        FillZone(scene.Grid, sx, sz);
        using var pool = new JobPool(0);
        var ring = new LodRing(world.Far.FarWorld(new LodStore(), 2), world, pool, new ColumnBandCache(world.Bands), scene.Grid);
        var before = ring.TakeResident(sx, sz)!;
        var layer = Array.FindIndex(before[1]!, static c => c is { IsEmpty: false });
        Assert.True(layer >= 0);
        var min = world.Source.MinChunkY;
        var (bx, bz) = (((2 * sx) + 1) * 16, 2 * sz * 16);
        var by = (min + layer) * 16;
        var block = Enumerable.Range(0, 16).Select(dy => (dy, scene.Grid.GetBlock(bx, by + dy, bz))).First(static t => t.Item2 != BlockId.Air);
        Assert.True(scene.Grid.SetBlock(bx, by + block.dy, bz, BlockId.Air));

        var after = ring.TakeResident(sx, sz)!;

        Assert.Null(after[1]![layer]);
        Assert.NotNull(before[1]![layer]);
        Assert.Equal(before[0]!.Count(static c => c is not null), after[0]!.Count(static c => c is not null));
    }

    [Fact]
    public void Given_RingJobsAndATileJobOnOneScale_When_TheyRun_Then_TheFootprintsNearerThanTheTileGoBeforeItAndTheRestAfter()
    {
        var world = StreamedWorlds.Composition.World;
        var (sx, sz) = OpenFootprint(0);
        var (x, z) = ((sx * 32) + 16.0, (sz * 32) + 16.0);
        using var pool = new JobPool(0);
        var ring = new LodRing(world.Far.FarWorld(new LodStore(), 2), world, pool, new ColumnBandCache(world.Bands)) { PriorityOffset = 10_000 };
        var distances = new List<long>();
        for (var dz = -6; dz <= 6; dz++)
        {
            for (var dx = -6; dx <= 6; dx++)
            {
                var (ex, ez) = (((sx + dx + 0.5) * 32) - x, ((sz + dz + 0.5) * 32) - z);
                var d = (long)Math.Sqrt((ex * ex) + (ez * ez));
                if (d <= 160)
                {
                    distances.Add(d);
                }
            }
        }

        distances.Sort();
        var tile = distances[3] + 1; // four of the eight footprints the ring asks for first are nearer than this tile
        var doneWhenTheTileRan = -1L;
        pool.Submit(JobLane.Tiles, 10_000 + tile, static () => false, () => doneWhenTheTileRan = ring.Done);
        ring.Update(x, z, 160);
        while (doneWhenTheTileRan < 0)
        {
            Assert.True(pool.RunInline(0) > 0, "the tile job ran");
        }

        Assert.Equal(distances.Take(8).Count(d => d < tile), doneWhenTheTileRan);
    }
}
