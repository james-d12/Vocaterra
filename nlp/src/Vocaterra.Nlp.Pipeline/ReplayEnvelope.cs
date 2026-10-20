using Vocaterra.Nlp.Core.Replay;
using Vocaterra.Nlp.Core.Stages;

namespace Vocaterra.Nlp.Pipeline;

/// <summary>
/// One replay log entry: the core <see cref="ReplayRecord"/> plus everything needed to re-evaluate it without a model or a recording:
/// the recognizers' outputs, the game context and the full pipeline result to compare against. Audio is never persisted.
/// </summary>
/// <param name="Record">The core replay record (clip hash, prompt id, game state hash, version stamp and verdict).</param>
/// <param name="Asr">The word recognizer output, served back by a <c>ReplayWordRecognizer</c> when replaying.</param>
/// <param name="Posteriors">The phoneme recognizer output, or null when pronunciation scoring did not run.</param>
/// <param name="Context">The game context the utterance was evaluated in.</param>
/// <param name="Result">The recorded pipeline result; its decision, response, effects and repair are compared on replay.</param>
public sealed record ReplayEnvelope(
    ReplayRecord Record,
    AsrResult Asr,
    PhonePosteriors? Posteriors,
    GameContext Context,
    PipelineResult Result)
{
    /// <summary>Builds the envelope for a traced evaluation.</summary>
    public static ReplayEnvelope From(PipelineTrace trace, GameContext context)
    {
        ArgumentNullException.ThrowIfNull(trace);
        ArgumentNullException.ThrowIfNull(context);
        return new ReplayEnvelope(trace.Result.Replay, trace.Asr, trace.Posteriors, context, trace.Result);
    }
}
