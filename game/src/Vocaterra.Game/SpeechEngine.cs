using Vocaterra.Nlp.Asr;
using Vocaterra.Nlp.Asr.Models;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Response.Speech;

namespace Vocaterra.Game;

/// <summary>
/// The speech models the game runs, loaded from the models folder (<c>VOCATERRA_MODELS_DIR</c>, the core's <see cref="ModelStore"/>): Vosk
/// small Spanish for the player's voice, and Kokoro (speaker 28, <c>ef_dora</c>) behind the core's <see cref="CachedSynthesizer"/> for the
/// scripted <c>--speak</c> lines (and, in P8, the guide's voice).
/// </summary>
/// <remarks>
/// <b>Threads.</b> The recognizer loads in its constructor, is not safe for concurrent calls and must not be disposed while a call may run
/// (core API section 8). A <see cref="SpeechEngine"/> is therefore created, used and disposed by one thread, the game's turn worker
/// (<see cref="TurnWorker"/>). Kokoro loads an engine for every line it speaks (about 1.5 s for a short line), so every line goes through the
/// cache: the cache is a folder of WAV files under <c>~/.cache/vocaterra/tts</c>, nothing of it is part of the repository.
/// </remarks>
public sealed class SpeechEngine : IDisposable
{
    /// <summary>The Vosk model's id in the models folder.</summary>
    public const string VoskModelId = "vosk-model-small-es-0.42";

    /// <summary>The Kokoro model's id in the models folder.</summary>
    public const string KokoroModelId = "kokoro-multi-lang-v1_0";

    /// <summary>Kokoro's Spanish female voice, <c>ef_dora</c> (core API section 6).</summary>
    public const int KokoroSpeaker = 28;

    /// <summary>The voice label that goes into the cache key.</summary>
    public const string VoiceId = "ef_dora";

    private readonly ModelStore _store;
    private readonly TimedRecognizer _recognizer;
    private readonly string _cacheDirectory;
    private CachedSynthesizer? _synthesizer;
    private KokoroSynthesizer? _kokoro;
    private bool _disposed;

    private SpeechEngine(ModelStore store, VoskWordRecognizer vosk, Func<double>? clock, string cacheDirectory)
    {
        _store = store;
        _recognizer = new TimedRecognizer(vosk, clock);
        _cacheDirectory = cacheDirectory;
    }

    /// <summary>The recognizer, wrapped so that it records how long its last decode took (when a clock was given). Call it from one thread.</summary>
    public IWordRecognizer Recognizer => _recognizer;

    /// <summary>How long the last decode took, in milliseconds (zero without a clock or before the first decode).</summary>
    public double LastRecognizeMilliseconds => _recognizer.LastMilliseconds;

    /// <summary>Whether the Kokoro model is in the models folder (a missing model only disables <see cref="Synthesize"/>).</summary>
    public bool CanSpeak => Directory.Exists(_store.ModelDirectory(KokoroModelId));

    /// <summary>The folder of the synthesis cache.</summary>
    public string CacheDirectory => _cacheDirectory;

    /// <summary>The default cache folder, <c>$XDG_CACHE_HOME/vocaterra/tts</c> or <c>~/.cache/vocaterra/tts</c>.</summary>
    public static string DefaultCacheDirectory()
    {
        var root = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
        if (string.IsNullOrWhiteSpace(root))
        {
            root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache");
        }

        return Path.Combine(root, "vocaterra", "tts");
    }

    /// <summary>
    /// The models store named by <c>VOCATERRA_MODELS_DIR</c> and the Vosk model inside it, or the reason there is none. A cheap check: nothing is
    /// loaded.
    /// </summary>
    /// <param name="problem">When the result is <see langword="null"/>, why (a message for the player, in Spanish).</param>
    public static ModelStore? FindModels(out string? problem)
    {
        ModelStore? store;
        try
        {
            var root = ModelStore.RootFromEnvironment();
            if (root is null)
            {
                problem = $"Falta la variable {ModelStore.EnvironmentVariable}: no hay carpeta de modelos de voz.";
                return null;
            }

            var manifest = ManifestPath(root);
            store = manifest is null ? ModelStore.FromEnvironment() : new ModelStore(root, ModelManifest.Load(manifest));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException)
        {
            problem = $"No se puede leer la carpeta de modelos: {ex.Message}";
            return null;
        }

        if (store is null)
        {
            problem = $"Falta la variable {ModelStore.EnvironmentVariable}: no hay carpeta de modelos de voz.";
            return null;
        }

        if (!Directory.Exists(store.ModelDirectory(VoskModelId)))
        {
            problem = $"Falta el modelo {VoskModelId} en {store.RootDirectory}.";
            return null;
        }

        if (store.Manifest.Find(VoskModelId) is null)
        {
            problem = $"El manifiesto de modelos no lista {VoskModelId} (ponga manifest.json en {store.RootDirectory} o la variable {ManifestVariable}).";
            return null;
        }

        problem = null;
        return store;
    }

