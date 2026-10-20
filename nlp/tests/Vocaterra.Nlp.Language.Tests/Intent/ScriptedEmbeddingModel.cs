namespace Vocaterra.Nlp.Language.Tests.Intent;

using Vocaterra.Nlp.Core.Stages;

/// <summary>Three-dimensional test embedder with exact cosines: the query is (1,0,0) and a text with similarity c is (c, sqrt(1-c^2), 0).</summary>
internal sealed class ScriptedEmbeddingModel : IEmbeddingModel
{
    private readonly string _query;
    private readonly Dictionary<string, double> _similarities;

    public ScriptedEmbeddingModel(string query, IReadOnlyDictionary<string, double> similarityByExample)
    {
        _query = query;
        _similarities = new Dictionary<string, double>(similarityByExample, StringComparer.Ordinal);
    }

    public int Dimension => 3;

    public string ModelHash => "scripted-v1";

    public float[] Embed(string text)
    {
        if (string.Equals(text, _query, StringComparison.Ordinal))
        {
            return [1f, 0f, 0f];
        }

        var c = _similarities.TryGetValue(text, out var value) ? value : 0.0;
        return [(float)c, (float)Math.Sqrt(1.0 - (c * c)), 0f];
    }
}
