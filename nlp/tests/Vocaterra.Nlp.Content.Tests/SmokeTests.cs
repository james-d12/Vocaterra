namespace Vocaterra.Nlp.Content.Tests;

/// <summary>Smoke tests for the Vocaterra.Nlp.Content test project.</summary>
public class SmokeTests
{
    [Fact]
    public void MarkerTypeIsAvailable()
    {
        Assert.NotNull(typeof(Vocaterra.Nlp.Content.AssemblyMarker));
    }
}
