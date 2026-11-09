using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Language.Speech;

namespace Vocaterra.Game.Tests.TestSupport;

/// <summary>A fact that needs the speech models: skipped, with the reason in the report, when <c>VOCATERRA_MODELS_DIR</c> or a model is missing.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ModelFactAttribute : FactAttribute
{
    /// <summary>Creates the fact; <paramref name="needsKokoro"/> is <see langword="false"/> for a test that only recognises a recorded WAV (Vosk alone).</summary>
    public ModelFactAttribute(bool needsKokoro = true)
    {
        var table = SpeechTable.Load(GameBundle.Load());
        var store = SpeechEngine.FindModels(table, out var problem);
        if (store is null)
        {
            Skip = $"Speech models unavailable: {problem}";
        }
        else if (!Directory.Exists(store.ModelDirectory(table.AsrModel)) || store.Manifest.Find(table.AsrModel) is null)
        {
            Skip = $"Speech models unavailable: {table.AsrModel} is not in {store.RootDirectory}.";
        }
        else if (needsKokoro && (!Directory.Exists(store.ModelDirectory(table.TtsModel)) || store.Manifest.Find(table.TtsModel) is null))
        {
            Skip = $"Speech models unavailable: {table.TtsModel} is not in {store.RootDirectory}.";
        }
    }
}
