using System.Collections.Concurrent;
using System.Security.Cryptography;
using Vocaterra.Game.Core.Menu;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Lod;
using Vocaterra.Game.Core.World.Streaming;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.Graphics.Far;
using Vocaterra.Game.Streaming;

namespace Vocaterra.Game.Tests.Streaming;

/// <summary>The ring of real chunks and the pre-generation built on it (B07d): the plan is made on a worker and never reads the disk on the game thread, a footprint made only from the layers the far view draws gives the same tiles as one made whole, the world does not depend on the worker count, and a run after a pre-generation has nothing left to make.</summary>
public sealed class LodRingTests : IDisposable
{
    private const double HangGuardMilliseconds = 3_600_000;

    private readonly List<string> _folders = [];

    public void Dispose()
    {
        foreach (var folder in _folders.Where(Directory.Exists))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private string NewFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "vocaterra-ring-test-" + Guid.NewGuid().ToString("N"));
        _folders.Add(folder);
        return folder;
    }

    private sealed class RecordingDisk(int gameThread) : ILodPersistence
    {
        public ConcurrentDictionary<LodSectionKey, byte[]> Files { get; } = new();

        public int LoadsOnTheGameThread;

        public int Loads;

        public byte[]? Load(LodSectionKey key)
        {
            _ = Interlocked.Increment(ref Loads);
            if (Environment.CurrentManagedThreadId == gameThread)
            {
                _ = Interlocked.Increment(ref LoadsOnTheGameThread);
            }

            return Files.TryGetValue(key, out var blob) ? blob : null;
        }

        public void Save(LodSectionKey key, byte[] blob) => Files[key] = blob;
    }

    private static void RunUntilIdle(LodRing ring, JobPool pool, double x, double z, int radius)
    {
        var until = Environment.TickCount64 + HangGuardMilliseconds;
        do
        {
            ring.Update(x, z, radius);
            if (pool.Workers == 0)
            {
                _ = pool.RunInline(null);
            }
            else
            {
                Thread.Sleep(1);
            }
        }
        while ((!ring.HasPlan || !ring.IsIdle) && Environment.TickCount64 < until);
    }

    [Fact]
    public void Given_AFreshStoreOverADiskThatHoldsTheRing_When_TheGameThreadUpdatesTheRing_Then_NoDiskReadIsOnTheGameThreadAndNothingIsMadeAgain()
    {
        var world = StreamedWorlds.Composition.World;
        var scene = StreamedWorlds.Scene();
        var (x, z) = (scene.Spawn.Feet.X + 0.5, scene.Spawn.Feet.Z + 0.5);
        var disk = new RecordingDisk(Environment.CurrentManagedThreadId);
        using (var pool = new JobPool(2))
        {
            var first = new LodRing(world.Far.FarWorld(new LodStore(disk), 2), world, pool, new ColumnBandCache(world.Bands));
            RunUntilIdle(first, pool, x, z, 64);
            Assert.True(first.Done >= 5, $"{first.Done} footprints made");
        }

        var made = disk.Files.Count;
        Assert.True(made > 20);
        Interlocked.Exchange(ref disk.Loads, 0);

        // A new run: an empty store over the same disk. The plan asks the disk about every footprint of the radius, and that must be a worker's work.
        using var again = new JobPool(2);
        var ring = new LodRing(world.Far.FarWorld(new LodStore(disk), 2), world, again, new ColumnBandCache(world.Bands));
        ring.Update(x, z, 64);
        Assert.False(ring.IsIdle, "the plan is under way (or its result waits to be taken) and counts as work");
        RunUntilIdle(ring, again, x, z, 64);

        Assert.Equal(0, ring.Done); // every footprint was found real on disk
        Assert.Equal(0, disk.LoadsOnTheGameThread);
        Assert.True(disk.Loads > 5, $"{disk.Loads} reads by the plan on a worker");
    }

    [Fact]
    public void Given_NoWorker_When_TheRingIsUpdated_Then_ItPlansAtOnceOnTheCallersThreadAsItAlwaysDid()
    {
        var world = StreamedWorlds.Composition.World;
        var scene = StreamedWorlds.Scene();
        using var pool = new JobPool(0);
        var ring = new LodRing(world.Far.FarWorld(new LodStore(), 2), world, pool, new ColumnBandCache(world.Bands));

        ring.Update(scene.Spawn.Feet.X + 0.5, scene.Spawn.Feet.Z + 0.5, 64);

        Assert.True(ring.HasPlan);
        Assert.True(ring.Pending > 4, $"{ring.Pending} planned");
        Assert.Equal(0, pool.Completed);
    }

    // The footprint of the most relief near the valley that no region owns, so the generator's own underground runs there.
    private static readonly Lazy<(int Sx, int Sz)> Rugged = new(FindRuggedFootprint);

    private static (int Sx, int Sz) RuggedFootprint() => Rugged.Value;

    private static (int Sx, int Sz) FindRuggedFootprint()
    {
        var composition = StreamedWorlds.Composition;
        var scene = StreamedWorlds.Scene();
        var (best, bestRelief) = ((Sx: 0, Sz: 0), -1);
        for (var dz = -40; dz <= 40; dz += 20)
        {
            for (var dx = -40; dx <= 40; dx += 20)
            {
                var (sx, sz) = (((int)scene.Spawn.Feet.X >> 5) + dx, ((int)scene.Spawn.Feet.Z >> 5) + dz);
                var owned = false;
                for (var cz = (2 * sz) - 2; cz <= (2 * sz) + 3 && !owned; cz++)
                {
                    for (var cx = (2 * sx) - 2; cx <= (2 * sx) + 3 && !owned; cx++)
                    {
                        owned = composition.World.Regions.ColumnOwner(cx, cz) is not null;
                    }
                }

                if (owned)
                {
                    continue;
                }

                int low = int.MaxValue, high = int.MinValue;
                for (var z = (sz * 32) - 48; z < (sz * 32) + 80; z += 32)
                {
                    for (var x = (sx * 32) - 48; x < (sx * 32) + 80; x += 32)
                    {
                        var height = composition.World.Far.At(x, z).Height;
                        low = Math.Min(low, height);
                        high = Math.Max(high, height);
                    }
                }

                if (high - low > bestRelief)
                {
                    (best, bestRelief) = ((sx, sz), high - low);
                }
            }
        }

        return best;
    }

    private static bool SameMesh(FarTileMesh a, FarTileMesh b) =>
        a.VertexCount == b.VertexCount && a.VertexArray.AsSpan(0, a.VertexCount).SequenceEqual(b.VertexArray.AsSpan(0, b.VertexCount)) && a.OpaqueIndices == b.OpaqueIndices && a.LitIndices == b.LitIndices && a.IndexCount == b.IndexCount
        && (a.Index16Array is not null
            ? b.Index16Array is not null && a.Index16Array.AsSpan(0, a.IndexCount).SequenceEqual(b.Index16Array.AsSpan(0, b.IndexCount))
            : b.Index32Array is not null && a.Index32Array.AsSpan(0, a.IndexCount).SequenceEqual(b.Index32Array.AsSpan(0, b.IndexCount)));

    [Fact]
    [Trait(TestSpeed.Trait, TestSpeed.Slow)]
    public void Given_TheRingMadeFromTheLayersTheFarViewDrawsAndTheRingMadeWhole_When_TheTilesOverThemAreBuilt_Then_TheyAreTheSameVertexForVertexAndTheSurfaceOnlyRingMadeFewerLayers()
    {
        var world = StreamedWorlds.Composition.World;
        var (sx, sz) = RuggedFootprint();
        var (x, z) = ((sx * 32) + 16.0, (sz * 32) + 16.0);
        var settings = new FarViewSettings(2048, TreeLevels: 2);
        var meshes = new List<(string Name, FarTileMesh Mesh)>[2];
        var layers = new (long Made, long Skipped)[2];
        for (var variant = 0; variant < 2; variant++)
        {
            using var pool = new JobPool(0);
            var far = world.Far.FarWorld(new LodStore(), 2);
            var ring = new LodRing(far, world, pool, new ColumnBandCache(world.Bands)) { SurfaceOnly = variant == 0 };
            RunUntilIdle(ring, pool, x, z, 40);
            Assert.True(ring.Done >= 5, $"{ring.Done} footprints made");
            layers[variant] = ring.Layers;
            meshes[variant] = [];
            foreach (var key in new[] { new FarTileKey(0, sx, sz), new FarTileKey(0, sx + 1, sz), new FarTileKey(0, sx, sz - 1), new FarTileKey(1, sx >> 1, sz >> 1), new FarTileKey(2, sx >> 2, sz >> 2) })
            {
                meshes[variant].Add(($"L{key.Level} ({key.Tx}, {key.Tz})", FarTileMesher.Build(far, settings, key)));
            }
        }

        Assert.True(layers[0].Skipped > 0, "the surface-only ring left layers out");
        Assert.Equal(0, layers[1].Skipped);
        Assert.True(layers[0].Made < layers[1].Made * 0.8, $"{layers[0].Made} layers made against {layers[1].Made}");
        for (var i = 0; i < meshes[0].Count; i++)
        {
            Assert.True(meshes[0][i].Mesh.Triangles > 0 || meshes[0][i].Name.StartsWith("L2", StringComparison.Ordinal), $"{meshes[0][i].Name} has faces");
            Assert.True(SameMesh(meshes[0][i].Mesh, meshes[1][i].Mesh), $"{meshes[0][i].Name} differs: {meshes[0][i].Mesh.Triangles} triangles against {meshes[1][i].Mesh.Triangles}");
        }
    }

    [Fact]
    public void Given_ARegionsColumnsAmongTheFootprintsNeighbours_When_ThePlanIsMadeForIt_Then_ThatFootprintIsMadeWhole()
    {
        var world = StreamedWorlds.Composition.World;
        var scene = StreamedWorlds.Scene();
        using var pool = new JobPool(0);
        var ring = new LodRing(world.Far.FarWorld(new LodStore(), 2), world, pool, new ColumnBandCache(world.Bands));
        var (sx, sz) = ((int)scene.Spawn.Feet.X >> 5, (int)scene.Spawn.Feet.Z >> 5);

        Assert.Equal(world.Source.MinChunkY, ring.LowestLayerOf(sx, sz)); // the valley's own chunks and the columns round them: every layer
        var (rx, rz) = RuggedFootprint();
        Assert.True(ring.LowestLayerOf(rx, rz) > world.Source.MinChunkY, "open land makes only the layers the far view draws");
        ring.SurfaceOnly = false;
        Assert.Equal(world.Source.MinChunkY, ring.LowestLayerOf(rx, rz));
    }

    // The resident chunks' coordinates and blocks, in a fixed order.
    private static string GridFingerprint(VoxelGrid grid)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var coord in GridResidency.Resident(grid).OrderBy(static c => c.Y).ThenBy(static c => c.Z).ThenBy(static c => c.X))
        {
            hash.AppendData(BitConverter.GetBytes(coord.X));
            hash.AppendData(BitConverter.GetBytes(coord.Y));
            hash.AppendData(BitConverter.GetBytes(coord.Z));
            if (grid.TryGetChunk(coord, out var chunk))
            {
                hash.AppendData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(chunk.Blocks.ToArray().AsSpan()));
            }
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static string FingerprintOf(StreamedWorldHost host, double x, double z)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var store = host.Far.World.Store;
        var (min, max) = host.Far.World.SyRange(0);
        for (var level = 0; level <= 2; level++)
        {
            var (lmin, lmax) = host.Far.World.SyRange(level);
            var span = (160 >> (5 + level)) + 2;
            var (cx, cz) = ((int)Math.Floor(x) >> (5 + level), (int)Math.Floor(z) >> (5 + level));
            for (var sz = cz - span; sz <= cz + span; sz++)
            {
                for (var sx = cx - span; sx <= cx + span; sx++)
                {
                    for (var sy = lmin; sy <= lmax; sy++)
                    {
                        if (store.Entry(new LodSectionKey(level, sx, sy, sz)) is { Origin: not LodOrigin.Model } entry)
                        {
                            hash.AppendData(BitConverter.GetBytes(level));
                            hash.AppendData(BitConverter.GetBytes(sx));
                            hash.AppendData(BitConverter.GetBytes(sy));
                            hash.AppendData(BitConverter.GetBytes(sz));
                            hash.AppendData(entry.Packed);
                            hash.AppendData([(byte)entry.Origin, entry.RealMask]);
                        }
                    }
                }
            }
        }

        _ = (min, max);
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    [Fact]
    [Trait(TestSpeed.Trait, TestSpeed.Slow)]
    public void Given_OneWorkerTwoAndEight_When_TheSameWorldIsSettled_Then_TheFarStoreAndTheGridAreTheSameBytesWhateverTheCount()
    {
        var preset = QualityPreset.For(QualityLevel.Low).WithViewDistance(96).WithFarRange(256) with { FarRealRadius = 96 };
        var fingerprints = new Dictionary<int, (string Store, string Grid, int Tiles)>();
        foreach (var workers in new[] { 1, 2, 8, 0 })
        {
            var scene = StreamedWorlds.Scene();
            using var host = StreamedWorldHost.For(scene, preset, workers)!;
            var (x, y, z) = (scene.Spawn.Feet.X + 0.5f, scene.Spawn.Feet.Y + 1.6f, scene.Spawn.Feet.Z + 0.5f);
            Assert.True(host.Settle(x, y, z, _ => { }, HangGuardMilliseconds));
            Assert.Equal(workers, host.Pool.Workers);
            fingerprints[workers] = (FingerprintOf(host, x, z), GridFingerprint(scene.Grid), host.Far.Draw.Count + host.Far.PendingTiles.Count);
        }

        Assert.NotEmpty(fingerprints[1].Store);
        foreach (var workers in new[] { 2, 8, 0 })
        {
            Assert.Equal(fingerprints[1].Store, fingerprints[workers].Store);
            Assert.Equal(fingerprints[1].Grid, fingerprints[workers].Grid);
            Assert.Equal(fingerprints[1].Tiles, fingerprints[workers].Tiles);
        }
    }

    [Fact]
    [Trait(TestSpeed.Trait, TestSpeed.Slow)]
    public void Given_APregeneration_When_ItRunsOnTheWorkersAndAFreshHostOpensTheFolder_Then_EveryFootprintOfTheRadiusIsMadeOnceAndTheNextRunHasNothingLeftToMake()
    {
        var folder = NewFolder();
        var preset = QualityPreset.For(QualityLevel.Low).WithViewDistance(96).WithFarRange(256) with { FarRealRadius = 0 };
        var scene = StreamedWorlds.Scene();
        var (x, y, z) = (scene.Spawn.Feet.X + 0.5f, scene.Spawn.Feet.Y + 1.6f, scene.Spawn.Feet.Z + 0.5f);
        var lines = new List<PregenProgress>();
        PregenResult result;
        using (var host = StreamedWorldHost.For(scene, preset, 2, "w", folder)!)
        {
            result = host.Pregenerate(x, z, 96, lines.Add, reportEveryMilliseconds: 50);
            Assert.True(host.Ring.IsIdle);
        }

        Assert.True(result.Footprints >= 20, $"{result.Footprints} footprints made"); // a disc of radius 96 round a point holds about 28 of 32 x 32
        Assert.Equal(result.Planned, result.Footprints);
        Assert.Equal(folder, result.Folder);
        Assert.NotEmpty(lines);
        Assert.Equal(result.Footprints, lines[^1].Done);
        Assert.Contains("pregen:", lines[^1].Line, StringComparison.Ordinal);
        Assert.True(lines[^1].Rate > 0);

        // A run after it (the far view's own ring wants the same radius): the footprints are on disk, the ring has nothing to make.
        var next = StreamedWorlds.Scene();
        using var again = StreamedWorldHost.For(next, preset with { FarRealRadius = 96 }, 2, "w", folder)!;
        Assert.True(again.Settle(x, y, z, _ => { }, HangGuardMilliseconds));
        Assert.Equal(0, again.Ring.Done);
        var (min, _) = again.Far.World.SyRange(0);
        Assert.True(again.Far.World.Store.Entry(new LodSectionKey(0, (int)Math.Floor(x / 32), min, (int)Math.Floor(z / 32))) is { IsWhollyReal: true });

        // And a pre-generation run twice makes nothing the second time.
        using var third = StreamedWorldHost.For(StreamedWorlds.Scene(), preset, 2, "w", folder)!;
        var second = third.Pregenerate(x, z, 96, _ => { }, reportEveryMilliseconds: 50);
        Assert.Equal(0, second.Footprints);
    }

    [Fact]
    public void Given_AProgressReport_When_ItIsRead_Then_TheRateTheTimeToGoAndTheLineFollowFromTheCounts()
    {
        var progress = new PregenProgress(250, 1000, 5_000, 4_000, 2_000);

        Assert.Equal(50.0, progress.Rate);
        Assert.Equal(15.0, progress.EtaSeconds);
        Assert.Contains("250 of 1000 footprints (25.0 %)", progress.Line, StringComparison.Ordinal);
        Assert.Null(new PregenProgress(0, 0, 0, 0, 0).EtaSeconds);
    }
}
