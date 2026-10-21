namespace Vocaterra.Nlp.Evaluation.Tests;

/// <summary>Smoke tests for the Vocaterra.Nlp.Evaluation test project.</summary>
public class SmokeTests
{
    [Fact]
    public void MarkerTypeIsAvailable()
    {
        Assert.NotNull(typeof(Vocaterra.Nlp.Evaluation.AssemblyMarker));
    }
}
