namespace Vocaterra.Nlp.Response.Tests;

/// <summary>Smoke tests for the Vocaterra.Nlp.Response test project.</summary>
public class SmokeTests
{
    [Fact]
    public void MarkerTypeIsAvailable()
    {
        Assert.NotNull(typeof(Vocaterra.Nlp.Response.AssemblyMarker));
    }
}
