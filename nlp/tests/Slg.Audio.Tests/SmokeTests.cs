namespace Slg.Audio.Tests;

/// <summary>Smoke tests for the Slg.Audio test project.</summary>
public class SmokeTests
{
    [Fact]
    public void MarkerTypeIsAvailable()
    {
        Assert.NotNull(typeof(Slg.Audio.AssemblyMarker));
    }
}
