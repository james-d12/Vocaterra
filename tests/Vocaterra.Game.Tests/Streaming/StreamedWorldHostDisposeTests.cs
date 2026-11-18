using Vocaterra.Game.Core.Menu;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.Streaming;

namespace Vocaterra.Game.Tests.Streaming;

/// <summary>The streamed world's host (B07): disposing it with the ring and the writes in flight returns within a bound.</summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class StreamedWorldHostDisposeTests : StreamedWorldHostTestBase
{
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
}
