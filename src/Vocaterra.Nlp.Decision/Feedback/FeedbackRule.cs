using Vocaterra.Nlp.Core.Prompts;

namespace Vocaterra.Nlp.Decision.Feedback;

/// <summary>
/// One row of the feedback table. <see cref="TagPattern"/> is an exact tag, a family wildcard (<c>pron.*</c>) or <c>*</c>;
/// the rule applies when the repeat count lies in [<see cref="MinRepeat"/>, <see cref="MaxRepeat"/>] and, if set, the difficulty equals <see cref="Difficulty"/>.
/// </summary>
public sealed record FeedbackRule(
    string TagPattern,
    int MinRepeat,
    int MaxRepeat,
    Difficulty? Difficulty,
    FeedbackType Type);
