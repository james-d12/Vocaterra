using Vocaterra.Game.Core.Scripted;

namespace Vocaterra.Game.Core.Tests.Scripted;

/// <summary>A scripted run's <c>--shot</c> step: a waiting picture is taken once.</summary>
public sealed class ShotRequestTests
{
    [Fact]
    public void AWaitingShotIsTakenOnce()
    {
        var shots = new ShotRequest();
        Assert.False(shots.Pending);
        Assert.Null(shots.Take());
        shots.Request("/tmp/a.png");
        Assert.True(shots.Pending);
        Assert.Equal("/tmp/a.png", shots.Take());
        Assert.False(shots.Pending);
    }
}
