namespace Vocaterra.Nlp.Response.Tests.Speech;

using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Phonology;
using Vocaterra.Nlp.Response.Speech;

public class SynthesizerTests
{
    [Fact]
    public void SilentSynthesizerProducesOneHundredMillisecondsAtSixteenKilohertz()
    {
        var buffer = new SilentSynthesizer().Synthesize(new SynthesisRequest("hola", "voz", null));
        Assert.Equal(16_000, buffer.SampleRate);
        Assert.Equal(1_600, buffer.Samples.Length);
        Assert.Equal(0.1, buffer.DurationSeconds, 1e-12);
        Assert.All(buffer.Samples.Skip(16), static sample => Assert.Equal(0, sample));
    }

    [Fact]
    public void SilentSynthesizerSamplesEncodeTheTextHash()
    {
        var synthesizer = new SilentSynthesizer();
        var buffer = synthesizer.Synthesize(new SynthesisRequest("¿Tienes dos espadas?", "voz", null));
        Assert.Equal(DeterministicHash.OfText("¿Tienes dos espadas?"), SilentSynthesizer.ReadTextHash(buffer));
        Assert.Contains(buffer.Samples.Take(16), static sample => sample != 0);
    }

    [Fact]
    public void SilentSynthesizerIsDeterministicAndTextSensitive()
    {
        var synthesizer = new SilentSynthesizer();
        var a1 = synthesizer.Synthesize(new SynthesisRequest("hola", "v1", null));
        var a2 = synthesizer.Synthesize(new SynthesisRequest("hola", "v2", ["o"]));
        var b = synthesizer.Synthesize(new SynthesisRequest("adiós", "v1", null));
        Assert.Equal(a1, a2);
        Assert.NotEqual(a1, b);
        Assert.Equal(3, synthesizer.CallCount);
    }

    [Fact]
    public void SpeechRequestsAttachPhonesFromTheSpanishG2pAndSpellNumbers()
    {
        var request = SpeechRequests.Create("Tengo 2 espadas", "voz");
        Assert.Equal("Tengo dos espadas", request.Text);
        Assert.Equal("voz", request.VoiceId);
        Assert.Equal(SpanishG2p.Phonemize("tengo dos espadas", DialectConfig.LatinAmericanNeutral).Phones, request.Phones);
        Assert.NotEmpty(request.Phones!);

        var castilian = SpeechRequests.Create("zapato", "voz", DialectConfig.CastilianStandard);
        var latin = SpeechRequests.Create("zapato", "voz");
        Assert.NotEqual(castilian.Phones, latin.Phones);
    }

    [Fact]
    public void KokoroRejectsMissingModelFilesAndBadArguments()
    {
        var missing = Path.Combine(Path.GetTempPath(), "vocaterra-no-kokoro-" + Guid.NewGuid().ToString("N"));
        Assert.Throws<FileNotFoundException>(() => new KokoroSynthesizer(missing, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new KokoroSynthesizer(missing, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new KokoroSynthesizer(missing, 0, 0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => new KokoroSynthesizer(missing, 0, float.NaN));
        Assert.Throws<ArgumentException>(() => new KokoroSynthesizer(" ", 0));
    }

    [RequiresModelFact(KokoroSynthesizer.ModelDirectoryName)]
    public void KokoroSpeaksSpanishDeterministicallyWithTheModelSampleRate()
    {
        var modelDir = RequiresModelFactAttribute.ModelPath(KokoroSynthesizer.ModelDirectoryName)!;
        using var synthesizer = new KokoroSynthesizer(modelDir, speakerId: 28);
        var request = SpeechRequests.Create("¿Tienes dos espadas?", "kokoro-es");

        var first = synthesizer.Synthesize(request);
        var second = synthesizer.Synthesize(request);

        Assert.True(first.SampleRate > 0);
        Assert.True(first.Samples.Length > first.SampleRate / 4, "Expected at least a quarter second of speech.");
        Assert.Contains(first.Samples, static sample => sample != 0);
        Assert.Equal(first, second);
        Assert.Equal(synthesizer.ModelHash, synthesizer.ModelHash);
    }
}
