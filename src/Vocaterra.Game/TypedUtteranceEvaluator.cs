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
        var services = new PipelineServices(new ScriptedWordRecognizer(asr), null, _embeddings, _synthesizer, _bundle);
        return new UtterancePipeline(services).Evaluate(PcmClip.FromSamples([]), prompt, context);
    }

    /// <summary>
    /// Evaluates the line so that the core's feedback and repair ladders count this utterance (CR-012). The core reads
    /// <c>ConversationContext.RepeatCountsByTag</c> as "seen in a row, <i>including</i> this utterance" when it plans feedback and repair, but
    /// <c>ConversationContext.Next</c> carries the counts <i>before</i> the next utterance, so evaluating with the carried counts makes the
    /// second mistake in a row a recast again and the second failure to be understood "ask again" again. The workaround, which the public
    /// API allows because the pipeline is pure: evaluate once to get the verdict, add it to the counts with <c>RepeatCounts.Next</c>, and
    /// evaluate again with those counts. The caller still carries the context with <c>Next</c> from the context it passed in.
    /// </summary>
    public PipelineResult EvaluateCountingThisUtterance(string transcript, PromptSpec prompt, ConversationContext context)
    {
        var first = Evaluate(transcript, prompt, context);
        var counted = context with { RepeatCountsByTag = RepeatCounts.Next(context.RepeatCountsByTag, first.Verdict) };
        return Evaluate(transcript, prompt, counted);
    }
}
