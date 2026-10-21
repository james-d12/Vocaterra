using System.Globalization;
using System.Security.Cryptography;
using SherpaOnnx;
using Vocaterra.Nlp.Asr.Models;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Stages;

namespace Vocaterra.Nlp.Response.Speech;

/// <summary>
/// Kokoro text-to-speech through sherpa-onnx (D1 adapter, CPU, one thread) with a fixed speaker and speed, so equal text gives equal audio.
/// The engine is created and disposed inside each <see cref="Synthesize"/> call (see the comment there). One instance speaks with one voice: <see cref="SynthesisRequest.VoiceId"/> is
/// only a label (it is part of cache keys) and does not select the speaker.
/// </summary>
/// <remarks>
/// Speaker ids of the Spanish voices in <c>kokoro-multi-lang-v1_0</c> (read from the <c>speaker2id</c> metadata of its <c>model.onnx</c>):
/// <c>28</c> is <c>ef_dora</c> (female), <c>29</c> is <c>em_alex</c> (male) and <c>53</c> is <c>em_santa</c> (male). The adapter exposes
/// speakers by number only; the names are documented here, not looked up at run time. The model has 54 speakers (ids 0 to 53) at 24000 Hz.
/// </remarks>
/// <remarks>
/// TODO(ADR): Kokoro multi-lang in sherpa-onnx phonemizes with bundled espeak-ng data (GPL-3.0); plan §1 requires replacing it with Vocaterra.Nlp.Phonology
/// phones before shipping. Until then <see cref="SynthesisRequest.Phones"/> is accepted and ignored.
/// </remarks>
public sealed class KokoroSynthesizer : ISpeechSynthesizer, IDisposable
{
    /// <summary>Name of the model directory inside the models directory (<c>VOCATERRA_MODELS_DIR</c>).</summary>
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

    /// <summary>
    /// Creates the adapter for the manifest entry <paramref name="id"/> (normally <see cref="ModelDirectoryName"/>) under the store's root,
    /// with the manifest's model hash. Like <c>SherpaOnnxOfflineRecognizer.FromStore</c> it trusts the manifest hash and does not re-hash the
    /// files; call <see cref="ModelStore.Verify"/> to check them. The manifest must pin the files the adapter loads.
    /// </summary>
    /// <param name="store">The models store (root directory and manifest).</param>
    /// <param name="id">The manifest id of the model.</param>
    /// <param name="speakerId">The speaker id (see the remarks of this class for the Spanish voices).</param>
    /// <param name="speed">The speaking speed, a positive finite number.</param>
    /// <exception cref="KeyNotFoundException">The id is not in the manifest.</exception>
    /// <exception cref="InvalidOperationException">The entry lacks a file the adapter loads, or a file has no pinned SHA-256.</exception>
    /// <exception cref="FileNotFoundException">A required file is missing from the model directory.</exception>
    public static KokoroSynthesizer FromStore(ModelStore store, string id, int speakerId, float speed = 1.0f)
    {
        ArgumentNullException.ThrowIfNull(store);
        var entry = store.Manifest.Find(id) ?? throw new KeyNotFoundException($"Model '{id}' is not in the manifest.");
        foreach (var required in new[] { ModelFile, VoicesFile, TokensFile })
        {
            if (!entry.Files.Any(f => string.Equals(f.Path, required, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException($"The manifest entry '{id}' does not list '{required}', which the Kokoro adapter loads.");
            }
        }

        if (!entry.Files.Any(static f => f.Path.StartsWith(DataDirectory + "/", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"The manifest entry '{id}' does not list any file under '{DataDirectory}/', which the Kokoro adapter loads.");
        }

        return new KokoroSynthesizer(store.ModelDirectory(id), speakerId, speed, store.ModelHash(id));
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

            // A new engine per call: sherpa-onnx's Kokoro keeps state between calls, so a reused engine does not repeat its output (measured:
            // the second call on one engine differed from the first for the same text, while every fresh engine reproduced the first output).
            // The cost is one model load per call; callers that repeat lines should wrap this in CachedSynthesizer.
            using var engine = CreateEngine();

            // TODO(ADR): Kokoro multi-lang in sherpa-onnx phonemizes with bundled espeak-ng data (GPL-3.0); plan §1 requires replacing it with Vocaterra.Nlp.Phonology phones before shipping
            // request.Phones is therefore a no-op for now.
            var audio = engine.Generate(request.Text, _speed, _speakerId);
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

    /// <summary>Marks the adapter as disposed; later calls to <see cref="Synthesize"/> throw. No native resource is held between calls.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
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
