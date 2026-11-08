using System.Globalization;
using System.Text;
using Vocaterra.Nlp.Asr;
using Vocaterra.Nlp.Asr.Tests;
using Vocaterra.Nlp.Audio;
using Vocaterra.Nlp.Bench;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using MatchType = Vocaterra.Nlp.Core.Stages.MatchType;

namespace Vocaterra.Nlp.Pipeline.Tests;

/// <summary>
/// M84, the forced rows: the constrained decode can force a different line onto an answer row with confidence, so the reading is taken and the open decode of M80
/// never runs (V31, Kokoro <c>ef_dora</c>: «el río es grande» heard «es gracias», read as <c>thank</c>; «me gusta el río» heard «me cuesta el río», read as
/// <c>ask_price</c>). This part of the measurement says every line at every one of the seven free-talk prompts, and every answer row of those prompts, in every voice
/// variant of M80, decodes each clip with the pipeline's defaults (the fallback on, the M84 trigger on) and without the fallback, and reports what a trigger could see
/// (the share of the voiced audio the words cover, the speech per letter, the weakest and mean word confidence, the dropped <c>[unk]</c> tokens, the answer match) for
/// the correctly heard rows against the forced lines, and what the shipped trigger does. The bars are asserted: no correct answer row is replaced and no false take
/// (M80's oracle: a line no intent should take that the free reading makes taken at a mean of 0.55 or more).
/// </summary>
public sealed partial class OpenFallbackMeasurementTests
{
    // Lines of the same kind as V31's two, found by saying them: sentences about the weather, the house, the music.
    private static readonly string[] MoreLines = ["el pan es muy bueno", "el cielo es azul", "la casa es grande", "me gusta la música", "mi madre cocina bien", "hoy hace sol"];

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
            foreach (var line in Lines.Concat(MoreLines))
            {
                var typed = Typed(bundle, bundle.Prompts[promptId], line);
                var kind = typed.Answer.Type != MatchType.None ? Kind.Row
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

    // What one evaluation shows: the constrained reading (the option off), the pipeline with the defaults, the signals of the constrained recognition.
    private sealed record Forced(
        Case Case,
        bool FirstTaken,
        bool FirstCorrect,
        string FirstMeaning,
        string TypedMeaning,
        string FinalMeaning,
        bool FinalTaken,
        double FinalMean,
        bool Fired,
        bool ChosenOpen,
        double OpenMilliseconds,
        double? Coverage,
        double SecondsPerLetter,
        double MinConfidence,
        double MeanConfidence,
        int Unknown,
        MatchType Match,
        string FirstHeard,
        string FinalHeard,
        string Words)
    {
        public bool CorrectRow => Case.Kind == Kind.Row && FirstTaken && FirstCorrect;

        public bool OtherCorrect => Case.Kind != Kind.Row && FirstTaken && FirstCorrect;

        // A taken constrained reading that is not what the typed line gives: the forced lines.
        public bool ForcedLine => FirstTaken && !FirstCorrect;

        public bool Replaced => FirstTaken && ChosenOpen;

        // M80's oracle: a line no intent should take (typed, nothing takes it; or noise) that the free reading made taken at a mean of 0.55 or more.
        public bool FalseTake => ChosenOpen && FinalTaken && FinalMean >= ConfidenceFloor && (Case.Kind is Kind.NoIntent or Kind.Noise);

        public bool Rescued => Replaced && ForcedLine && FinalMeaning == TypedMeaning;

        public bool WrongTake => Replaced && FinalMeaning != TypedMeaning && Case.Kind is not (Kind.NoIntent or Kind.Noise);

        public string Describe() => $"{Case} first '{FirstHeard}' -> {FirstMeaning} (typed: {TypedMeaning}, {Match}, coverage {Coverage?.ToString("0.00", CultureInfo.InvariantCulture) ?? "n/a"}); final '{FinalHeard}' -> {FinalMeaning} [{Words}]";
    }

    [VoskAndKokoroFact]
    [Trait(TestSpeed.Trait, TestSpeed.Slow)]
    public void TheForcedRowTriggerReplacesNoCorrectRowTakesNothingItShouldNotAndRescuesForcedLines()
    {
        var store = ModelGate.Store!;
        var bundle = TestSupport.Bundle;
        var cases = ForcedRowsCorpus(bundle);
        var clips = Render(store, cases);
        output.WriteLine($"{cases.Count} evaluations over {clips.Count} clips.");

        using var vosk = VoskWordRecognizer.FromStore(store, ModelGate.VoskId);
        var recognizer = new MeasuringRecognizer(vosk);
        var options = new PipelineOptions { RunRecognizersConcurrently = false, MaxAlternatives = 1 };
        var services = new PipelineServices(recognizer, null, null, null, bundle);
        var off = new UtterancePipeline(services, options);
        var on = new UtterancePipeline(services, options with { OpenRecognitionFallback = true });
        var typedCache = new Dictionary<(string, string), UtteranceAnalysis>();

        var results = new List<Forced>(cases.Count);
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
            var without = off.Analyze(clip, prompt);
            var with = on.Analyze(clip, prompt);
            var firstTaken = Taken(without.Verdict);
            var fired = recognizer.CaseRequests.Count == 3;
            var typedMeaning = c.Kind == Kind.Noise ? "-" : Meaning(typed.Verdict);
            var firstMeaning = Meaning(without.Verdict);
            var firstCorrect = firstTaken && firstMeaning == typedMeaning && (c.Kind != Kind.Row || without.Answer.AnswerId == typed.Answer.AnswerId);

            // The invariants, with the models: the fallback decodes a clip at most twice; an untaken reading always twice, a taken one twice only when the trigger fires,
            // and then the other recognition is reported; a trusted reading is the option-off result; a free reading is chosen only when taken, over the floor, and (replacing
            // a taken reading) of another intent.
            Assert.Equal(fired ? 3 : 2, recognizer.CaseRequests.Count);
            Assert.True(firstTaken ? fired == (with.OtherPass is not null) : fired, $"{c}: the other pass is not what the decodes say");
            Assert.True(!firstTaken || fired || CanonicalJson.Serialize(without.Verdict) == CanonicalJson.Serialize(with.Verdict), $"{c}: a trusted reading changed its verdict");
            if (with.Asr.Mode == RecognitionMode.Open)
            {
                Assert.True(Taken(with.Verdict) && with.Asr.Words.Count > 0 && with.Asr.Words.Min(static w => w.Confidence) >= PipelineOptions.DefaultOpenPassWordFloor, $"{c}: a free reading was chosen without being taken or with a word under the floor");
                Assert.True(!firstTaken || Meaning(with.Verdict) != firstMeaning, $"{c}: a free reading replaced a taken reading of the same intent");
            }

            var chosenOpen = with.Asr.Mode == RecognitionMode.Open;
            results.Add(new Forced(
                c,
                firstTaken,
                firstCorrect,
                firstMeaning,
                typedMeaning,
                Meaning(with.Verdict),
                Taken(with.Verdict),
                MeanConfidence(with.Asr),
                fired,
                chosenOpen,
                fired && recognizer.OpenMilliseconds.TryGetValue(clip.ClipHash, out var ms) ? ms : 0.0,
                ForcedReading.Coverage(clip, without.Asr),
                SpeechSecondsPerLetter(clip, without.Asr),
                MinConfidence(without.Asr),
                MeanConfidence(without.Asr),
                without.Asr.Words.Count(static w => w.Text == "[unk]"),
                without.Answer.Type,
                without.Asr.Transcript,
                chosenOpen ? with.Asr.Transcript : with.OtherPass?.Transcript ?? string.Empty,
                string.Join(' ', without.Asr.Words.Select(static w => $"{w.Text}:{w.Confidence:0.00}@{w.StartSeconds:0.00}-{w.EndSeconds:0.00}"))));
        }

        ReportForced(results, recognizer);

        // The corpus says something about each class.
        Assert.True(results.Count(static r => r.CorrectRow) >= 500, "The corpus has too few correctly heard answer rows.");
        Assert.True(results.Count(static r => r.ForcedLine) >= 100, "The corpus has too few forced lines.");

        // The bars: no correct row replaced (nor any other line the constrained decode read as typed), no false take; and the trigger is worth its decodes.
        var replacedRows = results.Where(static r => r.CorrectRow && r.Replaced).ToList();
        Assert.True(replacedRows.Count == 0, "Correct answer rows replaced by the trigger:\n" + string.Join('\n', replacedRows.Select(static r => r.Describe())));
        var replacedOther = results.Where(static r => r.OtherCorrect && r.Replaced).ToList();
        Assert.True(replacedOther.Count == 0, "Correctly read lines replaced by the trigger:\n" + string.Join('\n', replacedOther.Select(static r => r.Describe())));
        var falseTakes = results.Where(static r => r.FalseTake).ToList();
        Assert.True(falseTakes.Count == 0, "False takes (a line no intent should take, taken at a mean of 0.55 or more):\n" + string.Join('\n', falseTakes.Select(static r => r.Describe())));
        Assert.True(results.Count(static r => r.Rescued) >= 100, "The trigger rescued too few forced lines to be worth its decodes.");
    }

