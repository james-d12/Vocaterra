using Vocaterra.Nlp.Core.Prompts;

namespace Vocaterra.Nlp.Bench;

/// <summary>One annotated learner error in a golden manifest entry: the taxonomy tag and the (wrong) word the learner actually said.</summary>
/// <param name="Tag">The error tag, for example <c>conj.person</c>.</param>
/// <param name="Actual">The word or words the learner said, for example <c>tiene</c>.</param>
public sealed record ExpectedError(string Tag, string Actual);

/// <summary>One line of <c>golden/manifest.jsonl</c>.</summary>
/// <param name="Clip">The audio file, relative to the manifest's directory (or the <c>--audio-dir</c>).</param>
/// <param name="PromptId">The prompt the speaker was answering; it selects the answer set and the recognition mode.</param>
/// <param name="Speaker">An opaque speaker label (never a name).</param>
/// <param name="L1">The speaker's first language, as a language code.</param>
/// <param name="ExpectedTranscript">What a perfect recognizer returns: the words actually spoken, errors included.</param>
/// <param name="ExpectedErrors">The annotated learner errors the transcript should keep.</param>
/// <param name="ExpectedIntent">The intent the utterance should resolve to, or null for prompts without intents.</param>
public sealed record ManifestEntry(
    string Clip,
    string PromptId,
    string Speaker,
    string L1,
    string ExpectedTranscript,
    IReadOnlyList<ExpectedError> ExpectedErrors,
    string? ExpectedIntent);

/// <summary>The outcome of benchmarking one manifest entry.</summary>
/// <param name="Clip">The clip path from the manifest.</param>
/// <param name="PromptId">The prompt id from the manifest.</param>
/// <param name="Mode">The recognition mode used.</param>
/// <param name="Status"><c>ok</c>, <c>missing-clip</c> or <c>unknown-prompt</c>; only <c>ok</c> entries count in the statistics.</param>
/// <param name="ModelHash">The recognizer's model hash, empty when nothing ran.</param>
/// <param name="Hypothesis">The recognizer's transcript, empty when nothing ran.</param>
/// <param name="ReferenceWords">The number of normalized words in the expected transcript.</param>
/// <param name="Edits">The word-level Levenshtein distance between the hypothesis and the expected transcript.</param>
/// <param name="Wer">Edits divided by reference words; null when nothing ran.</param>
/// <param name="ErrorsExpected">How many annotated errors the entry has.</param>
/// <param name="ErrorsKept">How many of them are still present in the hypothesis.</param>
/// <param name="LatencyMs">Wall-clock recognition time in milliseconds; 0 when nothing ran.</param>
public sealed record EntryResult(
    string Clip,
    string PromptId,
    RecognitionMode Mode,
    string Status,
    string ModelHash,
    string Hypothesis,
    int ReferenceWords,
    int Edits,
    double? Wer,
    int ErrorsExpected,
    int ErrorsKept,
    double LatencyMs);

/// <summary>Aggregate figures for all successful entries that share a model and a mode.</summary>
/// <param name="ModelHash">The recognizer's model hash.</param>
/// <param name="Mode">The recognition mode.</param>
/// <param name="Entries">How many entries were recognized.</param>
/// <param name="Wer">Corpus word error rate: total edits over total reference words.</param>
/// <param name="ErrorPreservation">Share of annotated learner errors still present in the hypotheses; null when no entry had an annotated error.</param>
/// <param name="LatencyP50Ms">Median recognition latency in milliseconds (nearest rank).</param>
/// <param name="LatencyP95Ms">95th percentile recognition latency in milliseconds (nearest rank).</param>
public sealed record GroupSummary(
    string ModelHash,
    RecognitionMode Mode,
    int Entries,
    double Wer,
    double? ErrorPreservation,
    double LatencyP50Ms,
    double LatencyP95Ms);

/// <summary>The whole benchmark result: one row per manifest entry plus one summary per model and mode.</summary>
/// <param name="Entries">One result per manifest entry, in manifest order.</param>
/// <param name="Groups">One summary per (model, mode), ordered by model hash then mode.</param>
public sealed record BenchReport(IReadOnlyList<EntryResult> Entries, IReadOnlyList<GroupSummary> Groups);
