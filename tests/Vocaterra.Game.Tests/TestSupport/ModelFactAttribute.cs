using Vocaterra.Game.Core.Language.Speech;

namespace Vocaterra.Game.Tests.TestSupport;

/// <summary>A fact that needs the speech models: skipped, with the reason in the report, when <c>VOCATERRA_MODELS_DIR</c> or a model is missing.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ModelFactAttribute : FactAttribute
{
    /// <summary>Creates the fact; <paramref name="needsKokoro"/> is <see langword="false"/> for a test that only recognises a recorded WAV (Vosk alone).</summary>
    public ModelFactAttribute(bool needsKokoro = true)
    {
        var store = SpeechEngine.FindModels(out var problem);
        if (store is null)
        {
            Skip = $"Speech models unavailable: {problem}";
        }
        else if (!Directory.Exists(store.ModelDirectory(SpeechEngine.VoskModelId)) || store.Manifest.Find(SpeechEngine.VoskModelId) is null)
        {
            Skip = $"Speech models unavailable: {SpeechEngine.VoskModelId} is not in {store.RootDirectory}.";
        }
        else if (needsKokoro && (!Directory.Exists(store.ModelDirectory(SpeechEngine.KokoroModelId)) || store.Manifest.Find(SpeechEngine.KokoroModelId) is null))
        {
            Skip = $"Speech models unavailable: {SpeechEngine.KokoroModelId} is not in {store.RootDirectory}.";
        }
    }
}
