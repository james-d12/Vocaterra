namespace Slg.Asr.Tests;

/// <summary>Smoke tests for the Slg.Asr test project.</summary>
public class SmokeTests
{
    [Fact]
    public void MarkerTypeIsAvailable()
    {
        Assert.NotNull(typeof(Slg.Asr.AssemblyMarker));
    }
}
