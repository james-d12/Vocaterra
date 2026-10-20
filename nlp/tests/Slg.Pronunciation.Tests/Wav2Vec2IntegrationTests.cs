using Slg.Core.Audio;
using Slg.Core.Stages;

namespace Slg.Pronunciation.Tests;

/// <summary>Locates the real wav2vec2 model through <c>SLG_MODELS_DIR</c>.</summary>
internal static class ModelLocation
{
    public const string EnvironmentVariable = "SLG_MODELS_DIR";
    public const string ModelFolder = "wav2vec2-xlsr-53-espeak-cv-ft";

    public static string? Directory
    {
        get
        {
            var root = Environment.GetEnvironmentVariable(EnvironmentVariable);
            if (string.IsNullOrWhiteSpace(root))
            {
                return null;
            }

            var directory = Path.Combine(root, ModelFolder);
            return File.Exists(Path.Combine(directory, Wav2Vec2PhonemeRecognizer.ModelFileName))
                && File.Exists(Path.Combine(directory, Wav2Vec2PhonemeRecognizer.VocabFileName))
                ? directory
                : null;
        }
    }
}

/// <summary>A fact that is skipped, not failed, when the wav2vec2 model files are not available.</summary>
public sealed class Wav2Vec2ModelFactAttribute : FactAttribute
{
    /// <summary>Creates the attribute and sets <see cref="FactAttribute.Skip"/> when the model is absent.</summary>
    public Wav2Vec2ModelFactAttribute()
    {
        if (ModelLocation.Directory is null)
        {
            Skip = $"Set {ModelLocation.EnvironmentVariable} to a folder containing {ModelLocation.ModelFolder}/model.onnx and vocab.json to run this test.";
        }
    }
}

/// <summary>Integration tests for the ONNX adapter; they run only when the model has been fetched.</summary>
public class Wav2Vec2IntegrationTests
{
    private static PcmClip SyntheticClip()
    {
        // One second of a deterministic integer-valued tone burst, with no random source.
        var samples = new short[PcmClip.DefaultSampleRate];
        for (var i = 0; i < samples.Length; i++)
        {
            var phase = i % 80;
            samples[i] = (short)((phase < 40 ? 6000 : -6000) + ((i % 7) * 100));
        }

        return PcmClip.FromSamples(samples);
    }

    [Wav2Vec2ModelFact]
    public void TheRealModelProducesStableLogProbabilities()
    {
        using var recognizer = new Wav2Vec2PhonemeRecognizer(ModelLocation.Directory!);
        var clip = SyntheticClip();

        var first = recognizer.Recognize(clip);
        var second = recognizer.Recognize(clip);

        Assert.Equal(first, second);
        Assert.Equal(64, recognizer.ModelHash.Length);
        Assert.Equal(0.02, first.FrameSeconds);
        Assert.Contains(CtcDecoder.BlankSymbol, first.Vocabulary);
        Assert.InRange(first.FrameCount, 45, 51);
        Assert.Equal(first.Vocabulary.Count, first.Vocabulary.Distinct().Count());
        Assert.Equal(recognizer.UnmappedLabels.OrderBy(static l => l, StringComparer.Ordinal), recognizer.UnmappedLabels);

        for (var frame = 0; frame < first.FrameCount; frame++)
        {
            var mass = 0.0;
            foreach (var value in first.Frame(frame))
            {
                mass += Math.Exp(value);
            }

            Assert.InRange(mass, 0.999, 1.001);
        }
    }

    [Wav2Vec2ModelFact]
    public void TheRealModelDecodesWithoutThrowingOnAShortClip()
    {
        using var recognizer = new Wav2Vec2PhonemeRecognizer(ModelLocation.Directory!);

        var tooShort = recognizer.Recognize(PcmClip.FromSamples(new short[100]));

        Assert.Equal(0, tooShort.FrameCount);
        Assert.Empty(CtcDecoder.Greedy(tooShort));
    }

    [Fact]
    public void WithoutTheModelTheGatedTestsAreSkippedNotFailed()
    {
        var attribute = new Wav2Vec2ModelFactAttribute();

        Assert.Equal(ModelLocation.Directory is null, attribute.Skip is not null);
    }
}
