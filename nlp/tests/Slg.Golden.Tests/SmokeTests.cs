namespace Slg.Golden.Tests;

/// <summary>Smoke tests proving the golden test project references its source projects.</summary>
public class SmokeTests
{
    [Fact]
    public void ReferencedMarkerTypesAreAvailable()
    {
        Assert.NotNull(typeof(Slg.Core.AssemblyMarker));
        Assert.NotNull(typeof(Slg.Phonology.AssemblyMarker));
        Assert.NotNull(typeof(Slg.Pipeline.AssemblyMarker));
    }
}
