using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;

namespace Vocaterra.Nlp.Asr.Tests;

/// <summary>Tests for the sherpa-onnx adapter that need no model, plus the model-gated Parakeet run.</summary>
public class SherpaTests
{
    private const char Marker = SherpaWordAssembler.WordMarker;

    [Fact]
    public void WordsAreJoinedOnTheWordMarkerWithTokenDurations()
    {
        string[] tokens = [Marker + "Hola", ",", Marker + "c", "ó", "mo", Marker + "est", "á", "s", "?"];
        float[] timestamps = [0.0f, 0.25f, 0.5f, 0.75f, 1.0f, 1.25f, 1.5f, 1.75f, 2.0f];
        float[] durations = [0.25f, 0.25f, 0.25f, 0.25f, 0.25f, 0.25f, 0.25f, 0.25f, 0.25f];

        var words = SherpaWordAssembler.Assemble(tokens, timestamps, durations, 3.0);

        Assert.Equal(
            [
                new AsrWord("Hola,", 0.0, 0.5, 1.0),
                new AsrWord("cómo", 0.5, 1.25, 1.0),
                new AsrWord("estás?", 1.25, 2.25, 1.0),
            ],
            words.ToArray());
    }

    [Fact]
    public void WithoutDurationsAWordEndsWhereTheNextOneStartsAndTheLastEndsAtTheClipEnd()
    {
        string[] tokens = [Marker + "dos", Marker + "es", "pa", "das"];
        float[] timestamps = [0.5f, 1.0f, 1.25f, 1.75f];

        var words = SherpaWordAssembler.Assemble(tokens, timestamps, [], 2.5);

        Assert.Equal([new AsrWord("dos", 0.5, 1.0, 1.0), new AsrWord("espadas", 1.0, 2.5, 1.0)], words.ToArray());
    }

    [Fact]
    public void MarkerOnlyTokensStartAWordButProduceNoEmptyWord()
    {
        string[] tokens = [Marker.ToString(), Marker + "a", Marker.ToString()];
        float[] timestamps = [0.0f, 0.5f, 1.0f];
        float[] durations = [0.5f, 0.5f, 0.5f];

        var words = SherpaWordAssembler.Assemble(tokens, timestamps, durations, 2.0);

        Assert.Equal([new AsrWord("a", 0.5, 1.0, 1.0)], words.ToArray());
    }

    [Fact]
    public void MissingTimestampsGiveZeroTimedWordsInOrder()
    {
        string[] tokens = [Marker + "uno", Marker + "dos"];

        var words = SherpaWordAssembler.Assemble(tokens, [], [], 0.0);

        Assert.Equal(["uno", "dos"], words.Select(w => w.Text).ToArray());
        Assert.All(words, w => Assert.Equal(0.0, w.StartSeconds));
    }

    [Fact]
    public void NoTokensGiveNoWords()
    {
        Assert.Empty(SherpaWordAssembler.Assemble([], [], [], 1.0));
    }

    [Fact]
    public void ConfigurationIsGreedySingleThreadedCpuNemoTransducer()
    {
        var dir = Path.Combine(Path.GetTempPath(), "parakeet");

        var config = SherpaOnnxOfflineRecognizer.BuildConfig(dir, SherpaOptions.Default);

        Assert.Equal("greedy_search", config.DecodingMethod);
        Assert.Equal(1, config.ModelConfig.NumThreads);
        Assert.Equal("cpu", config.ModelConfig.Provider);
        Assert.Equal("nemo_transducer", config.ModelConfig.ModelType);
        Assert.Equal(16000, config.FeatConfig.SampleRate);
        Assert.Equal(Path.Combine(dir, "encoder.int8.onnx"), config.ModelConfig.Transducer.Encoder);
        Assert.Equal(Path.Combine(dir, "decoder.int8.onnx"), config.ModelConfig.Transducer.Decoder);
        Assert.Equal(Path.Combine(dir, "joiner.int8.onnx"), config.ModelConfig.Transducer.Joiner);
        Assert.Equal(Path.Combine(dir, "tokens.txt"), config.ModelConfig.Tokens);
    }

    [Fact]
    public void ConstructingWithoutModelFilesThrowsBeforeLoadingAnything()
    {
        var empty = Directory.CreateTempSubdirectory("vocaterra-sherpa-empty-");
        try
        {
            Assert.Throws<FileNotFoundException>(() => new SherpaOnnxOfflineRecognizer(empty.FullName));
        }
        finally
        {
            empty.Delete(recursive: true);
        }
    }

    [ParakeetModelFact]
    public void ParakeetRecognizesSilenceDeterministically()
    {
        var store = ModelGate.Store!;
        using var recognizer = new SherpaOnnxOfflineRecognizer(store.ModelDirectory(ModelGate.ParakeetId));
        var clip = TestData.SilenceClip();

        var first = recognizer.Recognize(clip, RecognitionRequest.Open());
        var second = recognizer.Recognize(clip, RecognitionRequest.Open());

        Assert.Equal(first, second);
        Assert.Equal(RecognitionMode.Open, first.Mode);
        Assert.Equal(recognizer.ModelHash, first.ModelHash);
        Assert.Single(first.NBest);
    }
}
