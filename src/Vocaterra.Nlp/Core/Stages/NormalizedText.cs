using System.Text.Json.Serialization;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Nlp.Core.Stages;

/// <summary>One normalized token: its text, its span in the normalized string and the original surface form.</summary>
public sealed record Token(string Text, TextSpan Span, string Original);

/// <summary>Normalized text and its tokens; token spans index into <see cref="Text"/>.</summary>
public sealed record NormalizedText(string Text, IReadOnlyList<Token> Tokens)
{
    /// <summary>
    /// The final mark of the source text, <c>?</c> or <c>!</c>, or null (M34, additive). Set only when the normalizer was asked to capture it
    /// (<c>NormalizationOptions.CaptureFinalMark</c>, off by default), so Spanish text normalized with the default options is unchanged. It is not part of
    /// <see cref="Text"/> or the tokens, and omitted from JSON while null.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FinalMark { get; init; }

    /// <summary>Structural equality including token list contents.</summary>
    public bool Equals(NormalizedText? other)
    {
        return other is not null
            && string.Equals(Text, other.Text, StringComparison.Ordinal)
            && string.Equals(FinalMark, other.FinalMark, StringComparison.Ordinal)
            && ValueEquality.ListEquals(Tokens, other.Tokens);
    }

    /// <summary>Hash consistent with <see cref="Equals(NormalizedText)"/>.</summary>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(Text);
        if (FinalMark is not null)
        {
            hash.Add(FinalMark);
        }

        ValueEquality.AddList(ref hash, Tokens);
        return hash.ToHashCode();
    }
}
