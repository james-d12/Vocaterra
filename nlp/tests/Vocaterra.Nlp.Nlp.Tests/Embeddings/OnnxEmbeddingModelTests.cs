namespace Vocaterra.Nlp.Nlp.Tests.Embeddings;

using Vocaterra.Nlp.Core.Configuration;
using Vocaterra.Nlp.Nlp.Embeddings;

/// <summary>A fact that is skipped unless <c>VOCATERRA_MODELS_DIR</c> points at a directory holding multilingual-e5-small (D1 gate).</summary>
public sealed class E5ModelFactAttribute : FactAttribute
{
    public E5ModelFactAttribute()
    {
        if (ModelDirectory is null)
        {
            Skip = "VOCATERRA_MODELS_DIR is not set or does not contain multilingual-e5-small/{model.onnx,tokenizer.json}.";
        }
    }

    public static string? ModelDirectory
    {
        get
        {
            var root = EnvironmentSettings.Get("VOCATERRA_MODELS_DIR", "SLG_MODELS_DIR");
            if (string.IsNullOrWhiteSpace(root))
            {
                return null;
            }

            var directory = Path.Combine(root, "multilingual-e5-small");
            return File.Exists(Path.Combine(directory, OnnxEmbeddingModel.ModelFileName))
                && File.Exists(Path.Combine(directory, OnnxEmbeddingModel.TokenizerFileName))
                ? directory
                : null;
        }
    }
}

public class OnnxEmbeddingModelTests
{
    [Fact]
    public void MeanPoolAveragesUnmaskedPositionsAndNormalizes()
    {
        float[] hidden = [1f, 0f, 3f, 4f, 100f, 100f];

        var pooled = OnnxEmbeddingModel.MeanPool(hidden, 3, 2, [1L, 1L, 0L]);

        Assert.Equal(2, pooled.Length);
        Assert.Equal(Math.Sqrt(0.5), pooled[0], 6);
        Assert.Equal(Math.Sqrt(0.5), pooled[1], 6);
    }

    [Fact]
    public void MeanPoolOfAnUnevenAverageIsUnitLength()
    {
        float[] hidden = [1f, 2f, 3f, 4f, 5f, 6f];

        var pooled = OnnxEmbeddingModel.MeanPool(hidden, 3, 2, [1L, 1L, 1L]);

        // mean = (3, 4) -> (0.6, 0.8)
        Assert.Equal(0.6, pooled[0], 6);
        Assert.Equal(0.8, pooled[1], 6);
    }

    [Fact]
    public void MeanPoolWithEverythingMaskedGivesTheZeroVector()
    {
        var pooled = OnnxEmbeddingModel.MeanPool([1f, 2f], 1, 2, [0L]);

        Assert.All(pooled, v => Assert.Equal(0f, v));
    }

    [Fact]
    public void MeanPoolRejectsInconsistentShapes()
    {
        Assert.Throws<ArgumentException>(() => OnnxEmbeddingModel.MeanPool([1f, 2f, 3f], 2, 2, [1L, 1L]));
        Assert.Throws<ArgumentException>(() => OnnxEmbeddingModel.MeanPool([1f, 2f], 1, 2, [1L, 1L]));
    }

    [Fact]
    public void MissingModelDirectoryFailsBeforeAnyNativeCode()
    {
        var missing = Path.Combine(Path.GetTempPath(), "vocaterra-no-such-model-" + Guid.NewGuid().ToString("N"));

        Assert.ThrowsAny<IOException>(() => new OnnxEmbeddingModel(missing));
    }

    [E5ModelFact]
    public void E5ModelProducesDeterministicUnitVectorsThatRankParaphrasesFirst()
    {
        using var model = new OnnxEmbeddingModel(E5ModelFactAttribute.ModelDirectory!);

        var query = model.Embed("quiero comprar una espada");
        var again = model.Embed("quiero comprar una espada");
        var paraphrase = model.Embed("compro una espada");
        var unrelated = model.Embed("hola buenos días");

        Assert.Equal(384, model.Dimension);
        Assert.Equal(model.Dimension, query.Length);
        Assert.False(string.IsNullOrEmpty(model.ModelHash));
        Assert.Equal(query, again);
        Assert.Equal(1.0, Dot(query, query), 4);
        Assert.True(Dot(query, paraphrase) > Dot(query, unrelated));
    }

    private static double Dot(float[] a, float[] b)
    {
        var sum = 0.0;
        for (var i = 0; i < a.Length; i++)
        {
            sum += (double)a[i] * b[i];
        }

        return sum;
    }
}
