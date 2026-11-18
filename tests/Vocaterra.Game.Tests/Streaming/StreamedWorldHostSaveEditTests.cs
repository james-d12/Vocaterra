using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Streaming;

namespace Vocaterra.Game.Tests.Streaming;

/// <summary>The streamed world's host (B07f, C2): a loaded save's edit is in the far store and survives a restart on the same save.</summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class StreamedWorldHostSaveEditTests : StreamedWorldHostTestBase
{
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
}
