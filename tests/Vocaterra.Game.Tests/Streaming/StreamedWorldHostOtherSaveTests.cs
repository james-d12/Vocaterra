using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Streaming;

namespace Vocaterra.Game.Tests.Streaming;

/// <summary>The streamed world's host (B07f, C2): an edit of another save left in the world's folder is dropped.</summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class StreamedWorldHostOtherSaveTests : StreamedWorldHostTestBase
{
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
}
