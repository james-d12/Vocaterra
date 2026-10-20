using SherpaOnnx;
using Slg.Asr.Models;
using Slg.Core.Audio;
using Slg.Core.Prompts;
using Slg.Core.Stages;

namespace Slg.Asr;

/// <summary>Settings of <see cref="SherpaOnnxOfflineRecognizer"/>; the defaults match the Parakeet TDT 0.6B v3 int8 release.</summary>
public sealed record SherpaOptions
{
    /// <summary>The default options.</summary>
    public static SherpaOptions Default { get; } = new();

    /// <summary>Transducer encoder file name inside the model directory.</summary>
    public string EncoderFile { get; init; } = "encoder.int8.onnx";

    /// <summary>Transducer decoder file name inside the model directory.</summary>
    public string DecoderFile { get; init; } = "decoder.int8.onnx";

    /// <summary>Transducer joiner file name inside the model directory.</summary>
    public string JoinerFile { get; init; } = "joiner.int8.onnx";

    /// <summary>Token table file name inside the model directory.</summary>
    public string TokensFile { get; init; } = "tokens.txt";

    /// <summary>
    /// The model hash to report, normally <see cref="ModelStore.ModelHash"/> of the manifest entry. When null the recognizer hashes
    /// the four model files with <see cref="ModelStore.HashFiles"/>.
    /// </summary>
    public string? ModelHash { get; init; }
}

/// <summary>
/// Open-vocabulary recognizer over sherpa-onnx for the NeMo Parakeet TDT transducer: greedy search, one thread, CPU provider.
/// It has no grammar support, so a constrained request is answered with an open-mode result (its <see cref="AsrResult.Mode"/> is
/// <see cref="RecognitionMode.Open"/>). Per-word and overall confidence are 1.0 because sherpa-onnx exposes no token probabilities.
/// </summary>
public sealed class SherpaOnnxOfflineRecognizer : IWordRecognizer, IDisposable
{
    /// <summary>The only sample rate this recognizer accepts, in hertz.</summary>
    public const int SampleRate = PcmClip.DefaultSampleRate;

    private readonly OfflineRecognizer _recognizer;
    private readonly object _gate = new();
    private bool _disposed;

    /// <summary>Loads the model in <paramref name="modelDir"/>.</summary>
    /// <exception cref="FileNotFoundException">A required model file is missing.</exception>
    public SherpaOnnxOfflineRecognizer(string modelDir, SherpaOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(modelDir);
        var settings = options ?? SherpaOptions.Default;
        var config = BuildConfig(modelDir, settings);
        foreach (var file in new[]
        {
            config.ModelConfig.Transducer.Encoder,
            config.ModelConfig.Transducer.Decoder,
            config.ModelConfig.Transducer.Joiner,
            config.ModelConfig.Tokens,
        })
        {
            if (!File.Exists(file))
            {
                throw new FileNotFoundException($"sherpa-onnx model file '{file}' does not exist.", file);
            }
        }

        ModelHash = settings.ModelHash ?? ModelStore.HashFiles(
            [config.ModelConfig.Transducer.Encoder, config.ModelConfig.Transducer.Decoder, config.ModelConfig.Transducer.Joiner, config.ModelConfig.Tokens]);
        _recognizer = new OfflineRecognizer(config);
    }

    /// <inheritdoc />
    public string ModelHash { get; }

    /// <summary>Creates a recognizer for the manifest entry <paramref name="id"/>, using the manifest's model hash.</summary>
    public static SherpaOnnxOfflineRecognizer FromStore(ModelStore store, string id)
    {
        ArgumentNullException.ThrowIfNull(store);
        return new SherpaOnnxOfflineRecognizer(store.ModelDirectory(id), new SherpaOptions { ModelHash = store.ModelHash(id) });
    }

    /// <inheritdoc />
    public AsrResult Recognize(PcmClip clip, RecognitionRequest request)
    {
        ArgumentNullException.ThrowIfNull(clip);
        ArgumentNullException.ThrowIfNull(request);
        if (clip.SampleRate != SampleRate)
        {
            throw new ArgumentException($"The model needs {SampleRate} Hz audio but the clip is {clip.SampleRate} Hz.", nameof(clip));
        }

        if (clip.Samples.Length == 0)
        {
            return new AsrResult(string.Empty, [new AsrHypothesis(string.Empty, 1.0)], [], 1.0, ModelHash, RecognitionMode.Open);
        }

        var floats = new float[clip.Samples.Length];
        for (var i = 0; i < floats.Length; i++)
        {
            floats[i] = clip.Samples[i] / 32768f;
        }

        string text;
        string[] tokens;
        float[] timestamps;
        float[] durations;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            using var stream = _recognizer.CreateStream();
            stream.AcceptWaveform(SampleRate, floats);
            _recognizer.Decode(stream);
            var result = stream.Result;
            text = result.Text ?? string.Empty;
            tokens = result.Tokens ?? [];
            timestamps = result.Timestamps ?? [];
            durations = result.Durations ?? [];
        }

        var transcript = text.Trim();
        var words = SherpaWordAssembler.Assemble(tokens, timestamps, durations, clip.DurationSeconds);
        return new AsrResult(transcript, [new AsrHypothesis(transcript, 1.0)], words, 1.0, ModelHash, RecognitionMode.Open);
    }

    /// <summary>Releases the native recognizer.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (!_disposed)
            {
                _disposed = true;
                _recognizer.Dispose();
            }
        }
    }

    /// <summary>Builds the sherpa-onnx configuration: NeMo transducer, int8 files, greedy search, one thread, CPU.</summary>
    internal static OfflineRecognizerConfig BuildConfig(string modelDir, SherpaOptions options)
    {
        var config = new OfflineRecognizerConfig();
        config.FeatConfig.SampleRate = SampleRate;
        config.ModelConfig.Transducer.Encoder = Path.Combine(modelDir, options.EncoderFile);
        config.ModelConfig.Transducer.Decoder = Path.Combine(modelDir, options.DecoderFile);
        config.ModelConfig.Transducer.Joiner = Path.Combine(modelDir, options.JoinerFile);
        config.ModelConfig.Tokens = Path.Combine(modelDir, options.TokensFile);
        config.ModelConfig.NumThreads = 1;
        config.ModelConfig.Provider = "cpu";
        config.ModelConfig.ModelType = "nemo_transducer";
        config.ModelConfig.Debug = 0;
        config.DecodingMethod = "greedy_search";
        return config;
    }
}
