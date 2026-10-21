using Vocaterra.Nlp.Asr;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Language.Embeddings;
using Vocaterra.Nlp.Pipeline;
using Vocaterra.Nlp.Response.Speech;

namespace Vocaterra.Game;

/// <summary>
/// The call sequence of the core's API document for one typed line: the line is the transcript (the core's scripted recognizer), the clip is
/// empty, and the embedding model and the synthesizer are the core's stand-ins, so no model file and no audio take part. The console host
/// (<c>ConversationSession</c>) and the guide (<see cref="GuideService"/>) both evaluate through this class, so the two cannot drift apart.
/// <see cref="EvaluateRecognized"/> does the same from a recognition that already exists, which is how the guide's second evaluation of a
/// spoken line reuses the first one's transcript instead of running the recognizer again.
/// </summary>
public sealed class TypedUtteranceEvaluator
{
    /// <summary>The model hash the scripted recognizer reports in the verdict's versions.</summary>
    public const string ScriptedModelHash = "scripted-transcript";

    private readonly ContentBundle _bundle;
    private readonly IEmbeddingModel _embeddings = new FakeEmbeddingModel();
    private readonly ISpeechSynthesizer _synthesizer = new SilentSynthesizer();

    /// <summary>Creates an evaluator over a loaded bundle.</summary>
    public TypedUtteranceEvaluator(ContentBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        _bundle = bundle;
    }

    /// <summary>
    /// Evaluates <paramref name="transcript"/> on <paramref name="prompt"/> in <paramref name="context"/>, whose repeat counts are passed to
    /// the core as they are. Pure after the call: the same arguments give the same result, as nothing here reads a clock or draws a random
    /// number.
    /// </summary>
    public PipelineResult Evaluate(string transcript, PromptSpec prompt, ConversationContext context)
    {
        ArgumentNullException.ThrowIfNull(transcript);
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(context);
        var asr = new AsrResult(transcript, [new AsrHypothesis(transcript, 1.0)], [], 1.0, ScriptedModelHash, prompt.Mode);
        return EvaluateRecognized(asr, PcmClip.FromSamples([]), prompt, context);
    }

    /// <summary>
    /// Evaluates a recognition that has already been made: the recognizer is the core's scripted one over <paramref name="recognition"/>, so the
    /// transcript, the hypotheses and the confidence are exactly those of the first recognition and nothing is decoded again. The
    /// <paramref name="clip"/> goes into the replay record as it is (its hash), so a spoken turn's record names the audio it came from.
    /// </summary>
    public PipelineResult EvaluateRecognized(AsrResult recognition, PcmClip clip, PromptSpec prompt, ConversationContext context)
    {
        ArgumentNullException.ThrowIfNull(recognition);
        ArgumentNullException.ThrowIfNull(clip);
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(context);
        var services = new PipelineServices(new ScriptedWordRecognizer(recognition), null, _embeddings, _synthesizer, _bundle);
        return new UtterancePipeline(services).Evaluate(clip, prompt, context);
    }
}
