using System.Text;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Stages;

namespace Vocaterra.Nlp.Language.Embeddings;

/// <summary>
/// Deterministic test embedder: a signed hashed bag of character trigrams, L2-normalized. It measures surface overlap only,
/// so it is a stand-in for a real model, not a semantic model.
/// </summary>
/// <remarks>
/// The text is NFC-normalized, lowercased with the invariant culture, whitespace-collapsed and padded with one space on each side.
/// Every trigram is hashed with <see cref="DeterministicHash.Seed64(string[])"/> (SHA-256 over the model name and the trigram):
/// the low bits pick the bucket, bit 32 the sign. Text without any trigram yields the zero vector.
/// </remarks>
public sealed class FakeEmbeddingModel : IEmbeddingModel
{
    /// <summary>The value of <see cref="ModelHash"/>.</summary>
    public const string Hash = "fake-trigram-v1";

    /// <summary>The default vector length.</summary>
    public const int DefaultDimension = 64;

    /// <summary>Creates a fake embedder producing vectors of the given length (at least 1).</summary>
    public FakeEmbeddingModel(int dimension = DefaultDimension)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(dimension, 1);
        Dimension = dimension;
    }

    /// <inheritdoc />
    public int Dimension { get; }

    /// <inheritdoc />
    public string ModelHash => Hash;

    /// <inheritdoc />
    public float[] Embed(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var vector = new float[Dimension];
        var padded = " " + Canonicalize(text) + " ";
        if (padded.Length < 3)
        {
            return vector;
        }

        var sums = new double[Dimension];
        for (var i = 0; i + 3 <= padded.Length; i++)
        {
            var seed = DeterministicHash.Seed64(Hash, padded.Substring(i, 3));
            var bucket = (int)(seed % (ulong)Dimension);
            sums[bucket] += ((seed >> 32) & 1UL) == 0UL ? 1.0 : -1.0;
        }

        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] = (float)sums[i];
        }

        EmbeddingMath.NormalizeInPlace(vector);
        return vector;
    }

    private static string Canonicalize(string text)
    {
        var lowered = text.Normalize(NormalizationForm.FormC).ToLowerInvariant();
        var builder = new StringBuilder(lowered.Length);
        var pendingSpace = false;
        foreach (var c in lowered)
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(c);
        }

        return builder.ToString();
    }
}
