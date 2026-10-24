namespace Vocaterra.Nlp.Pipeline.Tests;

/// <summary>Smoke tests for the Vocaterra.Nlp.Pipeline test project.</summary>
public class SmokeTests
{
    [Fact]
    public void MarkerTypeIsAvailable()
    {
        Assert.NotNull(typeof(Vocaterra.Nlp.Pipeline.AssemblyMarker));
    }
}
