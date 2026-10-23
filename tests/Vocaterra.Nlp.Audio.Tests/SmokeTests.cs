namespace Vocaterra.Nlp.Audio.Tests;

/// <summary>Smoke tests for the Vocaterra.Nlp.Audio test project.</summary>
public class SmokeTests
{
    [Fact]
    public void MarkerTypeIsAvailable()
    {
        Assert.NotNull(typeof(Vocaterra.Nlp.Audio.AssemblyMarker));
    }
}
