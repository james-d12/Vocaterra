namespace Vocaterra.Nlp.Asr.Tests;

/// <summary>Smoke tests for the Vocaterra.Nlp.Asr test project.</summary>
public class SmokeTests
{
    [Fact]
    public void MarkerTypeIsAvailable()
    {
        Assert.NotNull(typeof(Vocaterra.Nlp.Asr.AssemblyMarker));
    }
}
