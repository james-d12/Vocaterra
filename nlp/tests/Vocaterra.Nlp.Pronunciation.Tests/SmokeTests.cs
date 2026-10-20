namespace Vocaterra.Nlp.Pronunciation.Tests;

/// <summary>Smoke tests for the Vocaterra.Nlp.Pronunciation test project.</summary>
public class SmokeTests
{
    [Fact]
    public void MarkerTypeIsAvailable()
    {
        Assert.NotNull(typeof(Vocaterra.Nlp.Pronunciation.AssemblyMarker));
    }
}
