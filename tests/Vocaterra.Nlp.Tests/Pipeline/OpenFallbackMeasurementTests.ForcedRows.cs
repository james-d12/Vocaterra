using System.Globalization;
using System.Text;
using Vocaterra.Nlp.Asr;
using Vocaterra.Nlp.Asr.Tests;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Stages;

namespace Vocaterra.Nlp.Pipeline.Tests;

/// <summary>
/// M84, the forced rows: the constrained decode can force a different line onto an answer row with confidence, so the reading is taken and the open decode of
/// M80 never runs. This part of the measurement takes every taken constrained reading (an answer row said by a Kokoro voice, a line the catalog or nothing takes
/// said at every one of the seven prompts) and records the signals a trigger could use, next to what the free decode would have made of the same clip.
/// </summary>
public sealed partial class OpenFallbackMeasurementTests
{
    // The prompts' own answer rows are rendered in every voice variant, so the correctly heard rows are compared with the forced lines voice by voice.
    private static List<Case> ForcedRowsCorpus(Vocaterra.Nlp.Content.ContentBundle bundle)
    {
        var cases = new List<Case>();
        foreach (var promptId in Prompts)
        {
            var prompt = bundle.Prompts[promptId];
            foreach (var text in bundle.AnswerSets[prompt.AnswerSetId].Answers.Select(static a => a.Canonical).Distinct(StringComparer.Ordinal))
            {
                foreach (var voice in AllVoices)
                {
                    cases.Add(new Case(promptId, text, voice.Label, Kind.Row, ClipKey(text, voice.Label)));
                }
            }
        }

        // Every line at every prompt: V31's forced lines were found at the baker and the shopkeeper, and any prompt can force.
        foreach (var promptId in Prompts)
        {
            foreach (var line in Lines)
            {
                var typed = Typed(bundle, bundle.Prompts[promptId], line);
                var kind = typed.Answer.Type != Vocaterra.Nlp.Core.Stages.MatchType.None ? Kind.Row
                    : typed.Verdict.Communicated ? Kind.AllowedOutsideRows
                    : typed.Verdict.OffPromptIntent is not null ? Kind.OffPrompt
                    : Kind.NoIntent;
                foreach (var voice in AllVoices)
                {
                    cases.Add(new Case(promptId, line, voice.Label, kind, ClipKey(line, voice.Label)));
                }
            }
        }

        foreach (var promptId in Prompts)
        {
            foreach (var noise in new[] { "silence", "vowels", "noise" })
            {
                cases.Add(new Case(promptId, noise, "synthetic", Kind.Noise, ClipKey(noise, "synthetic")));
            }
        }

        return cases.DistinctBy(static c => (c.PromptId, c.ClipKey)).ToList();
    }

    [VoskAndKokoroFact]
    [Trait(TestSpeed.Trait, TestSpeed.Slow)]
    public void TheForcedRowsSignalsOfTakenConstrainedReadings()
    {
        var store = ModelGate.Store!;
        var bundle = TestSupport.Bundle;
        var cases = ForcedRowsCorpus(bundle);
        var clips = Render(store, cases);
        output.WriteLine($"{cases.Count} evaluations over {clips.Count} clips.");

        using var vosk = VoskWordRecognizer.FromStore(store, ModelGate.VoskId);
        var recognizer = new MeasuringRecognizer(vosk);
        var options = new PipelineOptions { RunRecognizersConcurrently = false, MaxAlternatives = 1 };
        var constrained = new UtterancePipeline(new PipelineServices(recognizer, null, null, null, bundle), options);
        var freeSource = new FixedRecognizer();
        var free = new UtterancePipeline(new PipelineServices(freeSource, null, null, null, bundle), options);
        var typedCache = new Dictionary<(string, string), UtteranceAnalysis>();

        var text = new StringBuilder();
        foreach (var c in cases)
        {
            var clip = clips[c.ClipKey];
            var prompt = bundle.Prompts[c.PromptId];
            if (!typedCache.TryGetValue((c.PromptId, c.Text), out var typed))
            {
                typed = Typed(bundle, prompt, c.Text);
                typedCache[(c.PromptId, c.Text)] = typed;
            }

            recognizer.StartCase();
            var first = constrained.Analyze(clip, prompt);
            if (!Taken(first.Verdict))
            {
                continue;
            }

            var open = recognizer.Recognize(clip, RecognitionRequest.Open(1));
            freeSource.Result = open;
            var second = free.Analyze(clip, prompt);
            var firstMeaning = Meaning(first.Verdict);
            var typedMeaning = c.Kind == Kind.Noise ? "-" : Meaning(typed.Verdict);
            var kindOfFirst = firstMeaning == typedMeaning && (c.Kind != Kind.Row || first.Answer.AnswerId == typed.Answer.AnswerId) ? "correct" : "forced";
            var signals = Signals.Of(clip, first.Asr);
            text.AppendLine(string.Join('|',
                "SIG", kindOfFirst, c.Kind, c.Voice, c.PromptId, c.Text.Replace('|', '/'), first.Asr.Transcript, firstMeaning, typedMeaning,
                F(signals.SpanSeconds), F(signals.VoicedSeconds), F(signals.CoverVoiced), F(signals.CoverSpan), F(signals.Lead), F(signals.Trail), F(signals.SecondsPerLetter),
                signals.Letters.ToString(CultureInfo.InvariantCulture), F(signals.MinConfidence), F(signals.MeanConfidence), signals.Unknown.ToString(CultureInfo.InvariantCulture),
                signals.Words.ToString(CultureInfo.InvariantCulture),
                Taken(second.Verdict) ? "1" : "0", F(open.Words.Count == 0 ? 0.0 : open.Words.Min(static w => w.Confidence)), F(MeanConfidence(open)), Meaning(second.Verdict), open.Transcript,
                string.Join(' ', first.Asr.Words.Select(static w => $"{w.Text}:{w.Confidence:0.00}@{w.StartSeconds:0.00}-{w.EndSeconds:0.00}")),
                first.Answer.Type.ToString(), first.Answer.Distance.ToString(CultureInfo.InvariantCulture), first.Answer.AnswerId ?? "-", F(first.Answer.Confidence),
                string.Join(' ', open.Words.Select(static w => $"{w.Text}:{w.Confidence:0.00}@{w.StartSeconds:0.00}-{w.EndSeconds:0.00}"))));
        }

        output.WriteLine(text.ToString());
    }