    // The seconds of speech (the voiced stretch, without the hangover) per letter of the words heard, [unk] left out; 0 for no letter or no speech.
    private static double SpeechSecondsPerLetter(PcmClip clip, AsrResult asr)
    {
        var vad = new EnergyVad(sampleRate: clip.SampleRate);
        var trimmed = vad.Trim(clip.Samples);
        var letters = asr.Words.Where(static w => w.Text != "[unk]").Sum(static w => w.Text.Count(char.IsLetter));
        return !trimmed.HasSpeech || letters == 0 ? 0.0 : (trimmed.SpeechEndFrame - trimmed.SpeechStartFrame - vad.HangoverFrames) * vad.FrameMs / 1000.0 / letters;
    }

    private static string Summary(IEnumerable<double> values)
    {
        var xs = values.Order().ToArray();
        return xs.Length == 0
            ? "n=0"
            : string.Create(CultureInfo.InvariantCulture, $"n={xs.Length} min {xs[0]:0.00} p10 {BenchMetrics.Percentile(xs, 0.1):0.00} median {BenchMetrics.Percentile(xs, 0.5):0.00} p90 {BenchMetrics.Percentile(xs, 0.9):0.00} max {xs[^1]:0.00}");
    }

    private static double Median(IEnumerable<double> values)
    {
        var xs = values.ToArray();
        return xs.Length == 0 ? 0.0 : BenchMetrics.Percentile(xs, 0.5);
    }

