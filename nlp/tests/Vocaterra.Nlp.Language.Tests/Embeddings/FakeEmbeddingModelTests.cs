namespace Vocaterra.Nlp.Language.Tests.Embeddings;

using System.Text;
using Vocaterra.Nlp.Language.Embeddings;

public class FakeEmbeddingModelTests
{
    private static double Cosine(float[] a, float[] b)
    {
        var sum = 0.0;
        for (var i = 0; i < a.Length; i++)
        {
            sum += (double)a[i] * b[i];
        }

        return sum;
    }

    [Fact]
    public void ReportsItsDimensionAndModelHash()
    {
        var model = new FakeEmbeddingModel();

        Assert.Equal(64, model.Dimension);
        Assert.Equal("fake-trigram-v1", model.ModelHash);
        Assert.Equal(64, model.Embed("hola").Length);
        Assert.Equal(16, new FakeEmbeddingModel(16).Embed("hola").Length);
    }

    [Fact]
    public void RejectsNonPositiveDimensions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FakeEmbeddingModel(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FakeEmbeddingModel(-3));
    }

    [Fact]
    public void VectorsAreUnitLength()
    {
        var model = new FakeEmbeddingModel();

        foreach (var text in new[] { "hola", "quiero comprar una espada", "a", "un texto bastante más largo que los anteriores, con puntuación" })
        {
            Assert.Equal(1.0, Cosine(model.Embed(text), model.Embed(text)), 5);
        }
    }

    [Fact]
    public void SameTextGivesTheSameVectorEveryTime()
    {
        var first = new FakeEmbeddingModel().Embed("quiero comprar una espada");
        var second = new FakeEmbeddingModel().Embed("quiero comprar una espada");

        Assert.Equal(first, second);
    }

    [Fact]
    public void CaseWhitespaceAndUnicodeFormDoNotChangeTheVector()
    {
        var model = new FakeEmbeddingModel();
        var plain = model.Embed("hola buenos días");

        Assert.Equal(plain, model.Embed("  Hola   BUENOS\tdías "));
        Assert.Equal(plain, model.Embed("hola buenos días".Normalize(NormalizationForm.FormD)));
    }

    [Fact]
    public void EmptyTextGivesTheZeroVector()
    {
        var model = new FakeEmbeddingModel();

        Assert.All(model.Embed(string.Empty), v => Assert.Equal(0f, v));
        Assert.All(model.Embed("   "), v => Assert.Equal(0f, v));
    }

    [Fact]
    public void ParaphraseIsCloserThanAnUnrelatedPhrase()
    {
        var model = new FakeEmbeddingModel();
        var query = model.Embed("quiero comprar una espada");

        var paraphrase = Cosine(query, model.Embed("compro una espada"));
        var unrelated = Cosine(query, model.Embed("hola buenos días"));

        Assert.True(paraphrase > unrelated + 0.5, $"paraphrase {paraphrase} vs unrelated {unrelated}");
        Assert.Equal(0.723, paraphrase, 3);
    }

    [Fact]
    public void ModelIsUsableThroughTheCoreInterface()
    {
        Vocaterra.Nlp.Core.Stages.IEmbeddingModel model = new FakeEmbeddingModel(32);

        Assert.Equal(32, model.Dimension);
        Assert.Equal(32, model.Embed("hola").Length);
    }
}
