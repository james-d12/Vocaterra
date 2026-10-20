namespace Slg.Decision.Tests;

/// <summary>Smoke tests for the Slg.Decision test project.</summary>
public class SmokeTests
{
    [Fact]
    public void MarkerTypeIsAvailable()
    {
        Assert.NotNull(typeof(Slg.Decision.AssemblyMarker));
    }
}
