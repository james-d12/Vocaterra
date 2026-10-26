using System.Security.Cryptography;
using Vocaterra.Nlp.Core.Audio;

namespace Vocaterra.Nlp.Pronunciation.Tests;

/// <summary>
/// Exercises the ONNX adapter end to end with a tiny synthetic model: file loading, normalization, session options, log-softmax,
/// label mapping and merging, model hashing. The real 1 GB model is covered by the gated tests.
/// </summary>
[Collection(NativeRuntimeCollection.Name)]
public sealed class Wav2Vec2AdapterTests : IDisposable
{
    private const string VocabularyJson = """{"<pad>":0,"<s>":1,"a":2,"ɑ":3,"ɡ":4,"g":5,"ʔ":6,"o":7}""";

    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("vocaterra-w2v-");
    private readonly byte[] modelBytes = TinyOnnxModel.Build(vocabularySize: 8);

    public Wav2Vec2AdapterTests()
    {
        File.WriteAllBytes(Path.Combine(directory.FullName, "model.onnx"), modelBytes);
        File.WriteAllText(Path.Combine(directory.FullName, "vocab.json"), VocabularyJson);
    }

    public void Dispose()
    {
        directory.Delete(recursive: true);
    }

    private static PcmClip Tone(int samples, int amplitude = 5000)
    {
        var data = new short[samples];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = (short)(((i % 50) < 25 ? amplitude : -amplitude) + ((i % 11) * 37));
        }

        return PcmClip.FromSamples(data);
    }

    [Fact]
    public void RecognizeReturnsNormalizedMappedAndMergedPosteriors()
    {
        using var recognizer = new Wav2Vec2PhonemeRecognizer(directory.FullName);

        var posteriors = recognizer.Recognize(Tone(16000));

        // 49 frames: floor((16000 - 400) / 320) + 1.
        Assert.Equal(49, posteriors.FrameCount);
        Assert.Equal(0.02, posteriors.FrameSeconds);

        // <pad> -> <blank>; the two labels for a and for g merge; the unknown label stays.
        Assert.Equal(["<blank>", "<s>", "a", "g", "ʔ", "o"], posteriors.Vocabulary);
        Assert.Equal(recognizer.Vocabulary, posteriors.Vocabulary);
        Assert.Equal(["ʔ"], recognizer.UnmappedLabels);

        for (var frame = 0; frame < posteriors.FrameCount; frame++)
        {
            var mass = 0.0;
            foreach (var value in posteriors.Frame(frame))
            {
                Assert.True(value <= 0f);
                mass += Math.Exp(value);
            }

            Assert.Equal(1.0, mass, 4);
        }
    }

    [Fact]
    public void RecognizeIsRepeatableAcrossCallsAndInstances()
    {
        using var first = new Wav2Vec2PhonemeRecognizer(directory.FullName);
        using var second = new Wav2Vec2PhonemeRecognizer(directory.FullName);
        var clip = Tone(8000);

        var a = first.Recognize(clip);

        Assert.Equal(a, first.Recognize(clip));
        Assert.Equal(a, second.Recognize(clip));
        Assert.Equal(first.ModelHash, second.ModelHash);
    }

    [Fact]
    public void DifferentAudioGivesDifferentPosteriors()
    {
        using var recognizer = new Wav2Vec2PhonemeRecognizer(directory.FullName);

        Assert.NotEqual(recognizer.Recognize(Tone(8000, 5000)), recognizer.Recognize(Tone(8000, 9000)));
    }

    [Fact]
    public void ModelHashIsTheSha256OfTheModelFile()
    {
        using var recognizer = new Wav2Vec2PhonemeRecognizer(directory.FullName);

        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(modelBytes)), recognizer.ModelHash);
    }

    [Fact]
    public void PosteriorsFeedTheScorersEndToEnd()
    {
        using var recognizer = new Wav2Vec2PhonemeRecognizer(directory.FullName);
        var posteriors = recognizer.Recognize(Tone(16000));
        var variants = TestSupport.Primary("a");

        foreach (var mode in Enum.GetValues<ScoringMode>())
        {
            var result = new PronunciationScorer().Score(
                posteriors,
                variants,
                [],
                Vocaterra.Nlp.Core.Prompts.Difficulty.Beginner,
                mode,
                recognizer.ModelHash);

            Assert.Single(result.Phones);
            Assert.Equal(recognizer.ModelHash, result.ModelHash);
            Assert.InRange(result.WordScore, 0.0, 1.0);
            Assert.InRange(result.Confidence, 0.0, 1.0);
        }
    }

    [Fact]
    public void ClipsAtTheWrongRateOrTooShortAreHandled()
    {
        using var recognizer = new Wav2Vec2PhonemeRecognizer(directory.FullName);

        Assert.Throws<ArgumentException>(() => recognizer.Recognize(PcmClip.FromSamples(new short[16000], 8000)));

        var tooShort = recognizer.Recognize(Tone(Wav2Vec2PhonemeRecognizer.MinimumSamples - 1));
        Assert.Equal(0, tooShort.FrameCount);
        Assert.Equal(recognizer.Vocabulary, tooShort.Vocabulary);

        var shortest = recognizer.Recognize(Tone(Wav2Vec2PhonemeRecognizer.MinimumSamples));
        Assert.Equal(1, shortest.FrameCount);
    }

    [Fact]
    public void MissingVocabularyOrPaddingTokenFailsFast()
    {
        File.Delete(Path.Combine(directory.FullName, "vocab.json"));
        Assert.Throws<FileNotFoundException>(() => new Wav2Vec2PhonemeRecognizer(directory.FullName));

        File.WriteAllText(Path.Combine(directory.FullName, "vocab.json"), """{"a":0,"o":1}""");
        Assert.Throws<ArgumentException>(() => new Wav2Vec2PhonemeRecognizer(directory.FullName));
    }

    [Fact]
    public void AVocabularyThatDisagreesWithTheModelOutputIsReported()
    {
        // The model emits 8 logits per frame but this vocabulary has 3 labels.
        File.WriteAllText(Path.Combine(directory.FullName, "vocab.json"), """{"<pad>":0,"a":1,"o":2}""");
        using var recognizer = new Wav2Vec2PhonemeRecognizer(directory.FullName);

        Assert.Throws<InvalidOperationException>(() => recognizer.Recognize(Tone(1600)));
    }
}
