using System.Diagnostics;
using Vocaterra.Nlp.Asr;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Language.Normalization;

namespace Vocaterra.Nlp.Bench;

/// <summary>Word-level metrics of the ASR benchmark; pure functions over normalized tokens.</summary>
public static class BenchMetrics
{
    private static readonly NormalizationOptions Options = new(ConvertIndefiniteArticles: false);

    /// <summary>Normalizes text with the pipeline's options and returns its tokens; the recognizer's <c>[unk]</c> marker is dropped.</summary>
    public static IReadOnlyList<string> Tokens(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var cleaned = text.Replace(GrammarBuilder.UnknownToken, " ", StringComparison.Ordinal);
        return TextNormalizer.Normalize(cleaned, Options).Tokens.Select(static token => token.Text).ToArray();
    }

    /// <summary>The word-level Levenshtein distance (insertions, deletions and substitutions all cost 1).</summary>
    public static int WordEdits(IReadOnlyList<string> reference, IReadOnlyList<string> hypothesis)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(hypothesis);
        var previous = new int[hypothesis.Count + 1];
        var current = new int[hypothesis.Count + 1];
        for (var j = 0; j <= hypothesis.Count; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= reference.Count; i++)
        {
            current[0] = i;
            for (var j = 1; j <= hypothesis.Count; j++)
            {
                var substitution = previous[j - 1] + (string.Equals(reference[i - 1], hypothesis[j - 1], StringComparison.Ordinal) ? 0 : 1);
                current[j] = Math.Min(substitution, Math.Min(previous[j] + 1, current[j - 1] + 1));
            }

            (previous, current) = (current, previous);
        }

