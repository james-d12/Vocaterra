using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Vocaterra.Nlp.Asr;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Configuration;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Decision.ConversationAgents;
using Vocaterra.Nlp.Decision.Learning;
using Vocaterra.Nlp.Phonology;
using Vocaterra.Nlp.Pipeline;
using Vocaterra.Nlp.Pronunciation;

namespace Vocaterra.Nlp.Golden.Tests;

/// <summary>
/// The worked example from the high-level design, end to end: scripted ASR hears "yo tiene dos espada", a fake phoneme recognizer hears it
/// pronounced correctly, and the whole pipeline runs on the committed <c>content/v1</c> bundle. The full result is compared with a committed
/// golden file. Set <c>VOCATERRA_RECORD_GOLDEN=1</c> (or the deprecated <c>SLG_RECORD_GOLDEN=1</c>) to (re)record that file after an intended change.
/// </summary>
public class EndToEndGoldenTests
{
    private const string GoldenName = "e2e-offer-items-open.golden.json";
    private const string ConfirmGoldenName = "e2e-confirm-open.golden.json";
    private const string RecordVariable = "VOCATERRA_RECORD_GOLDEN";
    private const string LegacyRecordVariable = "SLG_RECORD_GOLDEN";
    private const string Transcript = "yo tiene dos espada";
    private const string ConfirmTranscript = "sí";

    private static readonly string BundleDir = Path.Combine(AppContext.BaseDirectory, "content", "v1");

    private static ContentBundle Bundle { get; } = ContentLoader.Load(BundleDir);

    private static PcmClip Clip()
    {
        // A fixed one-second tone: the recognizers are fakes, so only the clip's hash matters.
        var samples = new short[PcmClip.DefaultSampleRate];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (short)Math.Round(6000.0 * Math.Sin(2.0 * Math.PI * 220.0 * i / PcmClip.DefaultSampleRate));
        }

