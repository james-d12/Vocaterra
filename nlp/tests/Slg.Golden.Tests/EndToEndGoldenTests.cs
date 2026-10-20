using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Slg.Asr;
using Slg.Content;
using Slg.Core.Audio;
using Slg.Core.Determinism;
using Slg.Core.Prompts;
using Slg.Core.Stages;
using Slg.Core.Taxonomy;
using Slg.Decision.Learning;
using Slg.Decision.Npc;
using Slg.Phonology;
using Slg.Pipeline;
using Slg.Pronunciation;

namespace Slg.Golden.Tests;

/// <summary>
/// The worked example from the high-level design, end to end: scripted ASR hears "yo tiene dos espada", a fake phoneme recognizer hears it
/// pronounced correctly, and the whole pipeline runs on the committed <c>content/v1</c> bundle. The full result is compared with a committed
/// golden file. Set <c>SLG_RECORD_GOLDEN=1</c> to (re)record that file after an intended change.
/// </summary>
public class EndToEndGoldenTests
{
    private const string GoldenName = "e2e-offer-items-open.golden.json";
    private const string RecordVariable = "SLG_RECORD_GOLDEN";
    private const string Transcript = "yo tiene dos espada";

    private static readonly string BundleDir = Path.Combine(AppContext.BaseDirectory, "content", "v1");
    private static readonly string GoldenPath = Path.Combine(AppContext.BaseDirectory, "Data", GoldenName);

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

    private static GameContext Context()
    {
        return GameContext.Create(
            20_261_018UL,
            "blacksmith",
            new NpcState("blacksmith", "trading", 7),
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
    public void TheBlacksmithRecastsFromThePlayersOwnSlots()
    {
        var result = Run();

        var recast = Assert.Single(result.Response.Lines, static line => line.IsRecast);
        Assert.Contains("dos espadas", recast.Text, StringComparison.Ordinal);
        Assert.Equal("¿Tienes dos espadas? ¡Perfecto!", recast.Text);
        Assert.Equal("done", result.Decision.Npc!.Next.StateId);
        Assert.Contains(result.Decision.Npc.Actions, static action => action.GameEvent == "trade.offer");
    }

    [Fact]
    public void TwoRunsAreByteIdentical()
    {
        Assert.Equal(CanonicalJson.Serialize(Run()), CanonicalJson.Serialize(Run()));
    }

    [Fact]
    public void TheResultMatchesTheCommittedGolden()
    {
        var actual = CanonicalJson.Serialize(Run());

        if (string.Equals(Environment.GetEnvironmentVariable(RecordVariable), "1", StringComparison.Ordinal))
        {
            File.WriteAllText(SourceGoldenPath(), Format(actual, indented: true) + "\n", new UTF8Encoding(false));
            return;
        }

        Assert.True(File.Exists(GoldenPath), $"Golden file '{GoldenName}' is missing; run the tests once with {RecordVariable}=1 to record it.");
        var expected = Format(File.ReadAllText(GoldenPath, Encoding.UTF8), indented: false);
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

    private static string SourceGoldenPath()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var data = Path.Combine(directory.FullName, "tests", "Slg.Golden.Tests", "Data");
            if (Directory.Exists(data))
            {
                return Path.Combine(data, GoldenName);
            }
        }

        throw new DirectoryNotFoundException("Could not find tests/Slg.Golden.Tests/Data above the test binaries.");
    }
}
