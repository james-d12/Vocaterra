namespace Slg.Content.Tests;

/// <summary>Smoke tests for the Slg.Content test project.</summary>
public class SmokeTests
{
    [Fact]
    public void MarkerTypeIsAvailable()
    {
        Assert.NotNull(typeof(Slg.Content.AssemblyMarker));
    }
}