        return PcmClip.FromSamples(samples);
    }

    private static ConversationContext Context()
    {
        return ConversationContext.Create(
            20_261_018UL,
            "blacksmith",
            new AgentState("blacksmith", "trading", 7),
            new StoredTimestamp(1_792_000_000),
            LearnerModel.New(),
            new Dictionary<string, int>());
    }

    private static PipelineResult Run()
    {
        var asr = new AsrResult(Transcript, [new AsrHypothesis(Transcript, 1.0)], [], 1.0, "scripted-golden", RecognitionMode.Open);
        var fake = FakePhonemeRecognizer.ForSpanish();
        var phones = SpanishG2p.Phonemize(Transcript, DialectConfig.LatinAmericanNeutral).Phones;
        fake.Returns(fake.FromPhones(phones));
        var pipeline = new UtterancePipeline(new PipelineServices(new ScriptedWordRecognizer(asr), fake, null, null, Bundle));
        return pipeline.Evaluate(Clip(), Bundle.Prompts["offer-items-open"], Context());
    }

    [Fact]
    public void TheWorkedExampleYieldsTheHldVerdict()
    {
        var verdict = Run().Verdict;

        Assert.Equal("offer_item", verdict.Intent.Value);
        Assert.Equal("espada", verdict.Slots["item"]);
        Assert.Equal("2", verdict.Slots["qty"]);
        Assert.True(verdict.Communicated);
        Assert.Equal([ErrorTags.ConjPerson, ErrorTags.NounNumber], verdict.Errors.Select(static e => e.Tag).ToArray());
        Assert.Equal(("tiene", "tengo"), (verdict.Errors[0].Actual, verdict.Errors[0].Expected));
        Assert.Equal(("espada", "espadas"), (verdict.Errors[1].Actual, verdict.Errors[1].Expected));
        Assert.NotEmpty(verdict.Pronunciation);
        Assert.All(verdict.Pronunciation, static score => Assert.Equal(1.0, score.Score, 6));
        Assert.Equal(["asr", "phoneme"], verdict.Versions.ModelHashes.Keys.ToArray());
        Assert.Equal(Bundle.ContentHash, verdict.Versions.ContentHash);
    }

    [Fact]
    public void TheSampleAgentRecastsFromThePlayersOwnSlots()
    {
        var result = Run();

        var recast = Assert.Single(result.Response.Lines, static line => line.IsRecast);
        Assert.Contains("dos espadas", recast.Text, StringComparison.Ordinal);
        Assert.Equal("¿Tienes dos espadas? ¡Perfecto!", recast.Text);
        Assert.Equal("done", result.Decision.Agent!.Next.StateId);
        Assert.Contains(result.Decision.Agent.Actions, static action => action.HostEvent == "trade.offer");
    }

    [Fact]
    public void TwoRunsAreByteIdentical()
    {
        Assert.Equal(CanonicalJson.Serialize(Run()), CanonicalJson.Serialize(Run()));
    }

    // The confirm flow of the sample agent (M17b): the agent is in its confirming state and the learner says "sí", so the trade is confirmed and the agent is done.
    private static PipelineResult RunConfirm()
    {
        var asr = new AsrResult(ConfirmTranscript, [new AsrHypothesis(ConfirmTranscript, 1.0)], [], 1.0, "scripted-golden", RecognitionMode.Open);
        var context = ConversationContext.Create(
            20_261_021UL,
            "blacksmith",
            new AgentState("blacksmith", "confirming", 3),
            new StoredTimestamp(1_792_000_000),
            LearnerModel.New(),
            new Dictionary<string, int>());
        var pipeline = new UtterancePipeline(new PipelineServices(new ScriptedWordRecognizer(asr), null, null, null, Bundle));
        return pipeline.Evaluate(Clip(), Bundle.Prompts["confirm-open"], context);
    }

    [Fact]
    public void TheConfirmFlowFromConfirmingEndsTheTradeWithItsHostEvent()
    {
        var result = RunConfirm();

        Assert.Equal("confirm", result.Verdict.Intent.Value);
        Assert.True(result.Verdict.Communicated);
        Assert.Empty(result.Verdict.Errors);
        Assert.Equal("done", result.Decision.Agent!.Next.StateId);
        Assert.Contains(result.Decision.Agent.Actions, static action => action.HostEvent == "trade.confirmed");
        Assert.Single(result.Response.Lines);
    }

    [Fact]
    public void TheResultMatchesTheCommittedGolden()
    {
        AssertGolden(CanonicalJson.Serialize(Run()), GoldenName);
    }

    [Fact]
    public void TheConfirmFlowMatchesItsCommittedGolden()
    {
        AssertGolden(CanonicalJson.Serialize(RunConfirm()), ConfirmGoldenName);
    }

    private static void AssertGolden(string actual, string goldenName)
    {
        if (string.Equals(EnvironmentSettings.Get(RecordVariable, LegacyRecordVariable), "1", StringComparison.Ordinal))
        {
            File.WriteAllText(SourceGoldenPath(goldenName), Format(actual, indented: true) + "\n", new UTF8Encoding(false));
            return;
        }

        var goldenPath = Path.Combine(AppContext.BaseDirectory, "Data", goldenName);
        Assert.True(File.Exists(goldenPath), $"Golden file '{goldenName}' is missing; run the tests once with {RecordVariable}=1 to record it.");
        var expected = Format(File.ReadAllText(goldenPath, Encoding.UTF8), indented: false);
        Assert.Equal(expected, actual);
    }

    /// <summary>Rewrites JSON text compactly or indented without touching property order, numbers or the canonical escaping.</summary>
    private static string Format(string json, bool indented)
    {
        using var document = JsonDocument.Parse(json);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = indented,
            NewLine = "\n",
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }))
        {
            document.RootElement.WriteTo(writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string SourceGoldenPath(string goldenName)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var data = Path.Combine(directory.FullName, "tests", "Vocaterra.Nlp.Golden.Tests", "Data");
            if (Directory.Exists(data))
            {
                return Path.Combine(data, goldenName);
            }
        }

        throw new DirectoryNotFoundException("Could not find tests/Vocaterra.Nlp.Golden.Tests/Data above the test binaries.");
    }
}