    /// <summary>The environment variable that names a manifest file to use when the models folder holds none.</summary>
    public const string ManifestVariable = "VOCATERRA_MODELS_MANIFEST";

    /// <summary>
    /// The manifest to read, when it is not <c>manifest.json</c> in the models folder (where the core's <see cref="ModelStore"/> looks, and
    /// where the core's fetch script does not put it: CR-019): the file named by <see cref="ManifestVariable"/>, else the core submodule's
    /// <c>models/manifest.json</c> found by walking up from the executable's folder and the current directory. <see langword="null"/> means
    /// the store reads the folder's own manifest.
    /// </summary>
    public static string? ManifestPath(string modelsRoot)
    {
        if (File.Exists(Path.Combine(modelsRoot, ModelStore.ManifestFileName)))
        {
            return null;
        }

        var named = Environment.GetEnvironmentVariable(ManifestVariable);
        if (!string.IsNullOrWhiteSpace(named))
        {
            return File.Exists(named) ? named : null;
        }

        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            {
                var candidate = Path.Combine(directory.FullName, "external", "Vocaterra.NLP", "models", ModelStore.ManifestFileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Loads the Vosk model from the models folder (a second or two) and returns the engine, or <see langword="null"/> with the reason when
    /// there is no models folder or no Vosk model. Call it on the thread that will use the engine.
    /// </summary>
    /// <param name="problem">When the result is <see langword="null"/>, why (a message for the player, in Spanish).</param>
    /// <param name="clockMilliseconds">A clock for the timings only (the front-end's); nothing in an evaluation reads it.</param>
    /// <param name="cacheDirectory">The synthesis cache folder, or <see langword="null"/> for <see cref="DefaultCacheDirectory"/>.</param>
    public static SpeechEngine? TryCreate(out string? problem, Func<double>? clockMilliseconds = null, string? cacheDirectory = null)
    {
        var store = FindModels(out problem);
        if (store is null)
        {
            return null;
        }

        try
        {
            var vosk = VoskWordRecognizer.FromStore(store, VoskModelId);
            problem = null;
            return new SpeechEngine(store, vosk, clockMilliseconds, cacheDirectory ?? DefaultCacheDirectory());
        }
        catch (Exception ex) when (ex is IOException or DllNotFoundException or BadImageFormatException or InvalidOperationException or KeyNotFoundException or TypeInitializationException)
        {
            problem = $"No se pudo cargar el reconocedor de voz: {ex.Message}";
            return null;
        }
    }

    /// <summary>
    /// Speaks <paramref name="text"/> with Kokoro (through the cache) and returns it as a clip ready for the recognizer: 24 kHz audio converted
    /// to 16 kHz, silence trimmed. A line that was spoken before comes from the cache without loading Kokoro.
    /// </summary>
    /// <param name="text">The line to speak.</param>
    /// <param name="cached">Whether the audio came from the cache.</param>
    /// <exception cref="InvalidOperationException">The Kokoro model is not in the models folder.</exception>
    public PcmClip Synthesize(string text, out bool cached)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_synthesizer is null)
        {
            if (!CanSpeak)
            {
                throw new InvalidOperationException($"Falta el modelo {KokoroModelId} en {_store.RootDirectory}.");
            }

            _kokoro = KokoroSynthesizer.FromStore(_store, KokoroModelId, KokoroSpeaker);
            _synthesizer = new CachedSynthesizer(_kokoro, new DirectorySynthesisCache(_cacheDirectory));
        }

        var before = _synthesizer.Hits;
        var audio = _synthesizer.Synthesize(new SynthesisRequest(text, VoiceId, null));
        cached = _synthesizer.Hits > before;
        return AudioClips.Prepare(audio.Samples, audio.SampleRate)
            ?? throw new InvalidOperationException("The synthesized line is silent.");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _recognizer.Dispose();
        _kokoro?.Dispose();
    }

    /// <summary>Records how long the last decode took; everything else is the inner recognizer.</summary>
    private sealed class TimedRecognizer(VoskWordRecognizer inner, Func<double>? clock) : IWordRecognizer, IDisposable
    {
        public string ModelHash => inner.ModelHash;

        public double LastMilliseconds { get; private set; }

        public AsrResult Recognize(PcmClip clip, RecognitionRequest request)
        {
            var start = clock?.Invoke() ?? 0;
            var result = inner.Recognize(clip, request);
            LastMilliseconds = clock is null ? 0 : clock() - start;
            return result;
        }

        public void Dispose() => inner.Dispose();
    }
}
