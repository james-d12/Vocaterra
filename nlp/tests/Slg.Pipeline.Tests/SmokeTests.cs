namespace Slg.Pipeline.Tests;

/// <summary>Smoke tests for the Slg.Pipeline test project.</summary>
public class SmokeTests
{
    [Fact]
    public void MarkerTypeIsAvailable()
    {
        Assert.NotNull(typeof(Slg.Pipeline.AssemblyMarker));
    }
}
