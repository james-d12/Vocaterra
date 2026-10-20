using Slg.Core.Prompts;
using Slg.Core.Stages;
using Slg.Core.Verdicts;
using MatchType = Slg.Core.Stages.MatchType;

namespace Slg.Evaluation;

/// <summary>Everything verdict assembly needs: the outputs of every earlier stage plus configuration and version stamp.</summary>
/// <param name="Prompt">The prompt being answered; its mode and difficulty steer assembly.</param>
/// <param name="Asr">Word recognizer output.</param>
/// <param name="Match">Answer-set match; <see cref="MatchType.None"/> when nothing matched.</param>
/// <param name="Analysis">Linguistic analysis of the normalized transcript.</param>
/// <param name="Intent">Resolved intent and slots.</param>
/// <param name="Pronunciation">Pronunciation scoring, or null when it did not run.</param>
/// <param name="RequiredSlots">Slot names the resolved intent needs, supplied from the intent catalog.</param>
/// <param name="KnownLexiconWords">Normalized words the lexicon knows; used to tell a wrong word from an unknown one.</param>
/// <param name="Config">Evaluation weights and thresholds.</param>
/// <param name="Versions">Content and model hashes copied into the verdict.</param>
public sealed record VerdictInputs(
    PromptSpec Prompt,
    AsrResult Asr,
    MatchResult Match,
    AnalysisResult Analysis,
    IntentResolution Intent,
    PronunciationResult? Pronunciation,
    IReadOnlyList<string> RequiredSlots,
    IReadOnlyList<string> KnownLexiconWords,
    EvaluationConfig Config,
    VersionStamp Versions);
