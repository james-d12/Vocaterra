using Slg.Core.Determinism;

namespace Slg.Core.Stages;

/// <summary>Input to a speech synthesizer: text, voice and optional explicit phones.</summary>
public sealed record SynthesisRequest(string Text, string VoiceId, IReadOnlyList<string>? Phones)
{
    /// <summary>Structural equality including phone list contents.</summary>
    public bool Equals(SynthesisRequest? other)
    {
        return other is not null
            && string.Equals(Text, other.Text, StringComparison.Ordinal)
            && string.Equals(VoiceId, other.VoiceId, StringComparison.Ordinal)
            && ValueEquality.ListEquals(Phones, other.Phones);
    }

    /// <summary>Hash consistent with <see cref="Equals(SynthesisRequest)"/>.</summary>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(Text);
        hash.Add(VoiceId);
        ValueEquality.AddList(ref hash, Phones);
        return hash.ToHashCode();
    }
}
