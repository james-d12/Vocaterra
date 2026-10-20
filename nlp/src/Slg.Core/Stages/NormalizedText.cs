using Slg.Core.Determinism;
using Slg.Core.Verdicts;

namespace Slg.Core.Stages;

/// <summary>One normalized token: its text, its span in the normalized string and the original surface form.</summary>
public sealed record Token(string Text, TextSpan Span, string Original);

/// <summary>Normalized text and its tokens; token spans index into <see cref="Text"/>.</summary>
public sealed record NormalizedText(string Text, IReadOnlyList<Token> Tokens)
{
    /// <summary>Structural equality including token list contents.</summary>
    public bool Equals(NormalizedText? other)
    {
        return other is not null
            && string.Equals(Text, other.Text, StringComparison.Ordinal)
            && ValueEquality.ListEquals(Tokens, other.Tokens);
    }

    /// <summary>Hash consistent with <see cref="Equals(NormalizedText)"/>.</summary>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(Text);
        ValueEquality.AddList(ref hash, Tokens);
        return hash.ToHashCode();
    }
}
