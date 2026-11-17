using Vocaterra.Game.Core.Menu;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.Graphics.World;
using Vocaterra.Game.Streaming;

namespace Vocaterra.Game.Tests.Streaming;

/// <summary>The streamed world's host (B07): none for the bounded valley, the worker threads and the residency and the far view for the streamed world, and radii that follow the view distance.</summary>
public sealed class StreamedWorldHostTests : IDisposable
{
    private readonly List<string> _folders = [];

    public void Dispose()
    {
        foreach (var folder in _folders.Where(Directory.Exists))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Given_TheBoundedValleyAndAStreamedWorld_When_AHostIsAsked_Then_OnlyTheStreamedWorldHasOne()
    {
        var bounded = WorldFactory.Create(TestGame.Data, 42, WorldMode.Bounded);
        var streamed = StreamedWorlds.Scene();
        var preset = QualityPreset.For(QualityLevel.Low);

        Assert.Null(StreamedWorldHost.For(bounded, preset, 0));
        using var host = StreamedWorldHost.For(streamed, preset, 1);
        Assert.NotNull(host);
        Assert.Equal(1, host.Pool.Workers);
        Assert.Equal(preset.FarRange, host.Far.Settings.Range);
    }

    [Theory]
    [InlineData(224)]
    [InlineData(304)]
    [InlineData(400)]
    [InlineData(576)]
    public void Given_AViewDistance_When_ThePolicyIsAsked_Then_TheLoadReachesTheViewWithTheNeighboursTheMeshesNeedAndTheKeepAddsSlack(int viewDistance)
    {
        var policy = StreamedWorldHost.PolicyFor(viewDistance);

        Assert.True(policy.LoadRadius * 16 >= viewDistance + (2 * 16), "a chunk at the edge of the view has its neighbours, a chunk and the diagonal beyond it");
        Assert.True(policy.KeepRadius > policy.LoadRadius);
        Assert.True(policy.NearRadius < policy.UndergroundNearRadius);
    }

    [Fact]
    public void Given_AHostOverTheValley_When_ItSettlesAtTheSpawn_Then_TheChunksAreResidentTheFarLandIsBuiltAndTheRendererWouldListThem()
    {
        var scene = StreamedWorlds.Scene();
        var preset = QualityPreset.For(QualityLevel.Low).WithViewDistance(64).WithFarRange(384) with { FarRealRadius = 0 }; // a small far range and no ring: the test is about the chunks and the far land being made and listed, not about the ring (the next tests) or the distance
        using var host = StreamedWorldHost.For(scene, preset, 0)!;
        var eye = (X: scene.Spawn.Feet.X + 0.5f, Y: scene.Spawn.Feet.Y + 1.6f, Z: scene.Spawn.Feet.Z + 0.5f);
        var released = 0;

        Assert.True(host.Settle(eye.X, eye.Y, eye.Z, _ => released++));

        Assert.True(host.Residency.IsSettled);
        Assert.Equal(0, host.Far.Missing);
        Assert.True(host.Far.PendingTiles.Count + host.Far.Draw.Count > 20); // built; the renderer puts them on the card
        Assert.True(new ChunkViewSet(scene.Grid, 64).Update(eye.X, eye.Y, eye.Z).Count > 30);
        Assert.Equal(0, released);

        host.ApplyPreset(preset.WithFarRange(0), _ => released++);
        host.Update(eye.X, eye.Y, eye.Z, _ => released++);
        Assert.Empty(host.Far.Draw);
    }

    [Fact]
    public void Given_AHostWhoseRingAndEditsAreOn_When_ItSettlesAndABlockIsBroken_Then_RealSectionsAreInTheStoreAndTheEditIsInTheOneItBelongsTo()
    {
        var scene = StreamedWorlds.Scene();
        var preset = QualityPreset.For(QualityLevel.Low).WithViewDistance(96).WithFarRange(256) with { FarRealRadius = 48 };
        using var host = StreamedWorldHost.For(scene, preset, 0)!;
        var eye = (X: scene.Spawn.Feet.X + 0.5f, Y: scene.Spawn.Feet.Y + 1.6f, Z: scene.Spawn.Feet.Z + 0.5f);
        Assert.True(host.Settle(eye.X, eye.Y, eye.Z, _ => { }));
        var done = host.Ring.Done;
        Assert.True(done > 4, $"{done} footprints are real");
        var (sx, sz) = ((int)MathF.Floor(eye.X / 32), (int)MathF.Floor(eye.Z / 32));
        var (min, _) = host.Far.World.SyRange(0);
        Assert.True(host.Far.World.Store.Entry(new Vocaterra.Game.Core.World.Lod.LodSectionKey(0, sx, min, sz)) is { IsWhollyReal: true });

        // The player breaks the block under his feet: the far world knows it too.
        var (bx, by, bz) = ((int)MathF.Floor(eye.X), (int)MathF.Floor(scene.Spawn.Feet.Y) - 1, (int)MathF.Floor(eye.Z));
        Assert.True(scene.Grid.SetBlock(bx, by, bz, Vocaterra.Game.Core.Voxel.BlockId.Air));
        host.Update(eye.X, eye.Y, eye.Z, _ => { });
        Assert.True(host.Settle(eye.X, eye.Y, eye.Z, _ => { }));

        var section = host.Far.World.Store.Get(new Vocaterra.Game.Core.World.Lod.LodSectionKey(0, bx >> 5, by >> 5, bz >> 5))!;
        Assert.Equal(Vocaterra.Game.Core.Voxel.BlockId.Air, section.BlockAt(Vocaterra.Game.Core.World.Lod.LodSection.Index(bx & 31, by & 31, bz & 31)));
        Assert.True(host.EditedChunksTaken >= 1);
    }

    [Fact]
    public void Given_ARingPlannedForOnePlaceAndNotFinished_When_TheEyeMovesAndTheHostSettles_Then_EveryFootprintInTheNewRadiusIsReal()
    {
        var scene = StreamedWorlds.Scene();
        var preset = QualityPreset.For(QualityLevel.Low).WithViewDistance(96).WithFarRange(256) with { FarRealRadius = 64 };
        using var host = StreamedWorldHost.For(scene, preset, 0)!;
        var (x, y, z) = (scene.Spawn.Feet.X + 0.5f, scene.Spawn.Feet.Y + 1.6f, scene.Spawn.Feet.Z + 0.5f);
        host.Update(x, y, z, _ => { }); // planned, hardly begun
        var done = host.Ring.Done;
        var pending = host.Ring.Pending;
        Assert.True(pending > 4, $"{pending} planned after {done} done");

        host.Update(x + 40, y, z, _ => { }); // the eye moves further than a section: a new plan
        Assert.True(host.Settle(x + 40, y, z, _ => { }));

        var (min, _) = host.Far.World.SyRange(0);
        var missing = 0;
        for (var sz = (int)MathF.Floor((z - 64) / 32); sz <= (int)MathF.Floor((z + 64) / 32); sz++)
        {
            for (var sx = (int)MathF.Floor((x + 40 - 64) / 32); sx <= (int)MathF.Floor((x + 40 + 64) / 32); sx++)
            {
                var (ex, ez) = (((sx + 0.5) * 32) - (x + 40), ((sz + 0.5) * 32) - z);
                if ((ex * ex) + (ez * ez) <= 64 * 64 && host.Far.World.Store.Entry(new Vocaterra.Game.Core.World.Lod.LodSectionKey(0, sx, min, sz)) is not { IsWhollyReal: true })
                {
                    missing++;
                }
            }
        }

        Assert.Equal(0, missing);
    }

    // B07f, C2: the far store's edits are the loaded save's, whatever the last run left in the world's folder.
    private string NewFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "vocaterra-host-test-" + Guid.NewGuid().ToString("N"));
        _folders.Add(folder);
        return folder;
    }

    private static QualityPreset EditPreset() => QualityPreset.For(QualityLevel.Low).WithViewDistance(96).WithFarRange(768) with { FarRealRadius = 64 };

    // A house block in the air over the ground beside the spawn.
    private static (int X, int Y, int Z) HouseSpot(Scene scene) => ((int)MathF.Floor(scene.Spawn.Feet.X) + 3, (int)MathF.Floor(scene.Spawn.Feet.Y) + 2, (int)MathF.Floor(scene.Spawn.Feet.Z) + 3);

    private static (float X, float Y, float Z) Eye(Scene scene) => (scene.Spawn.Feet.X + 0.5f, scene.Spawn.Feet.Y + 1.6f, scene.Spawn.Feet.Z + 0.5f);

    private static Vocaterra.Game.Core.World.Lod.LodSection FarSection(StreamedWorldHost host, (int X, int Y, int Z) at) =>
        host.Far.World.Stack(0, at.X >> 5, at.Z >> 5).Sections[(at.Y >> 5) - host.Far.World.SyRange(0).Min];

    private static Vocaterra.Game.Core.Voxel.BlockId FarBlock(StreamedWorldHost host, (int X, int Y, int Z) at) =>
        FarSection(host, at).BlockAt(Vocaterra.Game.Core.World.Lod.LodSection.Index(at.X & 31, at.Y & 31, at.Z & 31));

    private static void Settled(StreamedWorldHost host, Scene scene)
    {
        var eye = Eye(scene);
        Assert.True(host.Settle(eye.X, eye.Y, eye.Z, _ => { }));
    }

    [Fact]
    public void Given_ASaveWithAnEditLoadedIntoAFreshHost_When_TheHostSettles_Then_TheEditIsInTheFarStoreAtOnceAndSurvivesARestartOnTheSameSave()
    {
        var folder = NewFolder();
        var preset = EditPreset();
        var spot = HouseSpot(StreamedWorlds.Scene());
        for (var run = 0; run < 2; run++)
        {
            var scene = StreamedWorlds.Scene(); // what SaveGame.Restore leaves: the edit is in the grid before the host exists
            Assert.True(scene.Grid.SetBlock(spot.X, spot.Y, spot.Z, Vocaterra.Game.Core.Voxel.BlockId.Planks));
            using var host = StreamedWorldHost.For(scene, preset, 0, "w", folder)!;

            Settled(host, scene);

            Assert.Equal(Vocaterra.Game.Core.Voxel.BlockId.Planks, FarBlock(host, spot));
            Assert.Equal(0, host.EditsDropped);
            Assert.True(host.EditedChunksTaken >= 1);
        }
    }

    [Fact]
    public void Given_AWorldWhoseFolderHoldsTheEditOfAnotherSave_When_ASaveWithoutItIsLoadedOnTheSameSeed_Then_TheEditIsNotInTheFarViewAndTheGeneratorsStackIsThere()
    {
        var folder = NewFolder();
        var preset = EditPreset();
        var first = StreamedWorlds.Scene();
        var spot = HouseSpot(first);
        Assert.True(first.Grid.SetBlock(spot.X, spot.Y, spot.Z, Vocaterra.Game.Core.Voxel.BlockId.Planks));
        using (var host = StreamedWorldHost.For(first, preset, 0, "w", folder)!)
        {
            Settled(host, first);
            Assert.Equal(Vocaterra.Game.Core.Voxel.BlockId.Planks, FarBlock(host, spot));
        }

        var second = StreamedWorlds.Scene(); // another save of the seed: no edit
        using var other = StreamedWorldHost.For(second, preset, 0, "w", folder)!;
        Assert.True(other.EditsDropped > 0);
        Settled(other, second);

        Assert.NotEqual(Vocaterra.Game.Core.Voxel.BlockId.Planks, FarBlock(other, spot));
        var entry = other.Far.World.Store.Entry(new Vocaterra.Game.Core.World.Lod.LodSectionKey(0, spot.X >> 5, spot.Y >> 5, spot.Z >> 5))!;
        Assert.Equal(0, entry.EditedMask);
        Assert.Equal(0xFF, entry.RealMask);
    }

    [Fact]
    public void Given_ABuildMadeInARunThatIsQuitWithoutSaving_When_TheSaveIsLoadedAgain_Then_TheFarViewDoesNotShowIt()
    {
        var folder = NewFolder();
        var preset = EditPreset();
        var run = StreamedWorlds.Scene();
        var spot = HouseSpot(run);
        using (var host = StreamedWorldHost.For(run, preset, 0, "w", folder)!)
        {
            Settled(host, run);
            Assert.True(run.Grid.SetBlock(spot.X, spot.Y, spot.Z, Vocaterra.Game.Core.Voxel.BlockId.Planks)); // built in play
            Settled(host, run);
            Assert.Equal(Vocaterra.Game.Core.Voxel.BlockId.Planks, FarBlock(host, spot));
        }

        var loaded = StreamedWorlds.Scene(); // the last save has no house
        using var again = StreamedWorldHost.For(loaded, preset, 0, "w", folder)!;
        Settled(again, loaded);

        Assert.True(again.EditsDropped > 0);
        Assert.NotEqual(Vocaterra.Game.Core.Voxel.BlockId.Planks, FarBlock(again, spot));
    }

    [Fact]
    public void Given_AHostWithTwoWorkersWhoseRingAndWritesAreInFlight_When_ItIsDisposed_Then_ItReturnsWithinABoundAndThrowsNothing()
    {
        var folder = NewFolder();
        var scene = StreamedWorlds.Scene();
        var preset = QualityPreset.For(QualityLevel.Low).WithViewDistance(96).WithFarRange(768) with { FarRealRadius = 192 };
        var spot = HouseSpot(scene);
        Assert.True(scene.Grid.SetBlock(spot.X, spot.Y, spot.Z, Vocaterra.Game.Core.Voxel.BlockId.Planks));
        var host = StreamedWorldHost.For(scene, preset, 2, "w", folder)!;
        var eye = Eye(scene);
        for (var frame = 0; frame < 30; frame++)
        {
            host.Update(eye.X, eye.Y, eye.Z, _ => { });
            Thread.Sleep(5);
        }

        var started = Environment.TickCount64;
        host.Dispose();
        var took = Environment.TickCount64 - started;

        Assert.True(took < 15_000, $"dispose took {took} ms");
        Assert.Null(host.Pool.Failure);
        Assert.True(File.Exists(Path.Combine(folder, "manifest.txt")));
    }

    [Fact]
    public void Given_ABuildFarOutsideTheRingButInsideTheFarRange_When_ASavedGameWithItIsLoaded_Then_TheFarTilesOverItAreMadeFromIt()
    {
        // B07f: the build stands 600 blocks from the eye, where the ring (64 blocks here) has made nothing real: the far tiles must still show it, at every level.
        var preset = QualityPreset.For(QualityLevel.Low).WithViewDistance(96).WithFarRange(768) with { FarRealRadius = 64 };
        long Vertices(bool build)
        {
            var scene = StreamedWorlds.Scene();
            var (hx, hz) = ((int)MathF.Floor(scene.Spawn.Feet.X) + 600, (int)MathF.Floor(scene.Spawn.Feet.Z));
            if (build)
            {
                var y = 150;
                while (scene.Grid.GetBlock(hx, y, hz) != Vocaterra.Game.Core.Voxel.BlockId.Air)
                {
                    y += 8;
                }

                for (var dy = 0; dy < 8; dy++)
                {
                    for (var dx = 0; dx < 8; dx++)
                    {
                        for (var dz = 0; dz < 8; dz++)
                        {
                            _ = scene.Grid.SetBlock(hx + dx, y + dy, hz + dz, Vocaterra.Game.Core.Voxel.BlockId.Planks);
                        }
                    }
                }
            }

            using var host = StreamedWorldHost.For(scene, preset, 0)!;
            var eye = Eye(scene);
            Assert.True(host.Settle(eye.X, eye.Y, eye.Z, _ => { }));
            return host.Far.Stats.Vertices; // every tile built (none is on a card here, so the draw list is empty)
        }

        var without = Vertices(build: false);
        var with = Vertices(build: true);

        Assert.NotEqual(without, with); // the cube changes the tiles that cover it (a solid mass hides the faces under it and makes its own), at levels the ring never reached
    }
}
