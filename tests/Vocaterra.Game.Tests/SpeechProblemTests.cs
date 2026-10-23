using Vocaterra.Nlp.Asr.Models;

namespace Vocaterra.Game.Tests;

/// <summary>
/// S17b: the speech-model problems the game shows are English and say what to do. The cheap check runs with no models. Its variables come from a
/// reader the test passes in (<see cref="SpeechEngine.FindModels(Func{string, string?}, out string?)"/>), so no test changes the process environment,
/// which other test classes read in parallel.
/// </summary>
public sealed class SpeechProblemTests : IDisposable
{
    private readonly string _empty = Path.Combine(Path.GetTempPath(), "vocaterra-s17b-" + Guid.NewGuid().ToString("N"));

    public SpeechProblemTests() => _ = Directory.CreateDirectory(_empty);

    public void Dispose()
    {
        if (Directory.Exists(_empty))
        {
            Directory.Delete(_empty, recursive: true);
        }
    }

    [Fact]
    public void WithNoModelsFolderTheMessageSaysWhatToSetAndThatTheTypedAnswerStillWorks()
    {
        var store = SpeechEngine.FindModels(_ => null, out var problem);

        Assert.Null(store);
        Assert.Equal(
            "Speech models not found: set VOCATERRA_MODELS_DIR (see nlp/scripts/fetch-models.sh). You can still type.",
            problem);
        Assert.Equal(SpeechEngine.NoModelsFolder, problem);
    }

    [Fact]
    public void WithAnEmptyModelsFolderTheMessageNamesTheMissingModelInEnglish()
    {
        var store = SpeechEngine.FindModels(name => name == ModelStore.EnvironmentVariable ? _empty : null, out var problem);

        Assert.Null(store);
        Assert.NotNull(problem);
        Assert.StartsWith($"Speech model {SpeechEngine.VoskModelId} is missing from ", problem, StringComparison.Ordinal);
        Assert.Contains(_empty, problem, StringComparison.Ordinal);
        Assert.EndsWith("You can still type.", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void NoModelProblemIsWrittenInEnglish()
    {
        Assert.DoesNotMatch("Falta|No se |hay |Sin voz|reconocedor", SpeechEngine.NoModelsFolder);
    }
}
