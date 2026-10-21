using Vocaterra.Game;
using Vocaterra.Nlp.Asr.Models;

namespace Vocaterra.Game.Tests;

/// <summary>
/// A fact that needs the speech models (<c>vosk-model-small-es-0.42</c>, and <c>kokoro-multi-lang-v1_0</c> when asked): it is skipped, with the
/// reason in the test report, when <c>VOCATERRA_MODELS_DIR</c> is unset or a named model is missing from it. The models are fetched
/// outside the repository (the core's <c>scripts/fetch-models.sh</c>); nothing here downloads them.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ModelFactAttribute : FactAttribute
{
    /// <summary>Creates the fact; <paramref name="needsKokoro"/> adds the synthesizer's model to what it needs.</summary>
    public ModelFactAttribute(bool needsKokoro = false)
    {
        Skip = MissingReason(needsKokoro);
    }

    /// <summary>Why the models are not available, or <see langword="null"/> when they are.</summary>
    public static string? MissingReason(bool needsKokoro)
    {
        var store = SpeechEngine.FindModels(out var problem);
        if (store is null)
        {
            return $"Speech models unavailable: {problem} Set {ModelStore.EnvironmentVariable} to a folder with {SpeechEngine.VoskModelId}.";
        }

        if (needsKokoro && (!Directory.Exists(store.ModelDirectory(SpeechEngine.KokoroModelId)) || store.Manifest.Find(SpeechEngine.KokoroModelId) is null))
        {
            return $"Speech models unavailable: {SpeechEngine.KokoroModelId} is not in {store.RootDirectory}.";
        }

        return null;
    }
}
