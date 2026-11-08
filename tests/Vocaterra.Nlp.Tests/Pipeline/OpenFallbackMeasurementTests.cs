using System.Globalization;
using System.Text;
using Vocaterra.Nlp.Asr;
using Vocaterra.Nlp.Asr.Models;
using Vocaterra.Nlp.Asr.Tests;
using Vocaterra.Nlp.Audio;
using Vocaterra.Nlp.Bench;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Response.Speech;
using Xunit.Abstractions;

namespace Vocaterra.Nlp.Pipeline.Tests;

/// <summary>A fact that is skipped unless the Vosk small Spanish model and the Kokoro model are present and verify against the manifest.</summary>
public sealed class VoskAndKokoroFactAttribute : FactAttribute
{
    /// <summary>Checks both models once per process (the gate hashes each of them one time).</summary>
    public VoskAndKokoroFactAttribute()
    {
        foreach (var id in new[] { ModelGate.VoskId, KokoroSynthesizer.ModelDirectoryName })
        {
            if (!ModelGate.IsAvailable(id))
            {
                Skip = ModelGate.SkipReason(id);
                return;
            }
        }
    }
}

/// <summary>
/// M80, the measurement (the owner, 2026-11-06: Kokoro voices only, no recordings; zero false takes is the bar). Kokoro speaks lines over the seven constrained
/// prompts of free talk, in the Spanish voices 28, 29 and 53 at two speeds and in the Italian and Portuguese voices the NPCs use (a weak stand-in for an accent);
/// the pipeline with <see cref="PipelineOptions.OpenRecognitionFallback"/> on and off decodes them with Vosk, and the test asserts the invariants and prints
/// what the second pass reaches, what it understands, what it takes that it should not, and what it costs. The audio is rendered the way the other model-gated
/// corpora are (Kokoro behind <see cref="CachedSynthesizer"/> over a folder of WAV files under the user's cache, <c>~/.cache/vocaterra/tts</c>; nothing of it is
/// part of the repository); only the first run speaks every line. This test is M80's (the pipeline with the M84 trigger off); the other part of the class is M84's.
/// </summary>
[Collection(NativeRuntimeCollection.Name)]
public sealed partial class OpenFallbackMeasurementTests(ITestOutputHelper output)
{
    private const double ConfidenceFloor = 0.55;

    // The seven constrained prompts free talk waits on (the study, finding 1).
    private static readonly string[] Prompts = ["fisher-open", "baker-open", "smith-open", "farmer-open", "stall-need", "stall-offer", "guide-offer"];

    // Lines said at a prompt whose typed reading tells what they are: some the catalog takes off the prompt, some an allowed intent outside the rows, some no intent takes.
    private static readonly string[] Lines =
    [
        "me gusta el río", "tengo dos manzanas", "el río es grande", "hoy hace calor", "hoy llueve mucho", "¿cómo estás?", "usted hace hachas muy buenas", "dónde está la panadería",
        "mañana voy a comprar pan", "tengo un hacha", "gracias por todo", "hola buenos días", "quiero dos panes por favor", "voy al río con mi hermano",
        "la tienda está en el pueblo", "el gato duerme", "el perro come queso", "la luna brilla", "ayer llovió mucho", "mi hermano vive en Madrid", "dos más dos son cuatro", "ese libro es muy interesante",
        "mañana voy a comprar pan en la panadería del pueblo con mi hermano y con mi madre", "ayer mi hermano y yo caminamos por el río hasta la casa del herrero",
    ];

    private static readonly Voice[] AllVoices =
    [
        new("ef_dora@1.0", 28, 1.0f), new("ef_dora@0.8", 28, 0.8f),
        new("em_alex@1.0", 29, 1.0f), new("em_alex@0.8", 29, 0.8f),
        new("em_santa@1.0", 53, 1.0f), new("em_santa@0.8", 53, 0.8f),
        new("im_nicola(it)@1.0", 36, 1.0f), new("pm_santa(pt)@1.0", 44, 1.0f),
    ];

