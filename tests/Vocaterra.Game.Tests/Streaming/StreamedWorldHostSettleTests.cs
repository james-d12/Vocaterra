using Vocaterra.Game.Core.Menu;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.Graphics.World;
using Vocaterra.Game.Streaming;

namespace Vocaterra.Game.Tests.Streaming;

/// <summary>The streamed world's host (B07): it settles at the spawn with the chunks resident and the far land built.</summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class StreamedWorldHostSettleTests : StreamedWorldHostTestBase
{
    [Fact]
    public void Given_AHostOverTheValley_When_ItSettlesAtTheSpawn_Then_TheChunksAreResidentTheFarLandIsBuiltAndTheRendererWouldListThem()
    {
        var scene = StreamedWorlds.Scene();
        var preset = QualityPreset.For(QualityLevel.Low).WithViewDistance(64).WithFarRange(384) with { FarRealRadius = 0 }; // a small far range and no ring: the test is about the chunks and the far land being made and listed, not about the ring (the next tests) or the distance
        using var host = StreamedWorldHost.For(scene, preset, 0)!;
        var eye = (X: scene.Spawn.Feet.X + 0.5f, Y: scene.Spawn.Feet.Y + 1.6f, Z: scene.Spawn.Feet.Z + 0.5f);
        var released = 0;

        HangGuard.Settled(limit => host.Settle(eye.X, eye.Y, eye.Z, _ => released++, limit));

        Assert.True(host.Residency.IsSettled);
        Assert.Equal(0, host.Far.Missing);
        Assert.True(host.Far.PendingTiles.Count + host.Far.Draw.Count > 20); // built; the renderer puts them on the card
        Assert.True(new ChunkViewSet(scene.Grid, 64).Update(eye.X, eye.Y, eye.Z).Count > 30);
        Assert.Equal(0, released);

        host.ApplyPreset(preset.WithFarRange(0), _ => released++);
        host.Update(eye.X, eye.Y, eye.Z, _ => released++);
        Assert.Empty(host.Far.Draw);
    }
}
