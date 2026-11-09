using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Nlp.Asr.Models;

namespace Vocaterra.Game.Core.Tests.Language.Speech;

/// <summary>T04 (research G6): the speech table is data of the bundle, with Spanish values identical to the constants it replaced; a bad model id fails the validation, not a run.</summary>
public sealed class SpeechTableTests
{
    private const string German = """
        { "schemaVersion": 1,
          "asr": { "model": "vosk-model-small-de-0.15" },
          "tts": { "engine": "kokoro", "model": "kokoro-multi-lang-v1_0", "guideVoice": { "speaker": 3, "voiceId": "xx_test" } },
          "thresholds": { "vosk-model-small-de-0.15": { "wordConfidence": 0.6 } } }
        """;

    private static ModelEntry Entry(string id, string role) => new(id, role, id, "1", "Apache-2.0", "test", null, [], null);

    private static ModelManifest Manifest(params ModelEntry[] entries) => new(ModelManifest.CurrentSchemaVersion, entries);

    [Fact]
    public void TheSpanishBundleHoldsTodaysValues()
    {
        var table = TestGame.Data.SpeechTable;

        Assert.Equal("vosk-model-small-es-0.42", table.AsrModel);
        Assert.Equal("kokoro", table.TtsEngine);
        Assert.Equal("kokoro-multi-lang-v1_0", table.TtsModel);
        Assert.Equal(28, table.GuideVoice.Speaker);
        Assert.Equal("ef_dora", table.GuideVoice.VoiceId);
        Assert.Equal(0.55, table.WordConfidenceThreshold);
        Assert.Empty(table.Problems(SpeechEngine.FindManifest(Environment.GetEnvironmentVariable)));
    }

    [Fact]
    public void ATableWithAnotherModelIdIsReadNotRun()
    {
        var table = SpeechTable.Parse(German);

        Assert.Equal("vosk-model-small-de-0.15", table.AsrModel);
        Assert.Equal(0.6, table.WordConfidenceThreshold);
        Assert.Equal(3, table.GuideVoice.Speaker);
        Assert.Equal(["vosk-model-small-de-0.15"], table.ThresholdModels);
        var manifest = Manifest(Entry("vosk-model-small-de-0.15", ModelRoles.AsrConstrained), Entry("kokoro-multi-lang-v1_0", ModelRoles.Tts));
        Assert.Empty(table.Problems(manifest));
    }

    [Fact]
    public void AnUnknownModelIdFailsTheValidation()
    {
        var table = SpeechTable.Parse(German);

        var problems = table.Problems(Manifest(Entry("kokoro-multi-lang-v1_0", ModelRoles.Tts)));

        Assert.Contains(problems, static p => p.Contains("vosk-model-small-de-0.15", StringComparison.Ordinal) && p.Contains("not in models/manifest.json", StringComparison.Ordinal));
    }

    [Fact]
    public void AModelOfTheWrongRoleFailsTheValidation()
    {
        var table = SpeechTable.Parse(German);

        var problems = table.Problems(Manifest(Entry("vosk-model-small-de-0.15", ModelRoles.Tts), Entry("kokoro-multi-lang-v1_0", ModelRoles.Tts)));

        Assert.Contains(problems, static p => p.Contains("role", StringComparison.Ordinal));
    }

    [Fact]
    public void TheValidatorReportsTheSpeechTablesProblems()
    {
        var data = TestGame.Data.WithSpeechTable(SpeechTable.Parse(German));

        var problems = GameDataValidator.Validate(data, Manifest(Entry("kokoro-multi-lang-v1_0", ModelRoles.Tts)));

        Assert.Contains(problems, static p => p.Contains("game/speech.json", StringComparison.Ordinal));
    }

    [Fact]
    public void ATableWithNoThresholdForItsRecognizerIsMalformed()
    {
        var text = German.Replace("\"thresholds\": { \"vosk-model-small-de-0.15\"", "\"thresholds\": { \"other\"", StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => SpeechTable.Parse(text));
    }

    [Fact]
    public void AnEngineTheGameCannotRunFailsTheValidationEvenWithNoManifest()
    {
        var table = SpeechTable.Parse(German.Replace("\"kokoro\"", "\"piper\"", StringComparison.Ordinal));

        Assert.Single(table.Problems(null));
    }
}
