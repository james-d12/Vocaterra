namespace Vocaterra.Nlp.Core.Stages;

/// <summary>Adapter interface for sentence embedding models (D1); output must be deterministic for a given text.</summary>
public interface IEmbeddingModel
{
    /// <summary>Length of the vectors returned by <see cref="Embed"/>.</summary>
    int Dimension { get; }

    /// <summary>Hash of the model files this embedder runs.</summary>
    string ModelHash { get; }

    /// <summary>Embeds the text into a vector of <see cref="Dimension"/> floats.</summary>
    float[] Embed(string text);
}
