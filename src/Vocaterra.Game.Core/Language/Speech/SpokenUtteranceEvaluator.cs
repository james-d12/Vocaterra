using Vocaterra.Nlp.Asr;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Language.Embeddings;
using Vocaterra.Nlp.Pipeline;
using Vocaterra.Nlp.Response.Speech;

namespace Vocaterra.Game.Core.Language.Speech;

/// <summary>
/// The call sequence of the core's API document for one spoken line: the clip (16 kHz mono) goes through the pipeline with the real word
/// recognizer (Vosk), which decodes it once. The result carries the recognizer's output (<see cref="PipelineTrace.Asr"/>). The NPCs use the two
/// phases (CR-017): <see cref="Analyze"/> decodes the clip once and <see cref="CompleteTrace"/> completes the analysis in a context, as often as
/// the host needs, so the transcript and the confidence are the same in every completion.
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

    private static readonly PipelineOptions Options = new() { RunRecognizersConcurrently = false, MaxAlternatives = SingleBest };

    // V31: free talk adds the core's open decode (M80) for a line the constrained decode did not take; the floor is the core's default (0.5).
    private static readonly PipelineOptions FreeTalkOptions = new() { RunRecognizersConcurrently = false, MaxAlternatives = SingleBest, OpenRecognitionFallback = true };

    /// <summary>
    /// The seven constrained prompts free talk waits on (the owner, 2026-11-06, answer 4): the four NPC open prompts (the baker's, the smith's, the farmer's, the fisher's; the villager's and the guide's are open already), the shopkeeper's two and the
    /// guide's offer. Only a line said at one of them in free talk is decoded a second time, freely, when the constrained decode does not take it; every other prompt (lesson steps,
    /// reviews, the mic check, the baker's) decodes once.
    /// </summary>
    public static IReadOnlySet<string> FreeTalkPrompts { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "baker-open", "smith-open", "farmer-open", "fisher-open", "stall-need", "stall-offer", "guide-offer",
    };

    private readonly ContentBundle _bundle;
    private readonly UtterancePipeline _pipeline;
    private readonly UtterancePipeline _freeTalk;

    /// <summary>Creates an evaluator over a loaded bundle and a word recognizer.</summary>
    /// <param name="bundle">The content bundle.</param>
    /// <param name="recognizer">The word recognizer (Vosk); the caller keeps it alive and calls this evaluator from one thread.</param>
    public SpokenUtteranceEvaluator(ContentBundle bundle, IWordRecognizer recognizer)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(recognizer);
        _bundle = bundle;
        var services = new PipelineServices(recognizer, null, new FakeEmbeddingModel(), new SilentSynthesizer(), bundle);
        _pipeline = new UtterancePipeline(services, Options);
        _freeTalk = new UtterancePipeline(services, FreeTalkOptions);
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

    /// <summary>
    /// Phase 1 of a spoken line (CR-017): the clip is recognized, once, and read. Complete the analysis with <see cref="CompleteTrace"/>; the
    /// recognizer is not called again.
    /// </summary>
    public UtteranceAnalysis Analyze(PcmClip clip, PromptSpec prompt) => Analyze(clip, prompt, false);

    /// <summary>
    /// Phase 1 of a spoken line (CR-017), with free talk told (V31): at one of the <see cref="FreeTalkPrompts"/> in free talk (<paramref name="freeTalk"/>) a line the
    /// constrained decode does not take is decoded a second time, freely, and read from that (core M80); <see cref="UtteranceAnalysis.OtherPass"/> keeps the
    /// recognition not chosen. Anywhere else the clip is decoded once, as before.
    /// </summary>
    /// <param name="clip">The clip.</param>
    /// <param name="prompt">The prompt.</param>
    /// <param name="freeTalk">Whether the turn is free talk (no lesson step, no review).</param>
    public UtteranceAnalysis Analyze(PcmClip clip, PromptSpec prompt, bool freeTalk)
    {
        ArgumentNullException.ThrowIfNull(clip);
        ArgumentNullException.ThrowIfNull(prompt);
        return (freeTalk && FreeTalkPrompts.Contains(prompt.PromptId) ? _freeTalk : _pipeline).Analyze(clip, prompt);
    }

    /// <summary>
    /// Phase 1 over a recognition already made from <paramref name="clip"/>, changed by the host (the listener's name said as a vocative taken out, V14): the
    /// recognizer is the core's scripted one over <paramref name="recognition"/>, whose model hash is the real recognizer's, so the analysis completes with
    /// <see cref="CompleteTrace"/> like any other and its replay record names the clip it came from.
    /// </summary>
    /// <param name="recognition">The recognition to read.</param>
    /// <param name="clip">The clip it was made from.</param>
    /// <param name="prompt">The prompt.</param>
    public UtteranceAnalysis AnalyzeRecognized(AsrResult recognition, PcmClip clip, PromptSpec prompt)
    {
        ArgumentNullException.ThrowIfNull(recognition);
        ArgumentNullException.ThrowIfNull(clip);
        ArgumentNullException.ThrowIfNull(prompt);
        var services = new PipelineServices(new ScriptedWordRecognizer(recognition), null, new FakeEmbeddingModel(), new SilentSynthesizer(), _bundle);
        return new UtterancePipeline(services, Options).Analyze(clip, prompt);
    }

    /// <summary>Phase 2 for an analysis made by <see cref="Analyze"/> or <see cref="AnalyzeRecognized"/>, in the context. Pure.</summary>
    public PipelineTrace CompleteTrace(UtteranceAnalysis analysis, ConversationContext context)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(context);
        return _pipeline.CompleteTrace(analysis, context);
    }
}
