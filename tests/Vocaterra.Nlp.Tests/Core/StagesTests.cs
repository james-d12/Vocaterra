using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Nlp.Core.Tests;

/// <summary>Tests for the stage contracts, prompts and audio types.</summary>
public class StagesTests
{
    [Fact]
    public void PcmClipFromSamplesComputesHashAndDuration()
    {
        short[] samples = [1, -2, 300];
        var clip = PcmClip.FromSamples(samples);
        Assert.Equal(16000, clip.SampleRate);
        Assert.Equal(DeterministicHash.OfSamples(samples), clip.ClipHash);
        Assert.Equal(3.0 / 16000, clip.DurationSeconds);
        Assert.Equal(1.0, PcmClip.FromSamples(new short[8000], 8000).DurationSeconds);
    }

    [Fact]
    public void PcmClipValidatesArguments()
    {
        Assert.Throws<ArgumentNullException>(() => PcmClip.FromSamples(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => PcmClip.FromSamples([1], 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PcmClip.FromSamples([1], -16000));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PcmClip([1], 0, "h"));
    }

    [Fact]
    public void PcmClipEqualityComparesSampleContents()
    {
        Assert.Equal(PcmClip.FromSamples([1, 2, 3]), PcmClip.FromSamples([1, 2, 3]));
        Assert.NotEqual(PcmClip.FromSamples([1, 2, 3]), PcmClip.FromSamples([1, 2, 4]));
        Assert.Equal(new AudioBuffer([1, 2], 22050), new AudioBuffer([1, 2], 22050));
    }

    [Fact]
    public void RecognitionRequestFactories()
    {
        var constrained = RecognitionRequest.Constrained(["hola", "adios"]);
        Assert.Equal(RecognitionMode.Constrained, constrained.Mode);
        Assert.Equal(["hola", "adios"], constrained.GrammarPhrases);
        Assert.Equal(5, constrained.MaxAlternatives);

        var open = RecognitionRequest.Open(3);
        Assert.Equal(RecognitionMode.Open, open.Mode);
        Assert.Empty(open.GrammarPhrases);
        Assert.Equal(3, open.MaxAlternatives);
        Assert.Equal(RecognitionRequest.Open(), RecognitionRequest.Open(5));
        Assert.Equal(RecognitionRequest.Constrained(["a"]), RecognitionRequest.Constrained(["a"]));
    }

    [Fact]
    public void PhonePosteriorsValidatesLengthAndIndexesRowMajor()
    {
        string[] vocabulary = ["a", "b", "c"];
        float[] logProbs = [0f, 1f, 2f, 10f, 11f, 12f];
        var posteriors = new PhonePosteriors(vocabulary, logProbs, 2, 0.02);
        Assert.Equal(0f, posteriors[0, 0]);
        Assert.Equal(2f, posteriors[0, 2]);
        Assert.Equal(11f, posteriors[1, 1]);
        Assert.Equal([10f, 11f, 12f], posteriors.Frame(1).ToArray());
        Assert.Equal(3, posteriors.PhoneCount);
        Assert.Throws<ArgumentOutOfRangeException>(() => posteriors[2, 0]);
        Assert.Throws<ArgumentOutOfRangeException>(() => posteriors[0, 3]);
        Assert.Throws<ArgumentException>(() => new PhonePosteriors(vocabulary, logProbs, 3, 0.02));
        Assert.Throws<ArgumentException>(() => new PhonePosteriors(vocabulary, [0f], 2, 0.02));
        Assert.Equal(posteriors, new PhonePosteriors(vocabulary, [0f, 1f, 2f, 10f, 11f, 12f], 2, 0.02));
    }

    [Fact]
    public void EmptyPosteriorsAreAllowed()
    {
        var posteriors = new PhonePosteriors(["a"], [], 0, 0.02);
        Assert.Equal(0, posteriors.FrameCount);
    }

    [Fact]
    public void PromptSpecWithTextRoundTripsAndTextIsPartOfEquality()
    {
        var spec = new PromptSpec("p1", RecognitionMode.Open, Difficulty.Beginner, "answers-1", ["greet"], "villager", [], "¡Hola! ¿Cómo estás?");
        var json = CanonicalJson.Serialize(spec);
        Assert.EndsWith(",\"text\":\"¡Hola! ¿Cómo estás?\"}", json, StringComparison.Ordinal);
        var parsed = CanonicalJson.Deserialize<PromptSpec>(json);
        Assert.Equal(spec, parsed);
        Assert.Equal(spec.GetHashCode(), parsed.GetHashCode());
        Assert.Equal(json, CanonicalJson.Serialize(parsed));

        var other = spec with { Text = "Buenos días." };
        Assert.NotEqual(spec, other);
        Assert.NotEqual(spec.GetHashCode(), other.GetHashCode());
        Assert.Null(new PromptSpec("p1", RecognitionMode.Open, Difficulty.Beginner, "answers-1", ["greet"], "villager", []).Text);
    }

    [Fact]
    public void PromptSpecRoundTripsThroughCanonicalJson()
    {
        var spec = new PromptSpec("p1", RecognitionMode.Constrained, Difficulty.Beginner, "answers-1", ["greet", "offer_item"], null, ["qty"]);
        var json = CanonicalJson.Serialize(spec);
        Assert.Equal(
            "{\"promptId\":\"p1\",\"mode\":\"Constrained\",\"difficulty\":\"Beginner\",\"answerSetId\":\"answers-1\",\"allowedIntentIds\":[\"greet\",\"offer_item\"],\"agentId\":null,\"slotGrammarIds\":[\"qty\"]}",
            json);
        var parsed = CanonicalJson.Deserialize<PromptSpec>(json);
        Assert.Equal(spec, parsed);
        Assert.Equal(json, CanonicalJson.Serialize(parsed));
    }

    [Fact]
    public void StageRecordsCompareListsByValue()
    {
        var token = new Token("hola", new TextSpan(0, 4), "Hola");
        Assert.Equal(new NormalizedText("hola", [token]), new NormalizedText("hola", [token]));
        var reading = new LexicalFeatures("hola", "intj", null, null, null, null, null);
        Assert.Equal(new AnalyzedToken(token, [reading]), new AnalyzedToken(token, [reading]));
        Assert.Equal(
            new AsrResult("hola", [new AsrHypothesis("hola", -1.0)], [new AsrWord("hola", 0.0, 0.4, 0.9)], 0.9, "m", RecognitionMode.Open),
            new AsrResult("hola", [new AsrHypothesis("hola", -1.0)], [new AsrWord("hola", 0.0, 0.4, 0.9)], 0.9, "m", RecognitionMode.Open));
        Assert.Equal(new SynthesisRequest("hola", "v", ["o", "l", "a"]), new SynthesisRequest("hola", "v", ["o", "l", "a"]));
        Assert.Equal(new SynthesisRequest("hola", "v", null), new SynthesisRequest("hola", "v", null));
        Assert.NotEqual(new SynthesisRequest("hola", "v", null), new SynthesisRequest("hola", "v", []));
        Assert.Equal(
            new IntentResolution(IntentId.Unknown, SlotMap.Empty.With("a", "1"), IntentRung.Rules, 0.2, 0.6),
            new IntentResolution(IntentId.Unknown, SlotMap.Empty.With("a", "1"), IntentRung.Rules, 0.2, 0.6));
    }
}
