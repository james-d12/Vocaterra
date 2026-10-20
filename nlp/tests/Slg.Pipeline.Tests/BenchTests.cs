using System.Text.Json;
using Slg.Asr;
using Slg.Audio;
using Slg.Bench;
using Slg.Core.Audio;
using Slg.Core.Prompts;
using Slg.Core.Stages;
using BenchProgram = Slg.Bench.Program;

namespace Slg.Pipeline.Tests;

/// <summary>Tests for the ASR benchmark: the metrics, the runner on scripted results, and the command line.</summary>
public class BenchTests
{
    private static ManifestEntry Entry(
        string transcript,
        string promptId = "offer-items-open",
        string clip = "audio/e.wav",
        (string Tag, string Actual)[]? errors = null)
    {
        return new ManifestEntry(
            clip,
            promptId,
            "synth",
            "en",
            transcript,
            (errors ?? []).Select(static e => new ExpectedError(e.Tag, e.Actual)).ToArray(),
            null);
    }

    private static BenchReport Run(IReadOnlyList<ManifestEntry> entries, string hypothesis, RecognitionMode? mode = null, Func<ManifestEntry, PcmClip?>? clips = null)
    {
        var recognizer = new ScriptedWordRecognizer(TestSupport.Asr(hypothesis, RecognitionMode.Open, "bench-model"));
        return BenchRunner.Run(entries, TestSupport.Bundle, _ => recognizer, clips ?? (_ => TestSupport.Clip()), mode);
    }

    [Theory]
    [InlineData("", "", 0)]
    [InlineData("a b c", "a b c", 0)]
    [InlineData("a b c", "a c", 1)]
    [InlineData("a b c", "a x c", 1)]
    [InlineData("a b c", "a b c d", 1)]
    [InlineData("a b c", "", 3)]
    [InlineData("", "a b", 2)]
    [InlineData("yo tiene dos espada", "yo tengo dos espadas", 2)]
    public void WordEditsIsTheWordLevelLevenshteinDistance(string reference, string hypothesis, int expected)
    {
        var edits = BenchMetrics.WordEdits(BenchMetrics.Tokens(reference), BenchMetrics.Tokens(hypothesis));

        Assert.Equal(expected, edits);
    }

    [Fact]
    public void TokensAreNormalizedAndDropTheUnknownMarker()
    {
        Assert.Equal(["yo", "tiene", "2", "espada"], BenchMetrics.Tokens("¡Yo  TIENE dos [unk] espada!"));
        Assert.Equal(["una", "espada"], BenchMetrics.Tokens("una espada"));
        Assert.Empty(BenchMetrics.Tokens("[unk]"));
    }

    [Theory]
    [InlineData(0, 0, 0.0)]
    [InlineData(2, 0, 1.0)]
    [InlineData(1, 4, 0.25)]
    [InlineData(6, 4, 1.5)]
    public void WerDividesEditsByReferenceWords(int edits, int words, double expected)
    {
        Assert.Equal(expected, BenchMetrics.Wer(edits, words), 9);
    }

    [Fact]
    public void ErrorPreservationLooksForTheWrongWordInTheHypothesis()
    {
        ExpectedError[] errors = [new("conj.person", "tiene"), new("noun.number", "espada")];

        Assert.Equal(2, BenchMetrics.ErrorsKept(errors, BenchMetrics.Tokens("yo tiene dos espada")));
        Assert.Equal(1, BenchMetrics.ErrorsKept(errors, BenchMetrics.Tokens("yo tiene dos espadas")));
        Assert.Equal(0, BenchMetrics.ErrorsKept(errors, BenchMetrics.Tokens("yo tengo dos espadas")));
        Assert.Equal(0, BenchMetrics.ErrorsKept(errors, []));
        Assert.Equal(1, BenchMetrics.ErrorsKept([new("gender.agree", "un espada")], BenchMetrics.Tokens("quiero un espada")));
        Assert.Equal(0, BenchMetrics.ErrorsKept([new("gender.agree", "")], BenchMetrics.Tokens("quiero un espada")));
    }

