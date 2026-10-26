using System.Text;
namespace Vocaterra.Nlp.Core.Tests;

/// <summary>Unicode normalization (NFC) as the core's text handling relies on it. Formerly the core test project's smoke tests.</summary>
public class NfcNormalizationTests
{
    private const string Composed = "é";
    private const string Decomposed = "é";

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
