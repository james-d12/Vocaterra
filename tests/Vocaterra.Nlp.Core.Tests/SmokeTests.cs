using System.Text;
using Vocaterra.Nlp.Core.Audio;

namespace Vocaterra.Nlp.Core.Tests;

/// <summary>Smoke tests for the Vocaterra.Nlp.Core test project.</summary>
public class SmokeTests
{
    private const string Composed = "é";
    private const string Decomposed = "é";

    [Fact]
    public void CoreAssemblyIsReferenced()
    {
        Assert.Equal("Vocaterra.Nlp.Core", typeof(PcmClip).Assembly.GetName().Name);
    }

    [Fact]
    public void NfcNormalizationKeepsComposedInput()
    {
        Assert.Equal(Composed, Composed.Normalize(NormalizationForm.FormC));
    }

    [Fact]
    public void NfcComposesDecomposedInput()
    {
        Assert.NotEqual(Composed, Decomposed);
        Assert.Equal(Composed, Decomposed.Normalize(NormalizationForm.FormC));
    }
}