    [Theory]
    [InlineData(0.50, 4.0)]
    [InlineData(0.95, 10.0)]
    [InlineData(1.0, 10.0)]
    [InlineData(0.10, 1.0)]
    public void PercentileUsesTheNearestRank(double fraction, double expected)
    {
        double[] values = [10.0, 1.0, 3.0, 2.0, 5.0, 4.0, 3.0, 8.0, 6.0, 7.0];

        Assert.Equal(expected, BenchMetrics.Percentile(values, fraction));
    }

    [Fact]
    public void PercentileOfNothingIsZeroAndOfOneValueIsThatValue()
    {
        Assert.Equal(0.0, BenchMetrics.Percentile([], 0.5));
        Assert.Equal(4.5, BenchMetrics.Percentile([4.5], 0.95));
        Assert.Equal(2.0, BenchMetrics.Percentile([3.0, 1.0, 2.0], 0.5));
    }

    [Fact]
    public void ARecognizerThatKeepsTheLearnersErrorsScoresPerfectly()
    {
        var entries = new[] { Entry("yo tiene dos espada", errors: [("conj.person", "tiene"), ("noun.number", "espada")]) };

        var report = Run(entries, "yo tiene dos espada");

        var result = Assert.Single(report.Entries);
        Assert.Equal(BenchRunner.StatusOk, result.Status);
        Assert.Equal(0, result.Edits);
        Assert.Equal(0.0, result.Wer);
        Assert.Equal((2, 2), (result.ErrorsExpected, result.ErrorsKept));
        var group = Assert.Single(report.Groups);
        Assert.Equal("bench-model", group.ModelHash);
        Assert.Equal(0.0, group.Wer);
        Assert.Equal(1.0, group.ErrorPreservation);
        Assert.Equal(1, group.Entries);
    }

    [Fact]
    public void ARecognizerThatCorrectsTheLearnerLosesTheErrors()
    {
        var entries = new[] { Entry("yo tiene dos espada", errors: [("conj.person", "tiene"), ("noun.number", "espada")]) };

        var report = Run(entries, "yo tengo dos espadas");

        var result = report.Entries[0];
        Assert.Equal(2, result.Edits);
        Assert.Equal(0.5, result.Wer);
        Assert.Equal(0, result.ErrorsKept);
        Assert.Equal(0.0, report.Groups[0].ErrorPreservation);
        Assert.Equal(0.5, report.Groups[0].Wer);
    }

    [Fact]
    public void GroupFiguresAreCorpusLevel()
    {
        var entries = new[]
        {
            Entry("hola amigo", "greet-open", "audio/1.wav"),
            Entry("yo tiene dos espada", clip: "audio/2.wav", errors: [("noun.number", "espada")]),
            Entry("quiero comprar un espada", "buy-item-open", "audio/3.wav", [("gender.agree", "un")]),
        };
        var clips = new Dictionary<string, PcmClip>
        {
            ["audio/1.wav"] = TestSupport.Clip(100),
            ["audio/2.wav"] = TestSupport.Clip(200),
            ["audio/3.wav"] = TestSupport.Clip(300),
        };
        var recognizer = new ScriptedWordRecognizer(
            null,
            new Dictionary<string, AsrResult>
            {
                [clips["audio/1.wav"].ClipHash] = TestSupport.Asr("hola amigo", modelHash: "m"),
                [clips["audio/2.wav"].ClipHash] = TestSupport.Asr("yo tiene dos espadas", modelHash: "m"),
                [clips["audio/3.wav"].ClipHash] = TestSupport.Asr("quiero comprar un espada", modelHash: "m"),
            });

        var report = BenchRunner.Run(entries, TestSupport.Bundle, _ => recognizer, entry => clips[entry.Clip]);

        Assert.Equal([2, 4, 4], report.Entries.Select(static e => e.ReferenceWords).ToArray());
        Assert.Equal([0, 1, 0], report.Entries.Select(static e => e.Edits).ToArray());
        var group = Assert.Single(report.Groups);
        Assert.Equal("scripted", group.ModelHash);
        Assert.Equal(3, group.Entries);
        Assert.Equal(0.1, group.Wer, 9);
        Assert.Equal(0.5, group.ErrorPreservation);
    }

