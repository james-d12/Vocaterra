using Vocaterra.Nlp.Asr.Models;

namespace Vocaterra.Game.Tests;

/// <summary>
/// S17b: the speech-model problems the game shows are English and say what to do. The cheap check runs with no models: the models folder is an empty
/// temporary folder, or the variable is unset. The environment is set and restored by each test (the models variables are read by no other test at
/// the same time in this assembly, and each test restores them).
/// </summary>
public sealed class SpeechProblemTests : IDisposable
{
    private static readonly string[] Variables = [ModelStore.EnvironmentVariable, ModelStore.LegacyEnvironmentVariable];
    private readonly Dictionary<string, string?> _saved = [];
    private readonly string _empty = Path.Combine(Path.GetTempPath(), "vocaterra-s17b-" + Guid.NewGuid().ToString("N"));

    public SpeechProblemTests()
    {
        foreach (var name in Variables)
        {
            _saved[name] = Environment.GetEnvironmentVariable(name);
        }

        _ = Directory.CreateDirectory(_empty);
    }

    public void Dispose()
    {
        foreach (var (name, value) in _saved)
        {
            Environment.SetEnvironmentVariable(name, value);
        }

        if (Directory.Exists(_empty))
        {
            Directory.Delete(_empty, recursive: true);
        }
    }

    [Fact]
    public void WithNoModelsFolderTheMessageSaysWhatToSetAndThatTheTypedAnswerStillWorks()
    {
        foreach (var name in Variables)
        {
            Environment.SetEnvironmentVariable(name, null);
        }

        var store = SpeechEngine.FindModels(out var problem);

        Assert.Null(store);
        Assert.Equal(
            "Speech models not found: set VOCATERRA_MODELS_DIR (see nlp/scripts/fetch-models.sh). You can still type.",
            problem);
        Assert.Equal(SpeechEngine.NoModelsFolder, problem);
    }

    [Fact]
    public void WithAnEmptyModelsFolderTheMessageNamesTheMissingModelInEnglish()
    {
        Environment.SetEnvironmentVariable(ModelStore.EnvironmentVariable, _empty);

        var store = SpeechEngine.FindModels(out var problem);

        Assert.Null(store);
        Assert.NotNull(problem);
        Assert.StartsWith($"Speech model {SpeechEngine.VoskModelId} is missing from ", problem, StringComparison.Ordinal);
        Assert.Contains(_empty, problem, StringComparison.Ordinal);
        Assert.EndsWith("You can still type.", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void WithAnEmptyModelsFolderTheEngineDoesNotLoadAndSaysTheSameInEnglish()
    {
        Environment.SetEnvironmentVariable(ModelStore.EnvironmentVariable, _empty);

        var engine = SpeechEngine.TryCreate(out var problem, cacheDirectory: _empty);

        Assert.Null(engine);
        Assert.StartsWith("Speech model ", problem, StringComparison.Ordinal);
        Assert.EndsWith("You can still type.", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void NoModelProblemIsWrittenInEnglish()
    {
        Assert.DoesNotMatch("Falta|No se |hay |Sin voz|reconocedor", SpeechEngine.NoModelsFolder);
    }
}
