using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Core.Verdicts;
using MatchType = Vocaterra.Nlp.Core.Stages.MatchType;

namespace Vocaterra.Nlp.Evaluation;

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
/// <param name="CanonicalAnswers">The canonical answers of the prompt's answer set; a derived <c>vocab.wrong</c> of a line that resolved no intent names the set's word when there is exactly one. Null when unknown.</param>
/// <param name="ErrorTags">The error tags of the language and their ranks, which order errors at one position (M34, additive); null for the frozen v1 set, which is the Spanish one.</param>
/// <param name="NearMiss">
/// The line's near miss of the answer set (M75, <c>AnswerNearMisses.Find</c>): the one word by which it misses the nearest answer phrase, a slip pair. Null when there
/// is none, and when the line matched an answer or the prompt is open (the pipeline and <c>TextGrader</c> then do not look; the assembler ignores it there).
/// </param>
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
    VersionStamp Versions,
    IReadOnlyList<string>? CanonicalAnswers = null,
    ErrorTagSet? ErrorTags = null,
    AnswerNearMiss? NearMiss = null);
