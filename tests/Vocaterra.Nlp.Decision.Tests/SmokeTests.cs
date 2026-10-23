namespace Vocaterra.Nlp.Decision.Tests;

/// <summary>Smoke tests for the Vocaterra.Nlp.Decision test project.</summary>
public class SmokeTests
{
    [Fact]
    public void MarkerTypeIsAvailable()
    {
        Assert.NotNull(typeof(Vocaterra.Nlp.Decision.AssemblyMarker));
    }
}
