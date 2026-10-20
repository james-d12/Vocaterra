namespace Slg.Pronunciation.Tests;

/// <summary>Smoke tests for the Slg.Pronunciation test project.</summary>
public class SmokeTests
{
    [Fact]
    public void MarkerTypeIsAvailable()
    {
        Assert.NotNull(typeof(Slg.Pronunciation.AssemblyMarker));
    }
}
