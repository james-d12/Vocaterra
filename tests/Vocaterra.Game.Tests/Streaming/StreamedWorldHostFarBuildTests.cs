using Vocaterra.Game.Core.Menu;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.Streaming;

namespace Vocaterra.Game.Tests.Streaming;

/// <summary>The streamed world's host (B07f): a build far outside the ring but inside the far range is in the far tiles.</summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class StreamedWorldHostFarBuildTests : StreamedWorldHostTestBase
{
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
            Assert.True(SettleHost(host, eye), "the host settles (no slice without progress)");
            return host.Far.Stats.Vertices; // every tile built (none is on a card here, so the draw list is empty)
        }

        var without = Vertices(build: false);
        var with = Vertices(build: true);

        Assert.NotEqual(without, with); // the cube changes the tiles that cover it (a solid mass hides the faces under it and makes its own), at levels the ring never reached
    }
}