    private static readonly Voice[] RowVoices = [AllVoices[0], AllVoices[6]];

    [VoskAndKokoroFact]
    [Trait(TestSpeed.Trait, TestSpeed.Slow)]
    public void TheFreePassUnderstandsWhatItShouldTakesNothingItShouldNotAndNeverChangesATakenFirstReading()
    {
        var store = ModelGate.Store!;
        var bundle = TestSupport.Bundle;
        var cases = Corpus(bundle);
        var clips = Render(store, cases);
        output.WriteLine($"{cases.Count} evaluations over {clips.Count} clips.");

        using var vosk = VoskWordRecognizer.FromStore(store, ModelGate.VoskId);
        var recognizer = new MeasuringRecognizer(vosk);
        var options = new PipelineOptions { RunRecognizersConcurrently = false, MaxAlternatives = 1 };
        var services = new PipelineServices(recognizer, null, null, null, bundle);
        var off = new UtterancePipeline(services, options);
        // The shipped floor (0.5 on the weakest word of the free pass), and the same pipeline with the floor switched off, to report what the floor removes.
        var on = new UtterancePipeline(services, options with { OpenRecognitionFallback = true, OpenPassOnLooseMatch = false, OpenPassMinCoverage = 0.0 });
        var unfloored = new UtterancePipeline(services, options with { OpenRecognitionFallback = true, OpenPassWordFloor = 0.0, OpenPassOnLooseMatch = false, OpenPassMinCoverage = 0.0 });

        var results = new List<Measured>(cases.Count);
        var withoutFloor = new List<Measured>(cases.Count);
        foreach (var c in cases)
        {
            var clip = clips[c.ClipKey];
            var prompt = bundle.Prompts[c.PromptId];
            var typed = Typed(bundle, prompt, c.Text);
            recognizer.StartCase();
            var without = off.Analyze(clip, prompt);
            var offRequests = recognizer.CaseRequests.Count;
            var with = on.Analyze(clip, prompt);
            results.Add(Measure(c, clip, typed, without, with, recognizer.CaseRequests));

            // The invariants, with the models.
            var firstTaken = Taken(without.Verdict);
            Assert.Equal(1, offRequests);
            Assert.Equal(firstTaken ? 2 : 3, recognizer.CaseRequests.Count);
            Assert.All(recognizer.CaseRequests.Take(2), static r => Assert.Equal(RecognitionMode.Constrained, r.Mode));
            if (!firstTaken)
            {
                Assert.Equal(RecognitionMode.Open, recognizer.CaseRequests[2].Mode);
            }

            if (firstTaken)
            {
                Assert.Equal(CanonicalJson.Serialize(without.Verdict), CanonicalJson.Serialize(with.Verdict));
                Assert.Equal(without.Asr, with.Asr);
                Assert.Null(with.OtherPass);
            }
            else
            {
                Assert.NotNull(with.OtherPass);
                Assert.True(with.Asr.Mode == RecognitionMode.Constrained || Taken(with.Verdict), $"{c}: a free reading was chosen without being taken");
                if (with.Asr.Mode == RecognitionMode.Constrained)
                {
                    Assert.Equal(CanonicalJson.Serialize(without.Verdict), CanonicalJson.Serialize(with.Verdict));
                }
            }

            // The floor only ever withholds a free reading: the same pipeline without it chooses the free reading wherever this one does, with the same verdict.
            var loose = unfloored.Analyze(clip, prompt);
            withoutFloor.Add(Measure(c, clip, typed, without, loose, recognizer.CaseRequests));
            if (with.Asr.Mode == RecognitionMode.Open)
            {
                Assert.Equal(CanonicalJson.Serialize(loose.Verdict), CanonicalJson.Serialize(with.Verdict));
                Assert.True(with.Asr.Words.Count > 0 && with.Asr.Words.Min(static w => w.Confidence) >= PipelineOptions.DefaultOpenPassWordFloor, $"{c}: a free reading was chosen with a word under the floor");
            }

            if (c.Kind == Kind.Row)
            {
                Assert.True(!firstTaken || CanonicalJson.Serialize(without.Verdict) == CanonicalJson.Serialize(with.Verdict), $"{c}: an answer row changed its verdict");
            }
        }

        Report(results, withoutFloor, recognizer);

        foreach (var kind in new[] { Kind.Row, Kind.OffPrompt, Kind.AllowedOutsideRows, Kind.NoIntent })
        {
            Assert.True(results.Count(r => r.Case.Kind == kind) >= 10, $"The corpus has too few {kind} evaluations.");
        }

        var slowOpen = recognizer.Decodes.Where(static d => d.Mode == RecognitionMode.Open && d.Seconds <= 4.0).Select(static d => d.Milliseconds).ToArray();
        Assert.True(BenchMetrics.Percentile(slowOpen, 0.95) < 1000.0, "The open decode of a clip up to 4 s takes under a second at the 95th percentile (an estimated target).");

        var falseTakes = results.Where(static r => r.FalseTake).ToList();
        Assert.True(falseTakes.Count == 0, "False takes (a line no intent should take, taken at " + ConfidenceFloor.ToString("0.00", CultureInfo.InvariantCulture)
            + " or more on the free pass):\n" + string.Join('\n', falseTakes.Select(static r => r.Describe())));
    }

