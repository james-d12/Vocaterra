using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Streaming;

namespace Vocaterra.Game.Tests.Streaming;

/// <summary>The streamed world's host (B07f, C2): a build made in a run quit without saving is not in the far view of the next load.</summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class StreamedWorldHostQuitTests : StreamedWorldHostTestBase
{
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
}
