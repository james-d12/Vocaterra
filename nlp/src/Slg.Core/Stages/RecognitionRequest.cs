using Slg.Core.Determinism;
using Slg.Core.Prompts;

namespace Slg.Core.Stages;

/// <summary>Input to a word recognizer: the mode, the allowed phrases (constrained mode) and how many alternatives to return.</summary>
public sealed record RecognitionRequest(RecognitionMode Mode, IReadOnlyList<string> GrammarPhrases, int MaxAlternatives)
{
    /// <summary>Default number of n-best alternatives.</summary>
    public const int DefaultMaxAlternatives = 5;

    /// <summary>Creates a constrained request limited to the given phrases.</summary>
    public static RecognitionRequest Constrained(IReadOnlyList<string> phrases, int maxAlternatives = DefaultMaxAlternatives)
    {
        ArgumentNullException.ThrowIfNull(phrases);
        return new RecognitionRequest(RecognitionMode.Constrained, phrases, maxAlternatives);
    }

    /// <summary>Creates an open-vocabulary request with no phrase list.</summary>
    public static RecognitionRequest Open(int maxAlternatives = DefaultMaxAlternatives)
    {
        return new RecognitionRequest(RecognitionMode.Open, [], maxAlternatives);
    }

    /// <summary>Structural equality including phrase list contents.</summary>
    public bool Equals(RecognitionRequest? other)
    {
        return other is not null
            && Mode == other.Mode
            && MaxAlternatives == other.MaxAlternatives
            && ValueEquality.ListEquals(GrammarPhrases, other.GrammarPhrases);
    }

    /// <summary>Hash consistent with <see cref="Equals(RecognitionRequest)"/>.</summary>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(Mode);
        hash.Add(MaxAlternatives);
        ValueEquality.AddList(ref hash, GrammarPhrases);
        return hash.ToHashCode();
    }
}