    // ---- The corpus ----------------------------------------------------------------------------------------------------------

    private enum Kind
    {
        Row,
        OffPrompt,
        AllowedOutsideRows,
        NoIntent,
        Noise,
    }

    private sealed record Voice(string Label, int Speaker, float Speed);

    // What is said: a text and a voice (or a synthetic signal), and what it is said to.
    private sealed record Case(string PromptId, string Text, string Voice, Kind Kind, string ClipKey)
    {
        public override string ToString() => $"[{PromptId} | {Voice} | '{Text}' | {Kind}]";
    }

    private static List<Case> Corpus(Vocaterra.Nlp.Content.ContentBundle bundle)
    {
        var cases = new List<Case>();
        foreach (var promptId in Prompts)
        {
            var prompt = bundle.Prompts[promptId];
            Assert.Equal(RecognitionMode.Constrained, prompt.Mode);
            foreach (var text in bundle.AnswerSets[prompt.AnswerSetId].Answers.Select(static a => a.Canonical).Distinct(StringComparer.Ordinal))
            {
                foreach (var voice in RowVoices)
                {
                    cases.Add(new Case(promptId, text, voice.Label, Kind.Row, ClipKey(text, voice.Label)));
                }
            }
        }

        // Every line goes to two prompts (rotating, so each prompt gets some of each kind); the typed reading at that prompt names its kind.
        for (var i = 0; i < Lines.Length; i++)
        {
            foreach (var offset in new[] { 0, 3 })
            {
                var promptId = Prompts[(i + offset) % Prompts.Length];
                var typed = Typed(bundle, bundle.Prompts[promptId], Lines[i]);
                var kind = typed.Answer.Type != Vocaterra.Nlp.Core.Stages.MatchType.None ? Kind.Row
                    : typed.Verdict.Communicated ? Kind.AllowedOutsideRows
                    : typed.Verdict.OffPromptIntent is not null ? Kind.OffPrompt
                    : Kind.NoIntent;
                foreach (var voice in AllVoices)
                {
                    cases.Add(new Case(promptId, Lines[i], voice.Label, kind, ClipKey(Lines[i], voice.Label)));
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

        return cases;
    }

    private static string ClipKey(string text, string voice) => voice + "|" + text;

    // The typed reading of a line at a prompt: the free pass reads a spoken line the way the typed line is read.
    private static UtteranceAnalysis Typed(Vocaterra.Nlp.Content.ContentBundle bundle, PromptSpec prompt, string text)
    {
        var services = new PipelineServices(new ScriptedWordRecognizer(TestSupport.Asr(text, RecognitionMode.Open)), null, null, null, bundle);
        return new UtterancePipeline(services).Analyze(TestSupport.Clip(), prompt);
    }

    // Renders every clip once. Kokoro loads an engine for every line, so a cache of WAV files keeps a rerun fast, and one synthesizer per voice runs on its own thread.
    private static Dictionary<string, PcmClip> Render(ModelStore store, List<Case> cases)
    {
        var clips = new Dictionary<string, PcmClip>(StringComparer.Ordinal);
        var cache = new DirectorySynthesisCache(CacheDirectory());
        var spoken = cases.Where(static c => c.Kind != Kind.Noise).DistinctBy(static c => c.ClipKey).ToList();
        var byVoice = spoken.GroupBy(static c => c.Voice).ToList();
        var rendered = new System.Collections.Concurrent.ConcurrentDictionary<string, PcmClip>(StringComparer.Ordinal);
        Parallel.ForEach(byVoice, new ParallelOptions { MaxDegreeOfParallelism = 4 }, group =>
        {
            var voice = AllVoices.Single(v => v.Label == group.Key);
            using var kokoro = KokoroSynthesizer.FromStore(store, KokoroSynthesizer.ModelDirectoryName, voice.Speaker, voice.Speed);
            var cached = new CachedSynthesizer(kokoro, cache);
            foreach (var c in group)
            {
                var audio = cached.Synthesize(new SynthesisRequest(c.Text, voice.Label, null));
                rendered[c.ClipKey] = PcmClip.FromSamples(Resampler.To16k(audio.Samples, audio.SampleRate));
            }
        });
        foreach (var (key, clip) in rendered)
        {
            clips[key] = clip;
        }

        clips[ClipKey("silence", "synthetic")] = TestData.SilenceClip(2.0);
        clips[ClipKey("vowels", "synthetic")] = TestData.VowelClip(2.0);
        clips[ClipKey("noise", "synthetic")] = Noise(2.0);
        return clips;
    }

    private static string CacheDirectory()
    {
        var root = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
        if (string.IsNullOrWhiteSpace(root))
        {
            root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache");
        }

        return Path.Combine(root, "vocaterra", "tts");
    }

    // Broadband noise from a fixed linear congruential generator, so the same clip every run.
    private static PcmClip Noise(double seconds)
    {
        var samples = new short[(int)(seconds * PcmClip.DefaultSampleRate)];
        uint state = 12345;
        for (var i = 0; i < samples.Length; i++)
        {
            state = unchecked((state * 1664525u) + 1013904223u);
            samples[i] = (short)(((int)(state >> 16) % 6000) - 3000);
        }

        return PcmClip.FromSamples(samples);
    }

    // ---- The figures ---------------------------------------------------------------------------------------------------------

    private static bool Taken(Verdict verdict) => verdict.Communicated || verdict.OffPromptIntent is not null;

    // The mean of Vosk's word confidences, leaving out [unk], as the game's UncertaintyRule reads it (zero for no word).
    private static double MeanConfidence(AsrResult asr)
    {
        var words = asr.Words.Where(static w => !string.Equals(w.Text, "[unk]", StringComparison.Ordinal)).ToList();
        return words.Count == 0 ? 0.0 : words.Average(static w => w.Confidence);
    }

    // The weakest word of a recognition, [unk] left out (zero for no word).
    private static double MinConfidence(AsrResult asr)
    {
        var words = asr.Words.Where(static w => !string.Equals(w.Text, "[unk]", StringComparison.Ordinal)).ToList();
        return words.Count == 0 ? 0.0 : words.Min(static w => w.Confidence);
    }

    private static string Meaning(Verdict verdict) => verdict.Communicated ? verdict.Intent.Value : verdict.OffPromptIntent?.Value ?? "-";

    private sealed record Measured(
        Case Case,
        double Seconds,
        bool FirstTaken,
        double FirstMean,
        bool SecondRan,
        bool ChosenOpen,
        double FinalMean,
        string FinalMeaning,
        string TypedMeaning,
        bool FinalTaken,
        string Heard,
        string FirstHeard,
        string Words,
        double FinalMin)
    {
        // A line no intent should take (typed, nothing takes it; or noise) that the free pass made taken at the floor or above.
        public bool FalseTake => ChosenOpen && FinalTaken && FinalMean >= ConfidenceFloor && (Case.Kind is Kind.NoIntent or Kind.Noise);

        // A line something takes, taken by the free pass as something else.
        public bool WrongTake => ChosenOpen && FinalTaken && FinalMean >= ConfidenceFloor && Case.Kind is not (Kind.NoIntent or Kind.Noise) && FinalMeaning != TypedMeaning;

        public bool Understood => ChosenOpen && FinalTaken && FinalMean >= ConfidenceFloor && FinalMeaning == TypedMeaning;

        public string Describe() => $"{Case} first '{FirstHeard}' ({(FirstTaken ? "taken" : "not taken")}, mean {FirstMean:0.00}); free pass heard '{Heard}' [{Words}] mean {FinalMean:0.00} -> {FinalMeaning} (typed: {TypedMeaning}); {Seconds:0.0} s";
    }

    private static Measured Measure(Case c, PcmClip clip, UtteranceAnalysis typed, UtteranceAnalysis without, UtteranceAnalysis with, IReadOnlyList<RecognitionRequest> requests)
    {
        var chosenOpen = with.Asr.Mode == RecognitionMode.Open;
        var heard = chosenOpen ? with.Asr.Transcript : with.OtherPass?.Transcript ?? string.Empty;
        return new Measured(
            c,
            clip.Samples.Length / (double)clip.SampleRate,
            Taken(without.Verdict),
            MeanConfidence(without.Asr),
            requests.Count > 2,
            chosenOpen,
            MeanConfidence(with.Asr),
            Meaning(with.Verdict),
            c.Kind == Kind.Noise ? "-" : Meaning(typed.Verdict),
            Taken(with.Verdict),
            heard,
            without.Asr.Transcript,
            string.Join(' ', (chosenOpen ? with.Asr : with.OtherPass ?? with.Asr).Words.Select(static w => $"{w.Text}:{w.Confidence:0.00}")),
            MinConfidence(with.Asr));
    }

    private void Report(List<Measured> results, List<Measured> withoutFloor, MeasuringRecognizer recognizer)
    {
        var text = new StringBuilder();
        text.AppendLine("The open-recognition fallback, Kokoro voices only (accent risk on real speech: unmeasured).");
        text.AppendLine("kind | n | first not taken | free pass chosen | understood (right meaning, mean >= 0.55) | wrong take | false take (with the floor)");
        foreach (var kind in Enum.GetValues<Kind>())
        {
            var set = results.Where(r => r.Case.Kind == kind).ToList();
            text.AppendLine($"{kind} | {set.Count} | {set.Count(static r => !r.FirstTaken)} | {set.Count(static r => r.ChosenOpen)} | {set.Count(static r => r.Understood)} | {set.Count(static r => r.WrongTake)} | {set.Count(static r => r.FalseTake)}");
        }

        var off = results.Where(static r => r.Case.Kind == Kind.OffPrompt).ToList();
        text.AppendLine($"Off-prompt lines: trigger reach (first pass not taken) {Share(off.Count(static r => !r.FirstTaken), off.Count)}; understood after the free pass {Share(off.Count(static r => r.Understood), off.Count)} of all, {Share(off.Count(static r => r.Understood), off.Count(static r => !r.FirstTaken))} of those reached.");
        var allowed = results.Where(static r => r.Case.Kind == Kind.AllowedOutsideRows).ToList();
        text.AppendLine($"Allowed-outside-rows lines: first pass not taken {Share(allowed.Count(static r => !r.FirstTaken), allowed.Count)}; understood after the free pass {Share(allowed.Count(static r => r.Understood), allowed.Count)} of all.");
        // For information: what other floors on the weakest word of a free reading would do, from the run without the shipped floor (the mean of 0.55 is the game's UncertaintyRule).
        var free = withoutFloor.Where(static r => r.ChosenOpen && r.FinalTaken && r.FinalMean >= ConfidenceFloor).ToList();
        text.AppendLine("A floor on the weakest word of a free reading that is taken at a mean of 0.55 or more (the run with no floor): floor | understood kept | wrong takes kept | false takes kept");
        foreach (var floor in new[] { 0.0, 0.3, 0.4, 0.5, 0.6, 0.7 })
        {
            text.AppendLine($"{floor:0.0} | {free.Count(r => r.Understood && r.FinalMin >= floor)}/{free.Count(static r => r.Understood)} | {free.Count(r => r.WrongTake && r.FinalMin >= floor)}/{free.Count(static r => r.WrongTake)} | {free.Count(r => r.FalseTake && r.FinalMin >= floor)}/{free.Count(static r => r.FalseTake)}");
        }

        // What the shipped floor (0.5) removed, from the run without it: understood lines lost, wrong and false takes stopped.
        var lost = withoutFloor.Zip(results).Where(static p => p.First.Understood && !p.Second.Understood).Select(static p => p.First).ToList();
        var wrongStopped = withoutFloor.Zip(results).Where(static p => p.First.WrongTake && !p.Second.WrongTake).Select(static p => p.First).ToList();
        var falseStopped = withoutFloor.Zip(results).Where(static p => p.First.FalseTake && !p.Second.FalseTake).Select(static p => p.First).ToList();
        text.AppendLine($"The floor {PipelineOptions.DefaultOpenPassWordFloor:0.0} removed: understood lines lost {lost.Count} of {withoutFloor.Count(static r => r.Understood)}; wrong takes stopped {wrongStopped.Count} of {withoutFloor.Count(static r => r.WrongTake)}; false takes stopped {falseStopped.Count} of {withoutFloor.Count(static r => r.FalseTake)}.");
        text.AppendLine("by voice: voice | understood lost | wrong takes stopped | false takes stopped");
        foreach (var group in withoutFloor.Where(static r => r.Case.Kind != Kind.Noise).GroupBy(static r => r.Case.Voice).OrderBy(static g => g.Key, StringComparer.Ordinal))
        {
            text.AppendLine($"{group.Key} | {lost.Count(r => r.Case.Voice == group.Key)} | {wrongStopped.Count(r => r.Case.Voice == group.Key)} | {falseStopped.Count(r => r.Case.Voice == group.Key)}");
        }

        foreach (var r in lost)
        {
            text.AppendLine("understood line lost to the floor: " + r.Describe());
        }

        foreach (var r in wrongStopped)
        {
            text.AppendLine("wrong take stopped by the floor: " + r.Describe());
        }

        foreach (var r in falseStopped)
        {
            text.AppendLine("false take stopped by the floor: " + r.Describe());
        }

        var forcedWeak = results.Count(static r => r.FirstTaken && r.FirstMean < ConfidenceFloor && r.Case.Kind != Kind.Noise);
        text.AppendLine($"Forced but weak (first reading taken with a mean under {ConfidenceFloor:0.00}; a variant would send these to a second pass too): {forcedWeak} of {results.Count(static r => r.FirstTaken)} taken first readings.");

        text.AppendLine("by voice: voice | n | first not taken | second pass ran | understood (of lines something takes and the first pass left) | wrong takes | false takes | forced but weak");
        foreach (var group in results.Where(static r => r.Case.Kind != Kind.Noise).GroupBy(static r => r.Case.Voice).OrderBy(static g => g.Key, StringComparer.Ordinal))
        {
            var left = group.Where(static r => !r.FirstTaken && r.Case.Kind is Kind.OffPrompt or Kind.AllowedOutsideRows).ToList();
            text.AppendLine($"{group.Key} | {group.Count()} | {group.Count(static r => !r.FirstTaken)} | {group.Count(static r => r.SecondRan)} | {left.Count(static r => r.Understood)}/{left.Count} | {group.Count(static r => r.WrongTake)} | {group.Count(static r => r.FalseTake)} | {group.Count(static r => r.FirstTaken && r.FirstMean < ConfidenceFloor)}");
        }

        text.AppendLine("recognition time per pass (ms, the Vosk small model, one best hypothesis; a decode is timed once even when several prompts reuse the free decode): pass | clip length | n | median | p95");
        foreach (var mode in new[] { RecognitionMode.Constrained, RecognitionMode.Open })
        {
            foreach (var (label, low, high) in new[] { ("under 1.5 s", 0.0, 1.5), ("1.5 to 3 s", 1.5, 3.0), ("3 to 4 s", 3.0, 4.0), ("4 s or more", 4.0, double.MaxValue), ("all", 0.0, double.MaxValue) })
            {
                var times = recognizer.Decodes.Where(d => d.Mode == mode && d.Seconds >= low && d.Seconds < high).Select(static d => d.Milliseconds).ToArray();
                text.AppendLine($"{mode} | {label} | {times.Length} | {BenchMetrics.Percentile(times, 0.50):0} | {BenchMetrics.Percentile(times, 0.95):0}");
            }
        }

        text.AppendLine($"Second pass ran for {results.Count(static r => r.SecondRan)} of {results.Count} evaluations; the free reading was chosen for {results.Count(static r => r.ChosenOpen)}.");
        foreach (var r in results.Where(static r => r.Case.Kind == Kind.Row && (!r.FirstTaken || r.ChosenOpen)))
        {
            text.AppendLine("answer row left by the first pass: " + r.Describe());
        }

        foreach (var r in results.Where(static r => r.FalseTake || r.WrongTake))
        {
            text.AppendLine((r.FalseTake ? "FALSE TAKE: " : "wrong take: ") + r.Describe());
        }

        output.WriteLine(text.ToString());
    }

    private static string Share(int part, int whole) => whole == 0 ? "n/a" : $"{part}/{whole} ({100.0 * part / whole:0}%)";

    // ---- The recognizer under measurement ------------------------------------------------------------------------------------

    /// <summary>
    /// Vosk behind a memo, so the pipelines with the option on and off share their decodes of one clip (and one prompt's grammar) and every live decode is timed
    /// once, by the Bench tool's clock.
    /// </summary>
    private sealed class MeasuringRecognizer(VoskWordRecognizer inner) : IWordRecognizer
    {
        private readonly Dictionary<string, AsrResult> _memo = new(StringComparer.Ordinal);
        private readonly List<RecognitionRequest> _caseRequests = [];

        public string ModelHash => inner.ModelHash;

        public List<(RecognitionMode Mode, double Seconds, double Milliseconds)> Decodes { get; } = [];

        // The time of the free decode of each clip (by hash), as measured the one time it was decoded.
        public Dictionary<string, double> OpenMilliseconds { get; } = new(StringComparer.Ordinal);

        public IReadOnlyList<RecognitionRequest> CaseRequests => _caseRequests;

        public void StartCase()
        {
            _caseRequests.Clear();
        }

        public AsrResult Recognize(PcmClip clip, RecognitionRequest request)
        {
            _caseRequests.Add(request);

            var key = clip.ClipHash + "|" + request.Mode + "|" + DeterministicHash.OfText(string.Join('\n', request.GrammarPhrases)) + "|" + request.MaxAlternatives;
            if (_memo.TryGetValue(key, out var known))
            {
                return known;
            }

            var (result, milliseconds) = BenchRunner.TimedRecognize(inner, clip, request);
            Decodes.Add((request.Mode, clip.Samples.Length / (double)clip.SampleRate, milliseconds));
            if (request.Mode == RecognitionMode.Open)
            {
                OpenMilliseconds[clip.ClipHash] = milliseconds;
            }

            _memo[key] = result;
            return result;
        }
    }
}
