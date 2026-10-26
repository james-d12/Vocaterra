using System.Text.Json.Serialization;
using Vocaterra.Nlp.Core.Determinism;

namespace Vocaterra.Nlp.Core.Verdicts;

/// <summary>A character range in normalized text, as a start offset and a length.</summary>
[JsonConverter(typeof(TextSpanJsonConverter))]
public readonly record struct TextSpan(int Start, int Length)
{
    /// <summary>Offset one past the last character of the span.</summary>
    public int End => Start + Length;
}
