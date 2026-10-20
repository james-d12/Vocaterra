using Slg.Core.Determinism;

namespace Slg.Core.Prompts;

/// <summary>Describes one prompt the player must answer: mode, difficulty, answer set and allowed intents.</summary>
public sealed record PromptSpec(
    string PromptId,
    RecognitionMode Mode,
    Difficulty Difficulty,
    string AnswerSetId,
    IReadOnlyList<string> AllowedIntentIds,
    string? NpcId,
    IReadOnlyList<string> SlotGrammarIds)
{
    /// <summary>Structural equality including list contents.</summary>
    public bool Equals(PromptSpec? other)
    {
        return other is not null
            && string.Equals(PromptId, other.PromptId, StringComparison.Ordinal)
            && Mode == other.Mode
            && Difficulty == other.Difficulty
            && string.Equals(AnswerSetId, other.AnswerSetId, StringComparison.Ordinal)
            && ValueEquality.ListEquals(AllowedIntentIds, other.AllowedIntentIds)
            && string.Equals(NpcId, other.NpcId, StringComparison.Ordinal)
            && ValueEquality.ListEquals(SlotGrammarIds, other.SlotGrammarIds);
    }

    /// <summary>Hash consistent with <see cref="Equals(PromptSpec)"/>.</summary>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(PromptId);
        hash.Add(Mode);
        hash.Add(Difficulty);
        hash.Add(AnswerSetId);
        ValueEquality.AddList(ref hash, AllowedIntentIds);
        hash.Add(NpcId);
        ValueEquality.AddList(ref hash, SlotGrammarIds);
        return hash.ToHashCode();
    }
}
