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
/// Two turns of the game's bundle, end to end, each compared with a committed golden file. The first is the design document's worked example in the
/// bundle's words: scripted ASR hears "yo quiere dos espada" (a wrong verb form and a noun left singular after a number), a fake phoneme recognizer hears
/// it pronounced correctly, and the villager recasts it and answers. The second is a state transition: the shopkeeper, offering bread, hears "sí, por favor"
/// and finishes the trade with its host event. The pipeline runs on the repository's one bundle, <c>content/game-v1</c>, so a lesson that moves what the
/// core says here fails this test, and the milestone that changed the lesson records the golden again. Set <c>VOCATERRA_RECORD_GOLDEN=1</c> (or the deprecated
/// <c>SLG_RECORD_GOLDEN=1</c>) to (re)record the files after an intended change. The bundle hash, which moves with every edit of the game, is not in the golden
/// text: the golden holds a placeholder, and the test checks that the result carries the bundle's own hash.
/// </summary>
public class EndToEndGoldenTests
{
    private const string GoldenName = "e2e-villager-request.golden.json";
    private const string ConfirmGoldenName = "e2e-shopkeeper-confirm.golden.json";
    private const string HashPlaceholder = "<bundle-hash>";
    private const string RecordVariable = "VOCATERRA_RECORD_GOLDEN";
    private const string LegacyRecordVariable = "SLG_RECORD_GOLDEN";
    private const string Transcript = "yo quiere dos espada";
    private const string ConfirmTranscript = "sí, por favor";

    private static ContentBundle Bundle { get; } = ContentLoader.Load(GameBundleLocator.Directory);

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
            "villager",
            new AgentState("villager", "idle", 7),
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
        return pipeline.Evaluate(Clip(), Bundle.Prompts["villager-open"], Context());
    }

    [Fact]
    public void TheWorkedExampleYieldsTheHldVerdict()
    {
        var verdict = Run().Verdict;

        Assert.Equal("request_item", verdict.Intent.Value);
        Assert.Equal("espada", verdict.Slots["item"]);
        Assert.Equal("2", verdict.Slots["qty"]);
        Assert.True(verdict.Communicated);
        Assert.Equal([ErrorTags.ConjPerson, ErrorTags.NounNumber], verdict.Errors.Select(static e => e.Tag).ToArray());
        Assert.Equal(("quiere", "quiero"), (verdict.Errors[0].Actual, verdict.Errors[0].Expected));
        Assert.Equal(("espada", "espadas"), (verdict.Errors[1].Actual, verdict.Errors[1].Expected));
        Assert.NotEmpty(verdict.Pronunciation);
        Assert.All(verdict.Pronunciation, static score => Assert.Equal(1.0, score.Score, 6));
        Assert.Equal(["asr", "phoneme"], verdict.Versions.ModelHashes.Keys.ToArray());
        Assert.Equal(Bundle.ContentHash, verdict.Versions.ContentHash);
    }

    [Fact]
    public void TheVillagerRecastsTheSentenceAndGivesWhatWasAskedFor()
    {
        var result = Run();

        var recast = Assert.Single(result.Response.Lines, static line => line.IsRecast);
        Assert.Contains("dos espadas", recast.Text, StringComparison.Ordinal);
        Assert.Equal("idle", result.Decision.Agent!.Next.StateId);
        Assert.Contains(result.Decision.Agent.Actions, static action => action.HostEvent == "villager.give");
    }

    [Fact]
    public void TwoRunsAreByteIdentical()
    {
        Assert.Equal(CanonicalJson.Serialize(Run()), CanonicalJson.Serialize(Run()));
    }

    // The confirm flow of the shopkeeper: the agent is offering bread and the learner says "sí, por favor", so the bread is given and the agent is done.
    private static PipelineResult RunConfirm()
    {
        var asr = new AsrResult(ConfirmTranscript, [new AsrHypothesis(ConfirmTranscript, 1.0)], [], 1.0, "scripted-golden", RecognitionMode.Open);
        var context = ConversationContext.Create(
            20_261_021UL,
            "shopkeeper",
            new AgentState("shopkeeper", "offering", 3),
            new StoredTimestamp(1_792_000_000),
            LearnerModel.New(),
            new Dictionary<string, int>());
        var pipeline = new UtterancePipeline(new PipelineServices(new ScriptedWordRecognizer(asr), null, null, null, Bundle));
        return pipeline.Evaluate(Clip(), Bundle.Prompts["stall-offer"], context);
    }

    [Fact]
    public void TheConfirmFlowFromOfferingEndsTheTradeWithItsHostEvent()
    {
        var result = RunConfirm();

        Assert.Equal("confirm", result.Verdict.Intent.Value);
        Assert.True(result.Verdict.Communicated);
        Assert.Empty(result.Verdict.Errors);
        Assert.Equal("done", result.Decision.Agent!.Next.StateId);
        Assert.Contains(result.Decision.Agent.Actions, static action => action.HostEvent == "stall.give_bread");
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
        // The result stamps the bundle hash on the verdict and on the replay record; it is computed from the files, and the golden keeps a placeholder.
        var computed = ContentManifest.Compute(GameBundleLocator.Directory).BundleHash;
        Assert.Equal(computed, Bundle.ContentHash);
        Assert.Contains(computed, actual, StringComparison.Ordinal);
        actual = actual.Replace(computed, HashPlaceholder, StringComparison.Ordinal);

        if (string.Equals(EnvironmentSettings.Get(RecordVariable, LegacyRecordVariable), "1", StringComparison.Ordinal))
        {
            File.WriteAllText(GoldenFiles.SourcePath(Bundle.LanguageId, goldenName), Format(actual, indented: true) + "\n", new UTF8Encoding(false));
            return;
        }

        var goldenPath = GoldenFiles.RunPath(Bundle.LanguageId, goldenName);
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

}
