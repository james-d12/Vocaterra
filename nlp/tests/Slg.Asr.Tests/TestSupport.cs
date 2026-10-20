using Slg.Asr.Models;
using Slg.Audio;
using Slg.Core.Audio;
using Slg.Core.Prompts;
using Slg.Core.Stages;

namespace Slg.Asr.Tests;

/// <summary>Builders for clips and results shared by the tests.</summary>
internal static class TestData
{
    public static PcmClip SilenceClip(double seconds = 2.0)
    {
        // Goes through a real WAV file so the Slg.Audio reading path is exercised, as for recorded clips.
        var directory = Directory.CreateTempSubdirectory("slg-asr-wav-");
        try
        {
            var path = Path.Combine(directory.FullName, "silence.wav");
            var samples = new short[(int)(seconds * PcmClip.DefaultSampleRate)];
            WavFile.Write(path, new WavData(PcmClip.DefaultSampleRate, 1, samples));
            return FromWav(path);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>A vowel-like synthetic signal: a 110 Hz harmonic series shaped by moving formants, syllable-gated at 4 Hz.</summary>
    public static PcmClip VowelClip(double seconds = 2.0)
    {
        (double F1, double F2)[] vowels = [(700, 1200), (500, 1800), (300, 2300), (500, 900), (350, 700)];
        var rate = PcmClip.DefaultSampleRate;
        var samples = new short[(int)(seconds * rate)];
        var phases = new double[40];
        for (var i = 0; i < samples.Length; i++)
        {
            var t = (double)i / rate;
            var (f1, f2) = vowels[(int)(t / 0.4) % vowels.Length];
            var f0 = 110.0 + (6.0 * Math.Sin(2.0 * Math.PI * 5.0 * t));
            var sum = 0.0;
            for (var k = 1; k <= phases.Length; k++)
            {
                phases[k - 1] += 2.0 * Math.PI * k * f0 / rate;
                var f = k * f0;
                var envelope = Math.Exp(-Math.Pow((f - f1) / 150.0, 2)) + (0.7 * Math.Exp(-Math.Pow((f - f2) / 250.0, 2))) + 0.02;
                sum += envelope * Math.Sin(phases[k - 1]);
            }

            var gate = 0.5 + (0.5 * Math.Sin(2.0 * Math.PI * 4.0 * t));
            samples[i] = (short)Math.Round(1500.0 * sum * (0.2 + (0.8 * gate)));
        }

        return PcmClip.FromSamples(samples);
    }

    public static PcmClip FromWav(string path)
    {
        var wav = WavFile.Read(path);
        return PcmClip.FromSamples(Resampler.To16k(Downmixer.ToMono(wav), wav.SampleRate));
    }

    public static AsrResult Result(string text, string modelHash = "model-a", RecognitionMode mode = RecognitionMode.Constrained)
    {
        return new AsrResult(
            text,
            [new AsrHypothesis(text, 1.0)],
            [new AsrWord(text, 0.25, 0.75, 0.9)],
            1.0,
            modelHash,
            mode);
    }
}

/// <summary>Locates the model files and the committed manifest for the real-model tests.</summary>
internal static class ModelGate
{
    public const string VoskId = "vosk-model-small-es-0.42";
    public const string ParakeetId = "sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8";

    private static readonly Lazy<ModelStore?> StoreValue = new(CreateStore);

    /// <summary>The store over <c>SLG_MODELS_DIR</c> and the committed manifest, or null when either is unavailable.</summary>
    public static ModelStore? Store => StoreValue.Value;

    public static string SkipReason(string id)
    {
        var root = Environment.GetEnvironmentVariable(ModelStore.EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(root))
        {
            return $"{ModelStore.EnvironmentVariable} is not set; run scripts/fetch-models.sh {id} and set it to the models directory.";
        }

        return FindManifest() is null
            ? "models/manifest.json was not found above the test binaries."
            : $"{id} is missing or does not match models/manifest.json under {root}.";
    }

    public static bool IsAvailable(string id)
    {
        return Store is { } store && store.Verify(id).Status is ModelStatus.Ok or ModelStatus.Unpinned;
    }

    public static string? FindManifest()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "models", ModelStore.ManifestFileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static ModelStore? CreateStore()
    {
        var root = Environment.GetEnvironmentVariable(ModelStore.EnvironmentVariable);
        var manifestPath = FindManifest();
        return string.IsNullOrWhiteSpace(root) || manifestPath is null
            ? null
            : new ModelStore(root, ModelManifest.Load(manifestPath));
    }
}

/// <summary>A fact that is skipped unless the Vosk small Spanish model is present and verifies against the manifest.</summary>
public sealed class VoskModelFactAttribute : FactAttribute
{
    public VoskModelFactAttribute()
    {
        if (!ModelGate.IsAvailable(ModelGate.VoskId))
        {
            Skip = ModelGate.SkipReason(ModelGate.VoskId);
        }
    }
}

/// <summary>A fact that is skipped unless the Parakeet model is present and every pinned file verifies against the manifest.</summary>
public sealed class ParakeetModelFactAttribute : FactAttribute
{
    public ParakeetModelFactAttribute()
    {
        if (!ModelGate.IsAvailable(ModelGate.ParakeetId))
        {
            Skip = ModelGate.SkipReason(ModelGate.ParakeetId);
        }
    }
}
