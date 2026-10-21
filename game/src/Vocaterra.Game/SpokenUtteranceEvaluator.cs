using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Language.Embeddings;
using Vocaterra.Nlp.Pipeline;
using Vocaterra.Nlp.Response.Speech;

namespace Vocaterra.Game;

/// <summary>
/// The call sequence of the core's API document for one spoken line: the clip (16 kHz mono) goes through the pipeline with the real word
/// recognizer (Vosk), which decodes it once. The result carries the recognizer's output (<see cref="PipelineTrace.Asr"/>), so the guide's
/// second evaluation (CR-017) is made over that recognition with <see cref="TypedUtteranceEvaluator.EvaluateRecognized"/> and the same
/// transcript and confidence are seen twice with one decode.
/// </summary>
/// <remarks>
/// <b>Threads.</b> The recognizer is not safe for concurrent calls and must not be disposed while a call may run (core API section 8): one
/// <see cref="SpokenUtteranceEvaluator"/> belongs to one thread, the game's turn worker. The pipeline is built without concurrent recognizers,
/// so Vosk is always called on the calling thread, never on a pool thread.
/// </remarks>
public sealed class SpokenUtteranceEvaluator
{
    /// <summary>
    /// The number of n-best hypotheses asked of Vosk: one. With an n-best list the core's <c>AsrResult.Confidence</c> is a softmax over
    /// near-identical hypotheses and falls to 0.34 to 0.66 for a clean, correct phrase (measured on Kokoro speech with the small model), under
    /// the verdict's communicated threshold of 0.6, so a good recognition would count as not understood (CR-022). With one hypothesis Vosk
    /// gives per-word confidences instead and the result's confidence is 1.0; the intent stage then decides whether the words were understood.
    /// </summary>
    public const int SingleBest = 1;

    private readonly UtterancePipeline _pipeline;

    /// <summary>Creates an evaluator over a loaded bundle and a word recognizer.</summary>
    /// <param name="bundle">The content bundle.</param>
    /// <param name="recognizer">The word recognizer (Vosk); the caller keeps it alive and calls this evaluator from one thread.</param>
    public SpokenUtteranceEvaluator(ContentBundle bundle, IWordRecognizer recognizer)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(recognizer);
        var services = new PipelineServices(recognizer, null, new FakeEmbeddingModel(), new SilentSynthesizer(), bundle);
        _pipeline = new UtterancePipeline(services, new PipelineOptions { RunRecognizersConcurrently = false, MaxAlternatives = SingleBest });
    }

    /// <summary>
    /// Recognizes and evaluates <paramref name="clip"/> on <paramref name="prompt"/> in <paramref name="context"/>. Pure given the recognizer:
    /// the same clip, prompt and context give the same trace.
    /// </summary>
    public PipelineTrace Trace(PcmClip clip, PromptSpec prompt, ConversationContext context)
    {
        ArgumentNullException.ThrowIfNull(clip);
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(context);
        return _pipeline.Trace(clip, prompt, context);
    }
}
