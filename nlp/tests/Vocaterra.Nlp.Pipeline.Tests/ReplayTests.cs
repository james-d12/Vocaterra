using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Replay;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Nlp.Pipeline.Tests;

/// <summary>Tests for the replay log, the reader and the replayer.</summary>
public class ReplayTests
{
    private static readonly PcmClip Clip = TestSupport.Clip();

    private static ReplayEnvelope Record(string transcript = TestSupport.WorkedExample, bool phonemes = true, string state = "trading")
    {
        var pipeline = TestSupport.Pipeline(transcript, phonemes: phonemes);
        var context = TestSupport.Context(state: state, counter: 3);
        var trace = pipeline.Trace(Clip, TestSupport.Bundle.Prompts[TestSupport.OfferPrompt], context);
        return ReplayEnvelope.From(trace, context);
    }

    private static UtterancePipeline LivePipeline(string transcript = TestSupport.WorkedExample)
    {
        return TestSupport.Pipeline(transcript);
    }

    private static PcmClip ClipByHash(string hash)
    {
        return new PcmClip([], PcmClip.DefaultSampleRate, hash);
    }

    private static string Write(params ReplayEnvelope[] envelopes)
    {
        using var writer = new StringWriter();
        var log = new ReplayLog(writer);
        foreach (var envelope in envelopes)
        {
            log.Append(envelope);
        }

        return writer.ToString();
    }

    [Fact]
    public void TheLogWritesOneLfTerminatedCanonicalLinePerEntry()
    {
        var first = Record();
        var second = Record("hola", phonemes: false);

        var text = Write(first, second);

        var lines = text.Split('\n');
        Assert.Equal(3, lines.Length);
        Assert.Equal(string.Empty, lines[2]);
        Assert.Equal(CanonicalJson.Serialize(first), lines[0]);
        Assert.Equal(CanonicalJson.Serialize(second), lines[1]);
        Assert.DoesNotContain('\r', text);
    }

    [Fact]
    public void BareRecordsAreWrittenAndReadBack()
    {
        var envelope = Record();
        using var writer = new StringWriter();
        new ReplayLog(writer).Append(envelope.Record);

        var records = ReplayReader.Read(new StringReader(writer.ToString()));

        Assert.Equal([envelope.Record], records);
    }

    [Fact]
    public void ReadReturnsTheRecordOfEveryEnvelopeLine()
    {
        var first = Record();
        var second = Record("hola");

        var records = ReplayReader.Read(new StringReader(Write(first, second)));

        Assert.Equal([first.Record, second.Record], records);
    }

    [Fact]
    public void EnvelopesRoundTripThroughTheLog()
    {
        var first = Record();
        var second = Record("hola", phonemes: false);

        var envelopes = ReplayReader.ReadEnvelopes(new StringReader(Write(first, second)));

        Assert.Equal(2, envelopes.Count);
        Assert.Equal(CanonicalJson.Serialize(first), CanonicalJson.Serialize(envelopes[0]));
        Assert.Equal(CanonicalJson.Serialize(second), CanonicalJson.Serialize(envelopes[1]));
        Assert.NotNull(envelopes[0].Posteriors);
        Assert.Null(envelopes[1].Posteriors);
        Assert.Equal(first.Record, envelopes[0].Record);
    }

