namespace Vocaterra.Nlp.Language.Tests;

/// <summary>Smoke tests for the Vocaterra.Nlp.Language test project.</summary>
public class SmokeTests
{
    [Fact]
    public void MarkerTypeIsAvailable()
    {
        Assert.NotNull(typeof(Vocaterra.Nlp.Language.AssemblyMarker));
    }
}