    [Fact]
    public void EntriesWithoutAudioOrPromptAreListedButNotCounted()
    {
        var entries = new[] { Entry("hola"), Entry("hola", "no-such-prompt"), Entry("hola", "greet-open") };

        var report = Run(entries, "hola", clips: entry => entry.PromptId == "greet-open" ? null : TestSupport.Clip());

        Assert.Equal([BenchRunner.StatusOk, BenchRunner.StatusUnknownPrompt, BenchRunner.StatusMissingClip], report.Entries.Select(static e => e.Status).ToArray());
        Assert.Null(report.Entries[1].Wer);
        Assert.Equal(1, Assert.Single(report.Groups).Entries);
    }

    [Fact]
    public void TheModeOverrideReplacesThePromptsOwnMode()
    {
        var seen = new List<RecognitionMode>();
        var recognizer = new ScriptedWordRecognizer(TestSupport.Asr("hola"));

        var report = BenchRunner.Run(
            [Entry("hola", "greet-blacksmith"), Entry("hola", "greet-open")],
            TestSupport.Bundle,
            mode =>
            {
                seen.Add(mode);
                return recognizer;
            },
            _ => TestSupport.Clip(),
            RecognitionMode.Open);

        Assert.Equal([RecognitionMode.Open, RecognitionMode.Open], seen);
        Assert.All(report.Entries, static e => Assert.Equal(RecognitionMode.Open, e.Mode));
        Assert.Equal(RecognitionMode.Open, recognizer.LastRequest!.Mode);
    }

    [Fact]
    public void WithoutAnOverrideEachPromptKeepsItsOwnMode()
    {
        var recognizer = new ScriptedWordRecognizer(TestSupport.Asr("hola"));

        var report = BenchRunner.Run([Entry("hola", "greet-blacksmith"), Entry("hola", "greet-open")], TestSupport.Bundle, _ => recognizer, _ => TestSupport.Clip());

        Assert.Equal([RecognitionMode.Constrained, RecognitionMode.Open], report.Entries.Select(static e => e.Mode).ToArray());
        Assert.Equal(2, report.Groups.Count);
    }

    [Fact]
    public void LatencyIsMeasuredAndNonNegative()
    {
        var report = Run([Entry("hola"), Entry("hola")], "hola");

        Assert.All(report.Entries, static e => Assert.True(e.LatencyMs >= 0.0));
        var group = Assert.Single(report.Groups);
        Assert.True(group.LatencyP95Ms >= group.LatencyP50Ms);
    }

    [Fact]
    public void AnEmptyManifestStillPrintsTheTableHeadersAndASummary()
    {
        using var output = new StringWriter();

        ReportWriter.Write(new BenchReport([], []), output);

        var lines = output.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.Equal("clip  prompt  mode  WER  errors kept  latency ms  status", lines[0]);
        Assert.Equal("""{"entries":0,"groups":[]}""", lines[1]);
    }

    [Fact]
    public void TheTableAlignsEveryRow()
    {
        var report = Run([Entry("yo tiene dos espada", errors: [("conj.person", "tiene")])], "yo tengo dos espada");
        using var output = new StringWriter();

        ReportWriter.Write(report, output);

        var lines = output.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');
        Assert.Equal(3, lines.Length);
        Assert.StartsWith("clip", lines[0], StringComparison.Ordinal);
        Assert.Contains("0.250", lines[1], StringComparison.Ordinal);
        Assert.Contains("0/1", lines[1], StringComparison.Ordinal);
        using var summary = JsonDocument.Parse(lines[2]);
        Assert.Equal(1, summary.RootElement.GetProperty("entries").GetInt32());
        Assert.Equal("bench-model", summary.RootElement.GetProperty("groups")[0].GetProperty("modelHash").GetString());
        Assert.Equal(0.0, summary.RootElement.GetProperty("groups")[0].GetProperty("errorPreservation").GetDouble());
    }