    [Fact]
    public void BlankLinesAreSkippedAndErrorsNameTheLine()
    {
        var good = CanonicalJson.Serialize(Record());

        var ex = Assert.Throws<InvalidDataException>(() => ReplayReader.ReadEnvelopes(new StringReader($"{good}\n\n{{\"record\":1}}\n")));

        Assert.StartsWith("Replay log line 3:", ex.Message, StringComparison.Ordinal);
        Assert.Single(ReplayReader.ReadEnvelopes(new StringReader($"\n{good}\n\n")));
        Assert.Throws<InvalidDataException>(() => ReplayReader.Read(new StringReader("not json")));
        Assert.Throws<InvalidDataException>(() => ReplayReader.ReadEnvelopes(new StringReader(CanonicalJson.Serialize(Record().Record))));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReplayingAnUntouchedEnvelopeFindsNoDifference(bool phonemes)
    {
        var envelope = Record(phonemes: phonemes);

        var diff = Replayer.Replay(envelope, LivePipeline(), ClipByHash);

        Assert.Null(diff);
    }

    [Fact]
    public void ReplayNeedsNeitherTheModelNorTheAudio()
    {
        var envelope = ReplayReader.ReadEnvelopes(new StringReader(Write(Record()))).Single();
        var pipeline = TestSupport.Pipeline("this transcript is never used");

        var diff = Replayer.Replay(envelope, pipeline, ClipByHash);

        Assert.Null(diff);
    }

    [Fact]
    public void MutatingOneSlotReportsThatSlotsPath()
    {
        var envelope = Record();
        var slots = envelope.Record.Verdict.Slots.With("qty", "3");
        var mutated = envelope with { Record = envelope.Record with { Verdict = envelope.Record.Verdict with { Slots = slots } } };

        var diff = Replayer.Replay(mutated, LivePipeline(), ClipByHash);

        Assert.NotNull(diff);
        Assert.Equal(["/verdict/slots/qty"], diff.Paths);
    }

    [Fact]
    public void AddedAndRemovedSlotsAreReportedToo()
    {
        var envelope = Record();
        var slots = envelope.Record.Verdict.Slots.With("extra", "x");
        var mutated = envelope with { Record = envelope.Record with { Verdict = envelope.Record.Verdict with { Slots = slots } } };

        var diff = Replayer.Replay(mutated, LivePipeline(), ClipByHash);

        Assert.Equal(["/verdict/slots/extra"], diff!.Paths);
    }

    [Fact]
    public void MutatingTheVerdictsErrorsReportsTheErrorIndex()
    {
        var envelope = Record();
        var verdict = envelope.Record.Verdict;
        var mutated = envelope with
        {
            Record = envelope.Record with { Verdict = verdict with { Errors = [verdict.Errors[0] with { Expected = "tenemos" }, verdict.Errors[1]] } },
        };

        var diff = Replayer.Replay(mutated, LivePipeline(), ClipByHash);

        Assert.Equal(["/verdict/errors/0/expected"], diff!.Paths);
    }

    [Fact]
    public void AChangedModelHashIsReported()
    {
        var envelope = Record();
        var versions = new VersionStamp(envelope.Record.Versions.ContentHash, new Dictionary<string, string> { ["asr"] = "other-model", ["phoneme"] = "fake-phoneme-recognizer" });
        var mutated = envelope with { Record = envelope.Record with { Versions = versions } };

        var diff = Replayer.Replay(mutated, LivePipeline(), ClipByHash);

        Assert.Equal(["/versions/modelHashes/asr"], diff!.Paths);
    }

    [Fact]
    public void ADifferentClipHashIsReportedByTheRecordsClipHash()
    {
        var envelope = Record();
        var mutated = envelope with { Record = envelope.Record with { ClipHash = "deadbeef" } };

        var diff = Replayer.Replay(mutated, LivePipeline(), _ => ClipByHash(Clip.ClipHash));

        Assert.Equal(["/clipHash"], diff!.Paths);
    }

    [Fact]
    public void ADriftInTheDownstreamResultIsReportedWithItsPath()
    {
        var envelope = Record();
        var lines = envelope.Result.Response.Lines.ToArray();
        lines[1] = lines[1] with { Text = "something else entirely" };
        var mutated = envelope with { Result = envelope.Result with { Response = new Vocaterra.Nlp.Response.Templates.ResponsePlan(lines) } };

        var diff = Replayer.Replay(mutated, LivePipeline(), ClipByHash);

        Assert.Equal(["/response/lines/1/text"], diff!.Paths);
    }

    [Fact]
    public void ADriftInTheDecisionIsReported()
    {
        var envelope = Record();
        var decision = envelope.Result.Decision with { Rating = Vocaterra.Nlp.Decision.Scheduling.Rating.Easy };
        var mutated = envelope with { Result = envelope.Result with { Decision = decision } };

        var diff = Replayer.Replay(mutated, LivePipeline(), ClipByHash);

        Assert.Equal(["/decision/rating"], diff!.Paths);
    }

    [Fact]
    public void ARecordedPromptThatTheBundleLacksIsRejected()
    {
        var envelope = Record();
        var mutated = envelope with { Record = envelope.Record with { PromptId = "no-such-prompt" } };

        var ex = Assert.Throws<InvalidDataException>(() => Replayer.Replay(mutated, LivePipeline(), ClipByHash));

        Assert.Contains("no-such-prompt", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReplayingABareRecordReRunsTheLivePipelineInTheGivenContext()
    {
        var envelope = Record(phonemes: false);

        var same = Replayer.Replay(envelope.Record, envelope.Context, LivePipeline(), ClipByHash);
        var different = Replayer.Replay(envelope.Record, envelope.Context, LivePipeline("tengo dos espadas"), ClipByHash);

        Assert.Null(same);
        Assert.NotNull(different);
        Assert.Contains("/verdict/errors/0", different.Paths);
    }

    [Fact]
    public void ADiffPrintsItsPathsAndComparesByValue()
    {
        var diff = new ReplayDiff(["/a", "/b/0"]);

        Assert.Equal("/a, /b/0", diff.ToString());
        Assert.Equal(diff, new ReplayDiff(["/a", "/b/0"]));
        Assert.NotEqual(diff, new ReplayDiff(["/a"]));
        Assert.Equal(diff.GetHashCode(), new ReplayDiff(["/a", "/b/0"]).GetHashCode());
    }

    [Fact]
    public void JsonPointerPathsEscapeSlashAndTilde()
    {
        var paths = JsonDiff.Paths("""{"a/b":{"c~d":1,"e":[1,2]}}""", """{"a/b":{"c~d":2,"e":[1,3,4]}}""");

        Assert.Equal(["/a~1b/c~0d", "/a~1b/e/1", "/a~1b/e/2"], paths);
    }

    [Fact]
    public void IdenticalDocumentsHaveNoPaths()
    {
        Assert.Empty(JsonDiff.Paths("""{"a":[1,{"b":null}]}""", """{"a":[1,{"b":null}]}"""));
        Assert.Equal([string.Empty], JsonDiff.Paths("1", "2"));
    }

    [Fact]
    public void TheConversationContextHashDependsOnEveryValueAndSortsCounts()
    {
        var baseline = TestSupport.Context(repeats: new Dictionary<string, int> { ["b"] = 1, ["a"] = 2 });
        var reordered = TestSupport.Context(repeats: new Dictionary<string, int> { ["a"] = 2, ["b"] = 1 });

        Assert.Equal(baseline.HostStateHash, reordered.HostStateHash);
        Assert.Equal(baseline, reordered);
        Assert.Equal(["a", "b"], baseline.RepeatCountsByTag.Keys.ToArray());
        Assert.NotEqual(baseline.HostStateHash, TestSupport.Context(repeats: new Dictionary<string, int> { ["a"] = 3, ["b"] = 1 }).HostStateHash);
        Assert.NotEqual(baseline.HostStateHash, TestSupport.Context(seed: 7, repeats: new Dictionary<string, int> { ["b"] = 1, ["a"] = 2 }).HostStateHash);
        Assert.NotEqual(baseline.HostStateHash, TestSupport.Context(counter: 1, repeats: new Dictionary<string, int> { ["b"] = 1, ["a"] = 2 }).HostStateHash);
    }

    [Fact]
    public void TheConversationContextRoundTripsThroughCanonicalJson()
    {
        var context = TestSupport.Context(repeats: new Dictionary<string, int> { ["noun.number"] = 2, ["conj.person"] = 1 });

        var json = CanonicalJson.Serialize(context);
        var back = CanonicalJson.Deserialize<ConversationContext>(json);

        Assert.Equal(json, CanonicalJson.Serialize(back));
        Assert.Equal(context, back);
        Assert.Equal(context.GetHashCode(), back.GetHashCode());
    }
}