    private static string F(double value) => value.ToString("0.000", CultureInfo.InvariantCulture);

    /// <summary>A recognizer that answers every request with the result it was given last.</summary>
    private sealed class FixedRecognizer : IWordRecognizer
    {
        public AsrResult Result { get; set; } = TestSupport.Asr(string.Empty);

        public string ModelHash => Result.ModelHash;

        public AsrResult Recognize(PcmClip clip, RecognitionRequest request) => Result;
    }

    /// <summary>The signals of one recognition against its clip: how much of the speech the words cover, how long the speech is for the words, how sure the words are.</summary>
    private sealed record Signals(
        double SpanSeconds, double VoicedSeconds, double CoverVoiced, double CoverSpan, double Lead, double Trail, double SecondsPerLetter, int Letters,
        double MinConfidence, double MeanConfidence, int Unknown, int Words)
    {
        private const double FrameSeconds = 0.02;
        private const double ThresholdDbfs = -40.0;

        public static Signals Of(PcmClip clip, AsrResult asr)
        {
            var frame = (int)(clip.SampleRate * FrameSeconds);
            var frames = clip.Samples.Length / frame;
            var speech = new bool[frames];
            var threshold = Math.Pow(10.0, ThresholdDbfs / 10.0) * 32768.0 * 32768.0;
            for (var f = 0; f < frames; f++)
            {
                double energy = 0;
                for (var i = f * frame; i < (f + 1) * frame; i++)
                {
                    energy += (double)clip.Samples[i] * clip.Samples[i];
                }

                speech[f] = energy / frame >= threshold;
            }

            var first = Array.IndexOf(speech, true);
            var last = Array.LastIndexOf(speech, true);
            var voiced = speech.Count(static s => s);
            var known = asr.Words.Where(static w => w.Text != "[unk]").ToList();
            var span = first < 0 ? 0.0 : (last - first + 1) * FrameSeconds;
            double covered = 0;
            for (var f = 0; f < frames; f++)
            {
                var mid = (f + 0.5) * FrameSeconds;
                if (speech[f] && known.Any(w => mid >= w.StartSeconds && mid <= w.EndSeconds))
                {
                    covered++;
                }
            }

            double spanCovered = 0;
            for (var f = Math.Max(first, 0); f <= last; f++)
            {
                var mid = (f + 0.5) * FrameSeconds;
                if (known.Any(w => mid >= w.StartSeconds && mid <= w.EndSeconds))
                {
                    spanCovered++;
                }
            }

            var letters = known.Sum(static w => w.Text.Count(char.IsLetter));
            var spanStart = first < 0 ? 0.0 : first * FrameSeconds;
            var spanEnd = first < 0 ? 0.0 : (last + 1) * FrameSeconds;
            return new Signals(
                span,
                voiced * FrameSeconds,
                voiced == 0 ? 0.0 : covered / voiced,
                span == 0.0 ? 0.0 : spanCovered * FrameSeconds / span,
                known.Count == 0 ? span : Math.Max(0.0, known.Min(static w => w.StartSeconds) - spanStart),
                known.Count == 0 ? 0.0 : Math.Max(0.0, spanEnd - known.Max(static w => w.EndSeconds)),
                letters == 0 ? 0.0 : span / letters,
                letters,
                known.Count == 0 ? 0.0 : known.Min(static w => w.Confidence),
                known.Count == 0 ? 0.0 : known.Average(static w => w.Confidence),
                asr.Words.Count(static w => w.Text == "[unk]"),
                known.Count);
        }
    }
}
