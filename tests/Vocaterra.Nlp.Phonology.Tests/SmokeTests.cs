namespace Vocaterra.Nlp.Phonology.Tests;

/// <summary>Smoke tests for the Vocaterra.Nlp.Phonology test project.</summary>
public class SmokeTests
{
    [Fact]
    public void MarkerTypeIsAvailable()
    {
        Assert.NotNull(typeof(Vocaterra.Nlp.Phonology.AssemblyMarker));
    }
}
