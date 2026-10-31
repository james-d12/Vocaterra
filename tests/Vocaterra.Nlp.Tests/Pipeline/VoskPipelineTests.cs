using Vocaterra.Nlp.Asr;
using Vocaterra.Nlp.Asr.Tests;
using Vocaterra.Nlp.Core.Audio;

namespace Vocaterra.Nlp.Pipeline.Tests;

/// <summary>The pipeline on the real Vosk model; skipped unless <c>VOCATERRA_MODELS_DIR</c> holds the model (the ASR tests' model gate).</summary>
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
    [Trait(TestSpeed.Trait, TestSpeed.Slow)]
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
    [Trait(TestSpeed.Trait, TestSpeed.Slow)]
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