    [Fact]
    public void ManifestLinesAreParsedAndBadLinesNameTheirNumber()
    {
        const string good = """{"clip":"audio/a.wav","promptId":"greet-open","speaker":"s01","l1":"en","expectedTranscript":"hola","expectedErrors":[{"tag":"vocab.wrong","actual":"ola"}],"expectedIntent":null}""";

        var entries = ManifestReader.Read(new StringReader($"{good}\n\n{good}\n"));
        var ex = Assert.Throws<InvalidDataException>(() => ManifestReader.Read(new StringReader($"{good}\n{{\"clip\":1}}\n")));

        Assert.Equal(2, entries.Count);
        Assert.Equal("ola", entries[0].ExpectedErrors[0].Actual);
        Assert.Null(entries[0].ExpectedIntent);
        Assert.StartsWith("Manifest line 2:", ex.Message, StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(() => ManifestReader.Read(new StringReader("""{"clip":"a.wav","promptId":"p"}""")));
    }

    [Fact]
    public void TheCommandLinePrintsHeadersForAnEmptyManifestWithoutNeedingModels()
    {
        var manifest = Path.Combine(Path.GetTempPath(), "slg-bench-" + Guid.NewGuid().ToString("N") + ".jsonl");
        File.WriteAllText(manifest, string.Empty);
        try
        {
            using var output = new StringWriter();
            using var error = new StringWriter();

            var code = BenchProgram.Run(["--manifest", manifest, "--content", TestSupport.BundleDir], output, error);

            Assert.Equal(BenchProgram.ExitOk, code);
            Assert.Equal(string.Empty, error.ToString());
            Assert.StartsWith("clip", output.ToString(), StringComparison.Ordinal);
            Assert.Contains("""{"entries":0,"groups":[]}""", output.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(manifest);
        }
    }

    [Fact]
    public void TheCommandLineNeedsModelsOnlyWhenThereAreEntriesToRun()
    {
        var directory = Directory.CreateTempSubdirectory("slg-bench-").FullName;
        try
        {
            var manifest = Path.Combine(directory, "manifest.jsonl");
            File.WriteAllText(manifest, """{"clip":"audio/a.wav","promptId":"greet-open","speaker":"s01","l1":"en","expectedTranscript":"hola","expectedErrors":[],"expectedIntent":null}""" + "\n");
            WavFile.Write(Path.Combine(Directory.CreateDirectory(Path.Combine(directory, "audio")).FullName, "a.wav"), new WavData(16000, 1, new short[16000]));
            using var output = new StringWriter();
            using var error = new StringWriter();

            var code = BenchProgram.Run(["--manifest", manifest, "--content", TestSupport.BundleDir], output, error);

            Assert.Equal(BenchProgram.ExitFailed, code);
            Assert.Contains("--models is required", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void TheCommandLineListsEntriesWhoseAudioIsMissing()
    {
        var directory = Directory.CreateTempSubdirectory("slg-bench-").FullName;
        try
        {
            var manifest = Path.Combine(directory, "manifest.jsonl");
            File.WriteAllText(manifest, """{"clip":"audio/a.wav","promptId":"greet-open","speaker":"s01","l1":"en","expectedTranscript":"hola","expectedErrors":[],"expectedIntent":null}""" + "\n");
            using var output = new StringWriter();
            using var error = new StringWriter();

            var code = BenchProgram.Run(["--manifest", manifest, "--content", TestSupport.BundleDir], output, error);

            Assert.Equal(BenchProgram.ExitOk, code);
            Assert.Contains("missing-clip", output.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void TheCommandLineRejectsBadArguments()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        Assert.Equal(BenchProgram.ExitUsage, BenchProgram.Run([], output, error));
        Assert.Equal(BenchProgram.ExitUsage, BenchProgram.Run(["--manifest", "m.jsonl"], output, error));
        Assert.Equal(BenchProgram.ExitUsage, BenchProgram.Run(["--manifest", "m.jsonl", "--content", "c", "--mode", "both"], output, error));
        Assert.Equal(BenchProgram.ExitFailed, BenchProgram.Run(["--manifest", "missing.jsonl", "--content", TestSupport.BundleDir], output, error));
        Assert.Equal(BenchProgram.ExitOk, BenchProgram.Run(["--help"], output, error));
    }
}
