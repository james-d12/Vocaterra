namespace Slg.Nlp.Tests;

/// <summary>Smoke tests for the Slg.Nlp test project.</summary>
public class SmokeTests
{
    [Fact]
    public void MarkerTypeIsAvailable()
    {
        Assert.NotNull(typeof(Slg.Nlp.AssemblyMarker));
    }
}
