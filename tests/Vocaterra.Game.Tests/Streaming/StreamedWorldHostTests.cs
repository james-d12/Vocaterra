using Vocaterra.Game.Core.Menu;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.Graphics.World;
using Vocaterra.Game.Streaming;

namespace Vocaterra.Game.Tests.Streaming;

/// <summary>The streamed world's host (B07): none for the bounded valley, the worker threads and the residency and the far view for the streamed world, and radii that follow the view distance.</summary>
public sealed class StreamedWorldHostTests
{
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
        var preset = QualityPreset.For(QualityLevel.Low).WithViewDistance(96).WithFarRange(1024) with { FarRealRadius = 64 };
        using var host = StreamedWorldHost.For(scene, preset, 0)!;
        var eye = (X: scene.Spawn.Feet.X + 0.5f, Y: scene.Spawn.Feet.Y + 1.6f, Z: scene.Spawn.Feet.Z + 0.5f);
        var released = 0;

        Assert.True(host.Settle(eye.X, eye.Y, eye.Z, _ => released++));

        Assert.True(host.Residency.IsSettled);
        Assert.Equal(0, host.Far.Missing);
        Assert.True(host.Far.Draw.Count > 20);
        Assert.True(new ChunkViewSet(scene.Grid, 96).Update(eye.X, eye.Y, eye.Z).Count > 50);
        Assert.Equal(0, released);

        host.ApplyPreset(preset.WithFarRange(0), _ => released++);
        host.Update(eye.X, eye.Y, eye.Z, _ => released++);
        Assert.Empty(host.Far.Draw);
    }

    [Fact]
    public void Given_AHostWhoseRingAndEditsAreOn_When_ItSettlesAndABlockIsBroken_Then_RealSectionsAreInTheStoreAndTheEditIsInTheOneItBelongsTo()
    {
        var scene = StreamedWorlds.Scene();
        var preset = QualityPreset.For(QualityLevel.Low).WithViewDistance(96).WithFarRange(768) with { FarRealRadius = 48 };
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
        var preset = QualityPreset.For(QualityLevel.Low).WithViewDistance(96).WithFarRange(768) with { FarRealRadius = 96 };
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
        for (var sz = (int)MathF.Floor((z - 96) / 32); sz <= (int)MathF.Floor((z + 96) / 32); sz++)
        {
            for (var sx = (int)MathF.Floor((x + 40 - 96) / 32); sx <= (int)MathF.Floor((x + 40 + 96) / 32); sx++)
            {
                var (ex, ez) = (((sx + 0.5) * 32) - (x + 40), ((sz + 0.5) * 32) - z);
                if ((ex * ex) + (ez * ez) <= 96 * 96 && host.Far.World.Store.Entry(new Vocaterra.Game.Core.World.Lod.LodSectionKey(0, sx, min, sz)) is not { IsWhollyReal: true })
                {
                    missing++;
                }
            }
        }

        Assert.Equal(0, missing);
    }
}
