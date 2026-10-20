namespace Slg.Evaluation.Tests;

/// <summary>Smoke tests for the Slg.Evaluation test project.</summary>
public class SmokeTests
{
    [Fact]
    public void MarkerTypeIsAvailable()
    {
        Assert.NotNull(typeof(Slg.Evaluation.AssemblyMarker));
    }
}
