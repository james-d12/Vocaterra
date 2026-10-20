using Vocaterra.Nlp.Asr;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Replay;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Nlp.Intent;
using Vocaterra.Nlp.Response.Effects;
using Vocaterra.Nlp.Response.Templates;

namespace Vocaterra.Nlp.Pipeline;

/// <summary>Re-evaluates logged utterances and reports every difference from what was recorded.</summary>
public static class Replayer
{
    /// <summary>
    /// Replays an envelope with its recorded recognizer outputs (no model needed) through the pipeline's content and options, then compares the
    /// core record (<c>/clipHash</c>, <c>/versions</c>, <c>/verdict</c> and so on) and the downstream result (<c>/decision</c>, <c>/response</c>,
    /// <c>/effects</c>, <c>/repair</c>) with the recording.
    /// </summary>
    /// <returns>Null when everything is identical; otherwise the JSON pointer paths that differ.</returns>
    /// <exception cref="InvalidDataException">The recorded prompt id is not in the pipeline's content bundle.</exception>
    public static ReplayDiff? Replay(ReplayEnvelope envelope, UtterancePipeline pipeline, Func<string, PcmClip> clipByHash)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(clipByHash);
        var record = envelope.Record;
        var clip = clipByHash(record.ClipHash);
        var prompt = PromptOf(pipeline, record.PromptId);

        var words = new ReplayWordRecognizer(new Dictionary<string, AsrResult>(StringComparer.Ordinal) { [clip.ClipHash] = envelope.Asr });
        IPhonemeRecognizer? phonemes = null;
        if (envelope.Posteriors is not null)
        {
            record.Versions.ModelHashes.TryGetValue(PipelineServices.PhonemeKey, out var phonemeHash);
            phonemes = new RecordedPhonemeRecognizer(envelope.Posteriors, phonemeHash ?? "recorded");
        }

        var replayPipeline = new UtterancePipeline(pipeline.Services.WithRecognizers(words, phonemes), pipeline.Options);
        var result = replayPipeline.Evaluate(clip, prompt, envelope.Context);

        var paths = JsonDiff.Paths(CanonicalJson.Serialize(record), CanonicalJson.Serialize(result.Replay));
        paths.AddRange(JsonDiff.Paths(CanonicalJson.Serialize(Downstream.Of(envelope.Result)), CanonicalJson.Serialize(Downstream.Of(result))));
        return paths.Count == 0 ? null : new ReplayDiff(paths);
    }

    /// <summary>
    /// Replays a bare core record through the pipeline's own recognizers (so real models re-run on the clip returned by
    /// <paramref name="clipByHash"/>) in the given conversation context, and compares the new core record with the recorded one.
    /// </summary>
    /// <returns>Null when the records are identical; otherwise the JSON pointer paths that differ.</returns>
    /// <exception cref="InvalidDataException">The recorded prompt id is not in the pipeline's content bundle.</exception>
    public static ReplayDiff? Replay(ReplayRecord record, ConversationContext context, UtterancePipeline pipeline, Func<string, PcmClip> clipByHash)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(clipByHash);
        var clip = clipByHash(record.ClipHash);
        var result = pipeline.Evaluate(clip, PromptOf(pipeline, record.PromptId), context);
        var paths = JsonDiff.Paths(CanonicalJson.Serialize(record), CanonicalJson.Serialize(result.Replay));
        return paths.Count == 0 ? null : new ReplayDiff(paths);
    }

    private static PromptSpec PromptOf(UtterancePipeline pipeline, string promptId)
    {
        return pipeline.Services.Content.Prompts.TryGetValue(promptId, out var prompt)
            ? prompt
            : throw new InvalidDataException($"The content bundle has no prompt '{promptId}'.");
    }

    // The parts of a result that the core record does not already cover; the property names become the first path segment.
    private sealed record Downstream(
        DecisionOutcome Decision,
        ResponsePlan Response,
        IReadOnlyList<ResolvedEffect> Effects,
        RepairStep? Repair)
    {
        public static Downstream Of(PipelineResult result)
        {
            return new Downstream(result.Decision, result.Response, result.Effects, result.Repair);
        }
    }
}
