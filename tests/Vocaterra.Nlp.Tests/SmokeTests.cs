namespace Vocaterra.Nlp.Tests;

/// <summary>Smoke test for the engine's unit test project: the test assembly references the engine assembly Vocaterra.Nlp.</summary>
public class SmokeTests
{
    [Fact]
    public void MarkerTypeIsInTheEngineAssembly()
    {
        Assert.Equal("Vocaterra.Nlp", typeof(Vocaterra.Nlp.AssemblyMarker).Assembly.GetName().Name);
    }
}