        return previous[hypothesis.Count];
    }

    /// <summary>The word error rate of one utterance: edits over reference words; an empty reference scores 0 for an empty hypothesis and 1 otherwise.</summary>
    public static double Wer(int edits, int referenceWords)
    {
        return referenceWords == 0 ? (edits == 0 ? 0.0 : 1.0) : (double)edits / referenceWords;
    }

    /// <summary>Counts the annotated errors whose wrong word (or word sequence) still appears, contiguously, in the hypothesis.</summary>
    public static int ErrorsKept(IReadOnlyList<ExpectedError> errors, IReadOnlyList<string> hypothesisTokens)
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(hypothesisTokens);
        var kept = 0;
        foreach (var error in errors)
        {
            var actual = Tokens(error.Actual);
            if (actual.Count > 0 && ContainsSequence(hypothesisTokens, actual))
            {
                kept++;
            }
        }

        return kept;
    }

    /// <summary>The nearest-rank percentile (fraction in (0, 1]) of the values; 0 for an empty list.</summary>
    public static double Percentile(IReadOnlyList<double> values, double fraction)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
        {
            return 0.0;
        }

        var sorted = values.Order().ToArray();
        var rank = (int)Math.Ceiling(fraction * sorted.Length);
        return sorted[Math.Clamp(rank, 1, sorted.Length) - 1];
    }

    private static bool ContainsSequence(IReadOnlyList<string> haystack, IReadOnlyList<string> needle)
    {
        for (var start = 0; start + needle.Count <= haystack.Count; start++)
        {
            var match = true;
            for (var i = 0; i < needle.Count && match; i++)
            {
                match = string.Equals(haystack[start + i], needle[i], StringComparison.Ordinal);
            }

            if (match)
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>Reads <c>tests/golden/manifest.jsonl</c>.</summary>
public static class ManifestReader
{
    /// <summary>Reads one <see cref="ManifestEntry"/> per non-blank line.</summary>
    /// <exception cref="InvalidDataException">A line is not a valid entry; the message names the line number.</exception>
    public static IReadOnlyList<ManifestEntry> Read(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var entries = new List<ManifestEntry>();
        var number = 0;
        while (reader.ReadLine() is { } line)
        {
            number++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                entries.Add(Check(CanonicalJson.Deserialize<ManifestEntry>(line)));
            }
            catch (System.Text.Json.JsonException ex)
            {
                throw new InvalidDataException($"Manifest line {number}: {ex.Message}", ex);
            }
        }

        return entries;
    }

    // A missing property arrives as null even though the members are declared non-nullable, so check explicitly.
    private static ManifestEntry Check(ManifestEntry entry)
    {
        if ((object?)entry.Clip is null || (object?)entry.PromptId is null || (object?)entry.Speaker is null || (object?)entry.L1 is null
            || (object?)entry.ExpectedTranscript is null || (object?)entry.ExpectedErrors is null || entry.ExpectedErrors.Any(static e => (object?)e is null || (object?)e.Tag is null || (object?)e.Actual is null))
        {
            throw new System.Text.Json.JsonException("The entry needs clip, promptId, speaker, l1, expectedTranscript and expectedErrors (a list of {tag, actual}).");
        }

        return entry;
    }
}

/// <summary>Runs a recognizer over the golden manifest and computes WER, learner-error preservation and latency. Wall-clock timing lives here only.</summary>
public static class BenchRunner
{
    /// <summary>Status of an entry that was recognized.</summary>
    public const string StatusOk = "ok";

    /// <summary>Status of an entry whose audio file could not be found.</summary>
    public const string StatusMissingClip = "missing-clip";

    /// <summary>Status of an entry whose prompt is not in the content bundle.</summary>
    public const string StatusUnknownPrompt = "unknown-prompt";

    private const int Decimals = 4;

    /// <summary>
    /// Benchmarks every entry. <paramref name="recognizerFor"/> supplies the recognizer for a mode and <paramref name="clipFor"/> the audio (null when the
    /// file is missing); <paramref name="modeOverride"/> forces one recognition mode for all entries instead of each prompt's own.
    /// </summary>
    public static BenchReport Run(
        IReadOnlyList<ManifestEntry> entries,
        ContentBundle content,
        Func<RecognitionMode, IWordRecognizer> recognizerFor,
        Func<ManifestEntry, PcmClip?> clipFor,
        RecognitionMode? modeOverride = null)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(recognizerFor);
        ArgumentNullException.ThrowIfNull(clipFor);
        var results = new List<EntryResult>(entries.Count);
        foreach (var entry in entries)
        {
            results.Add(RunEntry(entry, content, recognizerFor, clipFor, modeOverride));
        }

        return new BenchReport(results, Summarize(results));
    }

    private static EntryResult RunEntry(
        ManifestEntry entry,
        ContentBundle content,
        Func<RecognitionMode, IWordRecognizer> recognizerFor,
        Func<ManifestEntry, PcmClip?> clipFor,
        RecognitionMode? modeOverride)
    {
        var errorCount = entry.ExpectedErrors.Count;
        if (!content.Prompts.TryGetValue(entry.PromptId, out var prompt) || !content.AnswerSets.TryGetValue(prompt.AnswerSetId, out var answerSet))
        {
            return Skipped(entry, modeOverride ?? RecognitionMode.Constrained, StatusUnknownPrompt, errorCount);
        }

        var mode = modeOverride ?? prompt.Mode;
        var clip = clipFor(entry);
        if (clip is null)
        {
            return Skipped(entry, mode, StatusMissingClip, errorCount);
        }

        var recognizer = recognizerFor(mode);
        var request = mode == RecognitionMode.Constrained
            ? RecognitionRequest.Constrained(GrammarBuilder.BuildList(answerSet.GrammarPhrases()))
            : RecognitionRequest.Open();
        var stopwatch = Stopwatch.StartNew();
        var asr = recognizer.Recognize(clip, request);
        stopwatch.Stop();

        var reference = BenchMetrics.Tokens(entry.ExpectedTranscript);
        var hypothesis = BenchMetrics.Tokens(asr.Transcript);
        var edits = BenchMetrics.WordEdits(reference, hypothesis);
        return new EntryResult(
            entry.Clip,
            entry.PromptId,
            mode,
            StatusOk,
            recognizer.ModelHash,
            asr.Transcript,
            reference.Count,
            edits,
            Math.Round(BenchMetrics.Wer(edits, reference.Count), Decimals),
            errorCount,
            BenchMetrics.ErrorsKept(entry.ExpectedErrors, hypothesis),
            Math.Round(stopwatch.Elapsed.TotalMilliseconds, 3));
    }

    private static EntryResult Skipped(ManifestEntry entry, RecognitionMode mode, string status, int errorCount)
    {
        return new EntryResult(entry.Clip, entry.PromptId, mode, status, string.Empty, string.Empty, 0, 0, null, errorCount, 0, 0.0);
    }

    private static List<GroupSummary> Summarize(IReadOnlyList<EntryResult> results)
    {
        var groups = results
            .Where(static result => string.Equals(result.Status, StatusOk, StringComparison.Ordinal))
            .GroupBy(static result => (result.ModelHash, result.Mode))
            .OrderBy(static group => group.Key.ModelHash, StringComparer.Ordinal)
            .ThenBy(static group => group.Key.Mode);
        var summaries = new List<GroupSummary>();
        foreach (var group in groups)
        {
            var words = group.Sum(static result => result.ReferenceWords);
            var edits = group.Sum(static result => result.Edits);
            var expected = group.Sum(static result => result.ErrorsExpected);
            var kept = group.Sum(static result => result.ErrorsKept);
            var latencies = group.Select(static result => result.LatencyMs).ToArray();
            summaries.Add(new GroupSummary(
                group.Key.ModelHash,
                group.Key.Mode,
                group.Count(),
                Math.Round(BenchMetrics.Wer(edits, words), Decimals),
                expected == 0 ? null : Math.Round((double)kept / expected, Decimals),
                BenchMetrics.Percentile(latencies, 0.50),
                BenchMetrics.Percentile(latencies, 0.95)));
        }

        return summaries;
    }
}
