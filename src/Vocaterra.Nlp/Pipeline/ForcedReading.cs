using Vocaterra.Nlp.Asr;
using Vocaterra.Nlp.Audio;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Stages;
using MatchType = Vocaterra.Nlp.Core.Stages.MatchType;

namespace Vocaterra.Nlp.Pipeline;

/// <summary>
/// M84: whether a taken constrained reading looks forced. The constrained decode hears only answer phrases, so it can put a different line onto an answer row with
/// confidence, and the reading is taken (V31). Two facts of the reading give it away, both measured on Kokoro speech (<c>docs/nlp/implementation-plan.md</c>, M84):
/// the reading is no answer row heard whole (its answer match is <see cref="MatchType.None"/> or <see cref="MatchType.Fuzzy"/>), or the recognized words leave most of
/// the speech in the clip unused. Pure functions of the clip and the recognition, so a replay (which has an empty clip) and a live run decide alike where it matters:
/// the open decode only ever adds a reading that wins when its intent differs.
/// </summary>
internal static class ForcedReading
{
    /// <summary>Whether the first reading, which is taken, should be checked against a free decode of the clip.</summary>
    public static bool LooksForced(PcmClip clip, UtteranceAnalysis first, PipelineOptions options)
    {
        if (options.OpenPassOnLooseMatch && first.Answer.Type is MatchType.None or MatchType.Fuzzy)
        {
            return true;
        }

        return options.OpenPassMinCoverage > 0.0 && Coverage(clip, first.Asr) is { } coverage && coverage < options.OpenPassMinCoverage;
    }

    /// <summary>
    /// The share of the clip's voiced frames (20 ms, at or above -40 dBFS, as <see cref="EnergyVad"/> decides) whose middle lies inside a recognized word, <c>[unk]</c>
    /// left out; <see langword="null"/> when the clip has no voiced frame or the recognition has no such word.
    /// </summary>
    public static double? Coverage(PcmClip clip, AsrResult asr)
    {
        var words = asr.Words.Where(static w => !string.Equals(w.Text, GrammarBuilder.UnknownToken, StringComparison.Ordinal)).ToList();
        if (words.Count == 0 || clip.Samples.Length == 0)
        {
            return null;
        }

        var vad = new EnergyVad(sampleRate: clip.SampleRate);
        var speech = vad.SpeechFrames(clip.Samples);
        var voiced = 0;
        var covered = 0;
        for (var frame = 0; frame < speech.Length; frame++)
        {
            if (!speech[frame])
            {
                continue;
            }

            voiced++;
            var start = frame * vad.FrameSamples;
            var length = Math.Min(vad.FrameSamples, clip.Samples.Length - start);
            var middle = (start + (length / 2.0)) / clip.SampleRate;
            if (words.Any(w => middle >= w.StartSeconds && middle <= w.EndSeconds))
            {
                covered++;
            }
        }

        return voiced == 0 ? null : covered / (double)voiced;
    }
}
