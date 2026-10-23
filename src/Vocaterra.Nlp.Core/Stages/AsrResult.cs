using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;

namespace Vocaterra.Nlp.Core.Stages;

/// <summary>A recognized word with its time range in seconds and a confidence.</summary>
public sealed record AsrWord(string Text, double StartSeconds, double EndSeconds, double Confidence);

/// <summary>One n-best transcript hypothesis with its score.</summary>
public sealed record AsrHypothesis(string Text, double Score);

/// <summary>Output of a word recognizer: best transcript, alternatives, word timings and the producing model hash.</summary>
public sealed record AsrResult(
    string Transcript,
    IReadOnlyList<AsrHypothesis> NBest,
    IReadOnlyList<AsrWord> Words,
    double Confidence,
    string ModelHash,
    RecognitionMode Mode)
{
    /// <summary>Structural equality including list contents.</summary>
    public bool Equals(AsrResult? other)
    {
        return other is not null
            && string.Equals(Transcript, other.Transcript, StringComparison.Ordinal)
            && ValueEquality.ListEquals(NBest, other.NBest)
            && ValueEquality.ListEquals(Words, other.Words)
            && Confidence.Equals(other.Confidence)
            && string.Equals(ModelHash, other.ModelHash, StringComparison.Ordinal)
            && Mode == other.Mode;
    }

    /// <summary>Hash consistent with <see cref="Equals(AsrResult)"/>.</summary>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(Transcript);
        ValueEquality.AddList(ref hash, NBest);
        ValueEquality.AddList(ref hash, Words);
        hash.Add(Confidence);
        hash.Add(ModelHash);
        hash.Add(Mode);
        return hash.ToHashCode();
    }
}
