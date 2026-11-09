using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Nlp.Asr.Models;

namespace Vocaterra.Game.Core.Tests.TestSupport;

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
        var store = SpeechEngine.FindModels(TestGame.Data.SpeechTable, out var problem);
        if (store is null)
        {
            return $"Speech models unavailable: {problem} Set {ModelStore.EnvironmentVariable} to a folder with {TestGame.Data.SpeechTable.AsrModel}.";
        }

        if (needsKokoro && (!Directory.Exists(store.ModelDirectory(TestGame.Data.SpeechTable.TtsModel)) || store.Manifest.Find(TestGame.Data.SpeechTable.TtsModel) is null))
        {
            return $"Speech models unavailable: {TestGame.Data.SpeechTable.TtsModel} is not in {store.RootDirectory}.";
        }

        return null;
    }
}
