using System.Text;
using System.Text.RegularExpressions;
using Vocaterra.Nlp.Asr;
using Vocaterra.Nlp.Audio;
using Vocaterra.Nlp.Bench;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Decision.ConversationAgents;
using Vocaterra.Nlp.Decision.Learning;
using Vocaterra.Nlp.Pipeline;

namespace Vocaterra.Nlp.Golden.Tests;

/// <summary>Checks the golden audio layout: the committed manifest, the synthetic clips a test generates, and that the pipeline agrees with the annotations.</summary>
public partial class ManifestGoldenTests
{
    private static readonly string GoldenDir = Path.Combine(AppContext.BaseDirectory, "golden");
    private static readonly ContentBundle Bundle = SpanishContent.Load(GameBundleLocator.Directory);

    [GeneratedRegex(@"^audio/[a-z0-9]+-[a-z0-9]+(-[a-z0-9]+)*-[1-9][0-9]*\.wav$")]
    private static partial Regex ClipName();

    private static IReadOnlyList<ManifestEntry> Entries()
    {
        using var reader = new StreamReader(Path.Combine(GoldenDir, "manifest.jsonl"), Encoding.UTF8);
        return ManifestReader.Read(reader);
    }

    /// <summary>Writes a one-second 16 kHz tone for the entry under the test output (never under the repository's tests/golden/audio).</summary>
    private static string WriteTone(ManifestEntry entry, int index)
    {
        var path = Path.Combine(GoldenDir, entry.Clip.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var samples = new short[PcmClip.DefaultSampleRate];
        var frequency = 220.0 * (index + 1);
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (short)Math.Round(8000.0 * Math.Sin(2.0 * Math.PI * frequency * i / PcmClip.DefaultSampleRate));
        }

        WavFile.Write(path, new WavData(PcmClip.DefaultSampleRate, 1, samples));
        return path;
    }

    [Fact]
    public void TheManifestHasWellFormedSyntheticEntries()
    {
        var entries = Entries();

        Assert.True(entries.Count >= 3);
        Assert.All(entries, entry =>
        {
            Assert.Matches(ClipName(), entry.Clip);
            Assert.Equal("synth", entry.Speaker);
            Assert.StartsWith($"audio/{entry.Speaker}-{entry.PromptId}-", entry.Clip, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(entry.L1));
            Assert.False(string.IsNullOrWhiteSpace(entry.ExpectedTranscript));
            Assert.Contains(entry.PromptId, Bundle.Prompts.Keys);
            Assert.All(entry.ExpectedErrors, error =>
            {
                Assert.True(ErrorTags.IsKnown(error.Tag), error.Tag);
                Assert.Contains(error.Actual, entry.ExpectedTranscript, StringComparison.Ordinal);
            });
            if (entry.ExpectedIntent is { } intent)
            {
                Assert.NotNull(Bundle.Intents.Find(intent));
            }
        });
        Assert.Equal(entries.Count, entries.Select(static e => e.Clip).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void TheTestWritesOneSecondTonesToItsOwnOutputOnly()
    {
        var entries = Entries();

        for (var i = 0; i < entries.Count; i++)
        {
            var path = WriteTone(entries[i], i);

            Assert.StartsWith(AppContext.BaseDirectory, path, StringComparison.Ordinal);
            var wav = WavFile.Read(path);
            Assert.Equal(PcmClip.DefaultSampleRate, wav.SampleRate);
            Assert.Equal(1, wav.Channels);
            Assert.Equal(1.0, wav.DurationSeconds, 6);
            var clip = new WavFileCaptureSource(path).Capture();
            Assert.Equal(PcmClip.DefaultSampleRate, clip.SampleRate);
            Assert.NotEmpty(clip.Samples);
        }
    }

    [Fact]
    public void TheBenchmarkScoresAPerfectRecognizerOnTheSyntheticClips()
    {
        var entries = Entries();
        var paths = entries.Select((entry, index) => WriteTone(entry, index)).ToArray();
        var clips = entries.Zip(paths).ToDictionary(static pair => pair.First.Clip, static pair => new WavFileCaptureSource(pair.Second).Capture(), StringComparer.Ordinal);
        var script = new Dictionary<string, AsrResult>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            script[clips[entry.Clip].ClipHash] = Asr(entry.ExpectedTranscript, "perfect");
        }

        var recognizer = new ScriptedWordRecognizer(null, script);
        var report = BenchRunner.Run(entries, Bundle, _ => recognizer, entry => clips[entry.Clip], RecognitionMode.Open);

        Assert.All(report.Entries, static result => Assert.Equal(BenchRunner.StatusOk, result.Status));
        Assert.All(report.Entries, static result => Assert.Equal(0, result.Edits));
        Assert.Equal(1.0, Assert.Single(report.Groups).ErrorPreservation);
        Assert.Equal(0.0, report.Groups[0].Wer);
    }

    [Fact]
    public void ACorrectingRecognizerLosesTheAnnotatedErrors()
    {
        var entries = Entries();
        var clip = PcmClip.FromSamples(new short[PcmClip.DefaultSampleRate]);
        var recognizer = new ScriptedWordRecognizer(Asr("quiero una espada", "correcting"));

        var report = BenchRunner.Run(entries, Bundle, _ => recognizer, _ => clip, RecognitionMode.Open);

        var group = Assert.Single(report.Groups);
        Assert.True(group.Wer > 0.0);
        Assert.True(group.ErrorPreservation < 1.0);
    }

    [Fact]
    public void ThePipelineAgreesWithEveryAnnotation()
    {
        foreach (var entry in Entries())
        {
            var prompt = Bundle.Prompts[entry.PromptId];
            var recognizer = new ScriptedWordRecognizer(new AsrResult(
                entry.ExpectedTranscript,
                [new AsrHypothesis(entry.ExpectedTranscript, 1.0)],
                [],
                1.0,
                "scripted-manifest",
                prompt.Mode));
            var pipeline = new UtterancePipeline(new PipelineServices(recognizer, null, null, null, Bundle));
            var context = ConversationContext.Create(1, "villager", new AgentState("villager", "idle", 0), new StoredTimestamp(0), LearnerModel.New());

            var verdict = pipeline.Evaluate(PcmClip.FromSamples([]), prompt, context).Verdict;

            Assert.Equal(entry.ExpectedIntent, verdict.Intent.Value);
            Assert.True(verdict.Communicated, entry.Clip);
            foreach (var expected in entry.ExpectedErrors)
            {
                Assert.Contains(verdict.Errors, error => error.Tag == expected.Tag && error.Actual == expected.Actual);
            }

            Assert.Equal(entry.ExpectedErrors.Count, verdict.Errors.Count);
        }
    }

    private static AsrResult Asr(string transcript, string modelHash)
    {
        return new AsrResult(transcript, [new AsrHypothesis(transcript, 1.0)], [], 1.0, modelHash, RecognitionMode.Open);
    }
}
