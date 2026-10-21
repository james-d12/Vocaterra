using System.Text.Json.Serialization;
using Vocaterra.Nlp.Core.Determinism;

namespace Vocaterra.Nlp.Core.Prompts;

/// <summary>
/// Describes one prompt the player must answer: mode, difficulty, answer set and allowed intents. <see cref="Text"/> is the line the agent says
/// before the player answers, when the prompt has one. A prompt without text is written without the member, so its canonical JSON is unchanged.
/// </summary>
public sealed record PromptSpec(
    string PromptId,
    RecognitionMode Mode,
    Difficulty Difficulty,
    string AnswerSetId,
    IReadOnlyList<string> AllowedIntentIds,
    string? AgentId,
    IReadOnlyList<string> SlotGrammarIds,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Text = null)
{
    /// <summary>Structural equality including list contents and the prompt text.</summary>
    public bool Equals(PromptSpec? other)
    {
        return other is not null
            && string.Equals(PromptId, other.PromptId, StringComparison.Ordinal)
            && Mode == other.Mode
            && Difficulty == other.Difficulty
            && string.Equals(AnswerSetId, other.AnswerSetId, StringComparison.Ordinal)
            && ValueEquality.ListEquals(AllowedIntentIds, other.AllowedIntentIds)
            && string.Equals(AgentId, other.AgentId, StringComparison.Ordinal)
            && ValueEquality.ListEquals(SlotGrammarIds, other.SlotGrammarIds)
            && string.Equals(Text, other.Text, StringComparison.Ordinal);
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
        hash.Add(AgentId);
        ValueEquality.AddList(ref hash, SlotGrammarIds);
        hash.Add(Text, StringComparer.Ordinal);
        return hash.ToHashCode();
    }
}
