namespace Slg.Response.Tests;

/// <summary>Smoke tests for the Slg.Response test project.</summary>
public class SmokeTests
{
    [Fact]
    public void MarkerTypeIsAvailable()
    {
        Assert.NotNull(typeof(Slg.Response.AssemblyMarker));
    }
}
