using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Streaming;

namespace Vocaterra.Game.Tests.Streaming;

/// <summary>The Main menu closes a streamed world and the title starts another in the same process (Q01): the first world's host is gone with its workers and its far store, and the next settles as the first did.</summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class StreamedWorldRestartTests : StreamedWorldHostTestBase
{
    [Fact]
    public void Given_AStreamedWorldClosedCleanly_When_AnotherIsStartedInTheSameProcess_Then_ItSettlesTheSameWayAndTheFirstHostIsClosedForGood()
    {
        var preset = EditPreset();
        var first = StreamedWorlds.Scene();
        var spot = HouseSpot(first);
        StreamedWorldHost closed;
        long installed;
        long built;
        Vocaterra.Game.Core.Voxel.BlockId block;
        using (var host = StreamedWorldHost.For(first, preset, 0, "w1", NewFolder())!)
        {
            Settled(host, first);
            installed = host.Residency.Stats.Installed;
            built = host.Far.Stats.Built;
            block = FarBlock(host, spot);
            closed = host;
        }

        Assert.Null(closed.Pool.Failure);
        closed.Dispose(); // closing again is harmless (the program disposes what it has once, the menu's path included)

        var second = StreamedWorlds.Scene();
        using var other = StreamedWorldHost.For(second, preset, 0, "w2", NewFolder())!;
        Settled(other, second);

        Assert.Null(other.Pool.Failure);
        Assert.Equal(installed, other.Residency.Stats.Installed);
        Assert.Equal(built, other.Far.Stats.Built);
        Assert.Equal(block, FarBlock(other, spot));
    }
}
