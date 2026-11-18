using Vocaterra.Game.Diagnostics;

namespace Vocaterra.Game.Tests.Diagnostics;

/// <summary>B07b: the probe that times a start or a far move for <c>--stream-stats</c>.</summary>
public sealed class MoveProbeTests
{
    [Fact]
    public void Given_AFarMove_When_TheNearZoneAndThenTheViewAreComplete_Then_OneLineSaysBothTimesAndTheLongestFrame()
    {
        var probe = new MoveProbe();
        Assert.Null(probe.Observe(100, 90, 10, 10, nearReady: false, nearMeshed: false)); // the start
        Assert.NotNull(probe.Observe(400, 300, 10, 10, nearReady: true, nearMeshed: true));
        Assert.Null(probe.Observe(416, 410, 12, 10, nearReady: true, nearMeshed: true)); // a walk: no move to time

        Assert.Null(probe.Observe(1200, 420, 710, 10, nearReady: true, nearMeshed: false)); // a jump: the frame took 784 ms
        var line = probe.Observe(1500, 1480, 710, 10, nearReady: true, nearMeshed: true);

        Assert.Equal("move: jump to 710,10: near zone resident after 784 ms, near view complete after 1084 ms, longest frame 784 ms", line);
    }
}
