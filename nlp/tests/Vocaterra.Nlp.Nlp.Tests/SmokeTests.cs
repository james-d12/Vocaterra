namespace Vocaterra.Nlp.Nlp.Tests;

/// <summary>Smoke tests for the Vocaterra.Nlp.Nlp test project.</summary>
public class SmokeTests
{
    [Fact]
    public void MarkerTypeIsAvailable()
    {
        Assert.NotNull(typeof(Vocaterra.Nlp.Nlp.AssemblyMarker));
    }
}
