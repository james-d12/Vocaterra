namespace Slg.Nlp.Tests.Embeddings;

using Slg.Nlp.Embeddings;

public class EmbeddingMathTests
{
    [Fact]
    public void ToUnitScalesToLengthOne()
    {
        var unit = EmbeddingMath.ToUnit([3f, 4f]);

        Assert.Equal(0.6, unit[0], 12);
        Assert.Equal(0.8, unit[1], 12);
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(float.NaN, 1f)]
    [InlineData(float.PositiveInfinity, 1f)]
    public void ToUnitMapsDegenerateVectorsToZero(float a, float b)
    {
        Assert.All(EmbeddingMath.ToUnit([a, b]), v => Assert.Equal(0.0, v));
    }

    [Fact]
    public void NormalizeInPlaceScalesAndZeroesDegenerateVectors()
    {
        var values = new[] { 0f, 2f, 0f };
        EmbeddingMath.NormalizeInPlace(values);
        Assert.Equal([0f, 1f, 0f], values);

        var zero = new[] { 0f, 0f };
        EmbeddingMath.NormalizeInPlace(zero);
        Assert.Equal([0f, 0f], zero);
    }

    [Fact]
    public void DotAndCosineWork()
    {
        double[] a = [1.0, 0.0];
        double[] b = [0.6, 0.8];

        Assert.Equal(0.6, EmbeddingMath.Dot(a, b), 12);
        Assert.Equal(0.6, EmbeddingMath.Cosine(a, b), 12);
        Assert.Equal(1.0, EmbeddingMath.Cosine([1.0000000001], [1.0]));
        Assert.Equal(-1.0, EmbeddingMath.Cosine([-2.0], [1.0]));
    }

    [Fact]
    public void DotRejectsMismatchedLengths()
    {
        Assert.Throws<ArgumentException>(() => EmbeddingMath.Dot([1.0], [1.0, 2.0]));
    }
}
