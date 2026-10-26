using Vocaterra.Nlp.Asr;
using Vocaterra.Nlp.Asr.Models;
using Vocaterra.Nlp.Core.Audio;

namespace Vocaterra.Nlp.Pipeline.Tests;

/// <summary>Locates the model files and the committed manifest for the real-model tests.</summary>
internal static class ModelGate
{
    public const string VoskId = "vosk-model-small-es-0.42";

    private static readonly Lazy<ModelStore?> StoreValue = new(CreateStore);

    /// <summary>The store over <c>VOCATERRA_MODELS_DIR</c> and the committed manifest, or null when either is unavailable.</summary>
    public static ModelStore? Store => StoreValue.Value;

    public static bool IsAvailable(string id)
    {
        return Store is { } store && store.Verify(id).Status is ModelStatus.Ok or ModelStatus.Unpinned;
    }

    public static string SkipReason(string id)
    {
        return string.IsNullOrWhiteSpace(ModelStore.RootFromEnvironment())
            ? $"{ModelStore.EnvironmentVariable} is not set; run scripts/fetch-models.sh {id} and set it to the models directory."
            : $"{id} is missing or does not match models/manifest.json under {ModelStore.RootFromEnvironment()}.";
    }

    private static ModelStore? CreateStore()
    {
        var root = ModelStore.RootFromEnvironment();
        return string.IsNullOrWhiteSpace(root) || !File.Exists(TestSupport.ModelManifest)
            ? null
            : new ModelStore(root, ModelManifest.Load(TestSupport.ModelManifest));
    }
}

/// <summary>A fact that is skipped unless the Vosk small Spanish model is present and verifies against the manifest.</summary>
public sealed class VoskModelFactAttribute : FactAttribute
{
    /// <summary>Skips the test when the model is unavailable.</summary>
    public VoskModelFactAttribute()
    {
        if (!ModelGate.IsAvailable(ModelGate.VoskId))
        {
            Skip = ModelGate.SkipReason(ModelGate.VoskId);
        }
    }
}

/// <summary>The pipeline on the real Vosk model; skipped unless <c>VOCATERRA_MODELS_DIR</c> holds the model.</summary>
[Collection(NativeRuntimeCollection.Name)]
public class VoskPipelineTests
{
    private static PcmClip Speechlike()
    {
        // A vowel-like signal: a harmonic series gated at 4 Hz. It is not speech; the test only needs a real decode of real audio.
        var rate = PcmClip.DefaultSampleRate;
        var samples = new short[rate * 2];
        for (var i = 0; i < samples.Length; i++)
        {
            var t = (double)i / rate;
            var sum = 0.0;
            for (var k = 1; k <= 20; k++)
            {
                sum += Math.Sin(2.0 * Math.PI * 110.0 * k * t) / k;
            }

            samples[i] = (short)Math.Round(3000.0 * sum * (0.5 + (0.5 * Math.Sin(2.0 * Math.PI * 4.0 * t))));
        }

        return PcmClip.FromSamples(samples);
    }

    [VoskModelFact]
    public void ConstrainedPromptsRunOnTheRealModelDeterministically()
    {
        var store = ModelGate.Store!;
        using var vosk = VoskWordRecognizer.FromStore(store, ModelGate.VoskId);
        var pipeline = new UtterancePipeline(new PipelineServices(vosk, null, null, null, TestSupport.Bundle));
        var prompt = TestSupport.Bundle.Prompts["lesson-she"];
        var context = TestSupport.Context();
        var clip = Speechlike();

        var first = pipeline.Evaluate(clip, prompt, context);
        var second = pipeline.Evaluate(clip, prompt, context);

        Assert.Equal(TestSupport.Json(first), TestSupport.Json(second));
        Assert.Equal(store.ModelHash(ModelGate.VoskId), first.Verdict.Versions.ModelHashes["asr"]);
        Assert.Equal(TestSupport.Bundle.ContentHash, first.Verdict.Versions.ContentHash);
        Assert.NotEmpty(first.Response.Lines);
    }

    [VoskModelFact]
    public void ARealRecognitionReplaysWithoutTheModel()
    {
        var store = ModelGate.Store!;
        using var vosk = VoskWordRecognizer.FromStore(store, ModelGate.VoskId);
        var live = new UtterancePipeline(new PipelineServices(vosk, null, null, null, TestSupport.Bundle));
        var prompt = TestSupport.Bundle.Prompts[TestSupport.OpenPrompt];
        var context = TestSupport.Context();
        var trace = live.Trace(Speechlike(), prompt, context);
        using var writer = new StringWriter();
        new ReplayLog(writer).Append(ReplayEnvelope.From(trace, context));
        var envelope = ReplayReader.ReadEnvelopes(new StringReader(writer.ToString())).Single();
        var offline = TestSupport.Pipeline("never used");

        var diff = Replayer.Replay(envelope, offline, hash => new PcmClip([], PcmClip.DefaultSampleRate, hash));

        Assert.Equal(store.ModelHash(ModelGate.VoskId), envelope.Record.Versions.ModelHashes["asr"]);
        Assert.Null(diff);
    }
}
