namespace Vocaterra.Nlp.Golden.Tests;

/// <summary>Smoke tests proving the golden test project references the language core (Vocaterra.Nlp).</summary>
public class SmokeTests
{
    [Fact]
    public void ReferencedEngineTypesAreAvailable()
    {
        Assert.NotNull(typeof(Vocaterra.Nlp.AssemblyMarker));
        Assert.NotNull(typeof(Vocaterra.Nlp.Pipeline.UtterancePipeline));
    }
}
