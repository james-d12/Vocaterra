namespace Slg.Phonology.Tests;

/// <summary>Smoke tests for the Slg.Phonology test project.</summary>
public class SmokeTests
{
    [Fact]
    public void MarkerTypeIsAvailable()
    {
        Assert.NotNull(typeof(Slg.Phonology.AssemblyMarker));
    }
}
