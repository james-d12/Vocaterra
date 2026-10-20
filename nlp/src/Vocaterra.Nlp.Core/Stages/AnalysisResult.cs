using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Nlp.Core.Stages;

/// <summary>Morphological reading of one word form; optional features are null when not applicable.</summary>
public sealed record LexicalFeatures(string Lemma, string Pos, string? Person, string? Number, string? Tense, string? Mood, string? Gender);

/// <summary>A token with its candidate readings; an empty reading list means the word is unknown.</summary>
public sealed record AnalyzedToken(Token Token, IReadOnlyList<LexicalFeatures> Readings)
{
    /// <summary>Structural equality including reading list contents.</summary>
    public bool Equals(AnalyzedToken? other)
    {
        return other is not null
            && EqualityComparer<Token>.Default.Equals(Token, other.Token)
            && ValueEquality.ListEquals(Readings, other.Readings);
    }

    /// <summary>Hash consistent with <see cref="Equals(AnalyzedToken)"/>.</summary>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(Token);
        ValueEquality.AddList(ref hash, Readings);
        return hash.ToHashCode();
    }
}

/// <summary>A named slot value extracted from the text, with its span.</summary>
public sealed record SlotValue(string Name, string Value, TextSpan Span);

/// <summary>Output of linguistic analysis: readings, extracted slots, grammar errors and the matched grammar if any.</summary>
public sealed record AnalysisResult(
    IReadOnlyList<AnalyzedToken> Tokens,
    IReadOnlyList<SlotValue> Slots,
    IReadOnlyList<ErrorTag> Errors,
    string? MatchedGrammarId,
    double Confidence)
{
    /// <summary>Structural equality including list contents.</summary>
    public bool Equals(AnalysisResult? other)
    {
        return other is not null
            && ValueEquality.ListEquals(Tokens, other.Tokens)
            && ValueEquality.ListEquals(Slots, other.Slots)
            && ValueEquality.ListEquals(Errors, other.Errors)
            && string.Equals(MatchedGrammarId, other.MatchedGrammarId, StringComparison.Ordinal)
            && Confidence.Equals(other.Confidence);
    }

    /// <summary>Hash consistent with <see cref="Equals(AnalysisResult)"/>.</summary>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        ValueEquality.AddList(ref hash, Tokens);
        ValueEquality.AddList(ref hash, Slots);
        ValueEquality.AddList(ref hash, Errors);
        hash.Add(MatchedGrammarId);
        hash.Add(Confidence);
        return hash.ToHashCode();
    }
}