    private void ReportForced(List<Forced> results, MeasuringRecognizer recognizer)
    {
        var text = new StringBuilder();
        var taken = results.Where(static r => r.FirstTaken).ToList();
        var classes = new[]
        {
            ("correct row", taken.Where(static r => r.CorrectRow).ToList()),
            ("other read as typed", taken.Where(static r => r.OtherCorrect).ToList()),
            ("forced line", taken.Where(static r => r.ForcedLine).ToList()),
        };
        text.AppendLine("M84, free talk's forced rows, Kokoro voices only (accent risk on real speech: unmeasured).");
        text.AppendLine($"{results.Count} evaluations; {taken.Count} with a taken constrained reading: {classes[0].Item2.Count} correctly heard answer rows, {classes[1].Item2.Count} other lines read as typed, {classes[2].Item2.Count} forced lines (read as something the typed line is not).");

        text.AppendLine("signals of the constrained recognition, by class (the trigger reads the answer match and the coverage; the rest are for comparison): class | signal | distribution");
        foreach (var (label, set) in classes)
        {
            text.AppendLine($"{label} | words' coverage of the voiced audio | {Summary(set.Where(static r => r.Coverage is not null).Select(static r => r.Coverage!.Value))}");
            text.AppendLine($"{label} | speech seconds per letter | {Summary(set.Where(static r => r.SecondsPerLetter > 0).Select(static r => r.SecondsPerLetter))}");
            text.AppendLine($"{label} | weakest word confidence | {Summary(set.Select(static r => r.MinConfidence))}");
            text.AppendLine($"{label} | mean word confidence | {Summary(set.Select(static r => r.MeanConfidence))}");
            text.AppendLine($"{label} | [unk] tokens dropped | {Share(set.Count(static r => r.Unknown > 0), set.Count)} of the clips have one");
            text.AppendLine($"{label} | answer match | {string.Join(", ", Enum.GetValues<MatchType>().Select(m => $"{m} {set.Count(r => r.Match == m)}"))}");
        }

        text.AppendLine("by voice: voice | correct rows: n, coverage median, coverage min, weakest word median | forced lines: n, coverage median, weakest word median | trigger fired on correct rows | forced lines rescued");
        foreach (var group in taken.Where(static r => r.Case.Kind != Kind.Noise).GroupBy(static r => r.Case.Voice).OrderBy(static g => g.Key, StringComparer.Ordinal))
        {
            var rows = group.Where(static r => r.CorrectRow).ToList();
            var forced = group.Where(static r => r.ForcedLine).ToList();
            var rowCoverage = rows.Where(static r => r.Coverage is not null).Select(static r => r.Coverage!.Value).ToList();
            var forcedCoverage = forced.Where(static r => r.Coverage is not null).Select(static r => r.Coverage!.Value).ToList();
            text.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{group.Key} | {rows.Count}, {Median(rowCoverage):0.00}, {rowCoverage.DefaultIfEmpty(0).Min():0.00}, {Median(rows.Select(static r => r.MinConfidence)):0.00} | {forced.Count}, {Median(forcedCoverage):0.00}, {Median(forced.Select(static r => r.MinConfidence)):0.00} | {rows.Count(static r => r.Fired)}/{rows.Count} | {forced.Count(static r => r.Rescued)}/{forced.Count}"));
        }

        text.AppendLine("what the shipped trigger does: class | taken readings | free decode ran | free reading replaced the first | rescued (final meaning as typed) | wrong takes");
        foreach (var (label, set) in classes.Append(("all taken", taken)))
        {
            text.AppendLine($"{label} | {set.Count} | {set.Count(static r => r.Fired)} ({Share(set.Count(static r => r.Fired), set.Count)}) | {set.Count(static r => r.Replaced)} | {set.Count(static r => r.Rescued)} | {set.Count(static r => r.WrongTake)}");
        }

        var forcedLines = classes[2].Item2;
        text.AppendLine($"forced lines: {forcedLines.Count}; fired {forcedLines.Count(static r => r.Fired)}; rescued {forcedLines.Count(static r => r.Rescued)}; replaced by another wrong reading {forcedLines.Count(static r => r.WrongTake)}; fired but kept (the free reading not taken, under the floor or of the same intent) {forcedLines.Count(static r => r.Fired && !r.Replaced)}; not fired {forcedLines.Count(static r => !r.Fired)}; false takes in the whole corpus {results.Count(static r => r.FalseTake)}.");
        text.AppendLine($"correct answer rows replaced: {classes[0].Item2.Count(static r => r.Replaced)} of {classes[0].Item2.Count}; other correctly read lines replaced: {classes[1].Item2.Count(static r => r.Replaced)} of {classes[1].Item2.Count}.");

        var extra = taken.Where(static r => r.Fired && r.OpenMilliseconds > 0).Select(static r => r.OpenMilliseconds).ToArray();
        var extraOnRows = classes[0].Item2.Where(static r => r.Fired && r.OpenMilliseconds > 0).Select(static r => r.OpenMilliseconds).ToArray();
        text.AppendLine($"extra free decodes on taken readings: {taken.Count(static r => r.Fired)} of {taken.Count}; time of the extra decode (ms): median {BenchMetrics.Percentile(extra, 0.5):0}, p95 {BenchMetrics.Percentile(extra, 0.95):0}; on correct rows: {classes[0].Item2.Count(static r => r.Fired)} of {classes[0].Item2.Count}, median {BenchMetrics.Percentile(extraOnRows, 0.5):0}, p95 {BenchMetrics.Percentile(extraOnRows, 0.95):0}.");
        var constrained = recognizer.Decodes.Where(static d => d.Mode == RecognitionMode.Constrained).Select(static d => d.Milliseconds).ToArray();
        text.AppendLine($"for comparison, a constrained decode: median {BenchMetrics.Percentile(constrained, 0.5):0} ms, p95 {BenchMetrics.Percentile(constrained, 0.95):0} ms.");

        text.AppendLine("V31's lines («el río es grande», «me gusta el río») with a taken constrained reading, by prompt and voice: first reading -> final reading");
        foreach (var r in results.Where(static r => r.FirstTaken && r.Case.Text is "el río es grande" or "me gusta el río"))
        {
            text.AppendLine((r.Rescued ? "rescued: " : r.ForcedLine ? "STILL FORCED: " : "read as typed: ") + r.Describe());
        }

        foreach (var r in results.Where(static r => r.FalseTake || r.WrongTake))
        {
            text.AppendLine((r.FalseTake ? "FALSE TAKE: " : "wrong take: ") + r.Describe());
        }

        output.WriteLine(text.ToString());
    }
}
