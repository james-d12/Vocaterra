using Vocaterra.Game.Core.Navigation;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.Navigation;

/// <summary>The beacon: the point the player is sent to, and its reach.</summary>
public sealed class BeaconTests
{
    [Fact]
    public void TheBeaconHoldsItsTargetAndClearsWhenThePlayerArrives()
    {
        var beacon = new Beacon();
        Assert.False(beacon.IsSet);
        Assert.False(beacon.ClearIfReached(0, 0));
        beacon.Set(new BlockPos(60, 7, 82), "río");
        Assert.True(beacon.IsSet);
        Assert.Equal("río", beacon.Label);
        Assert.False(beacon.ClearIfReached(20, 20));
        Assert.True(beacon.IsSet);
        Assert.False(beacon.ClearIfReached(60.5, 82.5 - Beacon.ReachedDistance - 0.5));
        Assert.True(beacon.ClearIfReached(60.5, 82.5 - Beacon.ReachedDistance + 0.5));
        Assert.False(beacon.IsSet);
        beacon.Set(new BlockPos(1, 2, 3));
        beacon.Clear();
        Assert.Null(beacon.Target);
        Assert.Null(beacon.Label);
    }
}
