namespace Vocaterra.Nlp.Golden.Tests;

/// <summary>Smoke tests proving the golden test project references its source projects.</summary>
public class SmokeTests
{
    [Fact]
    public void ReferencedMarkerTypesAreAvailable()
    {
        Assert.NotNull(typeof(Vocaterra.Nlp.Core.AssemblyMarker));
        Assert.NotNull(typeof(Vocaterra.Nlp.Phonology.AssemblyMarker));
        Assert.NotNull(typeof(Vocaterra.Nlp.Pipeline.AssemblyMarker));
    }
}
