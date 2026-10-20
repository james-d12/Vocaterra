using System.Globalization;
using System.Security.Cryptography;
using SherpaOnnx;
using Slg.Core.Audio;
using Slg.Core.Determinism;
using Slg.Core.Stages;

namespace Slg.Response.Speech;

/// <summary>
/// Kokoro text-to-speech through sherpa-onnx (D1 adapter, CPU, one thread) with a fixed speaker and speed, so equal text gives equal audio.
/// The engine is created on the first <see cref="Synthesize"/> call. One instance speaks with one voice: <see cref="SynthesisRequest.VoiceId"/> is
/// only a label (it is part of cache keys) and does not select the speaker.
/// </summary>
/// <remarks>
/// TODO(ADR): Kokoro multi-lang in sherpa-onnx phonemizes with bundled espeak-ng data (GPL-3.0); plan §1 requires replacing it with Slg.Phonology
/// phones before shipping. Until then <see cref="SynthesisRequest.Phones"/> is accepted and ignored.
/// </remarks>
public sealed class KokoroSynthesizer : ISpeechSynthesizer, IDisposable
{
    /// <summary>Name of the model directory inside the models directory (<c>SLG_MODELS_DIR</c>).</summary>
    public const string ModelDirectoryName = "kokoro-multi-lang-v1_0";

    private const string ModelFile = "model.onnx";
    private const string VoicesFile = "voices.bin";
    private const string TokensFile = "tokens.txt";
    private const string DataDirectory = "espeak-ng-data";
    private const string SpanishVoiceLanguage = "es";

    private readonly string _modelDirectory;
    private readonly int _speakerId;
    private readonly float _speed;
    private readonly Lazy<string> _modelHash;
    private readonly object _gate = new();
    private OfflineTts? _engine;
    private bool _disposed;

    /// <summary>
    /// Creates the adapter for a model directory (as unpacked from <c>kokoro-multi-lang-v1_0</c>). The model files must exist.
    /// <paramref name="modelHash"/> is the manifest hash of the model; when null the files are hashed on first use of <see cref="ModelHash"/>.
    /// </summary>
    public KokoroSynthesizer(string modelDir, int speakerId, float speed = 1.0f, string? modelHash = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelDir);
        ArgumentOutOfRangeException.ThrowIfNegative(speakerId);
        if (!float.IsFinite(speed) || speed <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(speed), speed, "Speed must be a positive finite number.");
        }

        foreach (var file in new[] { ModelFile, VoicesFile, TokensFile })
        {
            if (!File.Exists(Path.Combine(modelDir, file)))
            {
                throw new FileNotFoundException($"Kokoro model file '{file}' not found in '{modelDir}'.", Path.Combine(modelDir, file));
            }
        }

        if (!Directory.Exists(Path.Combine(modelDir, DataDirectory)))
        {
            throw new DirectoryNotFoundException($"Kokoro data directory '{DataDirectory}' not found in '{modelDir}'.");
        }

        _modelDirectory = modelDir;
        _speakerId = speakerId;
        _speed = speed;
        _modelHash = new Lazy<string>(() => ComputeModelHash(modelHash), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>Hash over the model, voices and tokens files (or the supplied manifest hash), the speaker id and the speed.</summary>
    public string ModelHash => _modelHash.Value;

    /// <summary>Speaks the request text with the configured speaker and speed; the sample rate comes from the model.</summary>
    public AudioBuffer Synthesize(SynthesisRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _engine ??= CreateEngine();

            // TODO(ADR): Kokoro multi-lang in sherpa-onnx phonemizes with bundled espeak-ng data (GPL-3.0); plan §1 requires replacing it with Slg.Phonology phones before shipping
            // request.Phones is therefore a no-op for now.
            var audio = _engine.Generate(request.Text, _speed, _speakerId);
            try
            {
                return new AudioBuffer(ToPcm16(audio.Samples), audio.SampleRate);
            }
            finally
            {
                audio.Dispose();
            }
        }
    }

    /// <summary>Releases the native engine.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _engine?.Dispose();
            _engine = null;
        }
    }

    private static short[] ToPcm16(float[] samples)
    {
        var result = new short[samples.Length];
        for (var i = 0; i < samples.Length; i++)
        {
            var scaled = MathF.Round(samples[i] * 32767f, MidpointRounding.AwayFromZero);
            result[i] = (short)Math.Clamp(scaled, short.MinValue, short.MaxValue);
        }

        return result;
    }

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    private OfflineTts CreateEngine()
    {
        var config = new OfflineTtsConfig();
        config.Model.Kokoro.Model = Path.Combine(_modelDirectory, ModelFile);
        config.Model.Kokoro.Voices = Path.Combine(_modelDirectory, VoicesFile);
        config.Model.Kokoro.Tokens = Path.Combine(_modelDirectory, TokensFile);
        config.Model.Kokoro.DataDir = Path.Combine(_modelDirectory, DataDirectory);
        config.Model.Kokoro.Lang = SpanishVoiceLanguage;
        config.Model.NumThreads = 1;
        config.Model.Provider = "cpu";
        config.Model.Debug = 0;
        return new OfflineTts(config);
    }

    private string ComputeModelHash(string? manifestHash)
    {
        var speaker = _speakerId.ToString(CultureInfo.InvariantCulture);
        var speed = _speed.ToString("R", CultureInfo.InvariantCulture);
        if (manifestHash is not null)
        {
            return DeterministicHash.Combine("kokoro", manifestHash, speaker, speed);
        }

        return DeterministicHash.Combine(
            "kokoro",
            HashFile(Path.Combine(_modelDirectory, ModelFile)),
            HashFile(Path.Combine(_modelDirectory, VoicesFile)),
            HashFile(Path.Combine(_modelDirectory, TokensFile)),
            speaker,
            speed);
    }
}
