using System.Globalization;
using System.Text.Json;
using Vocaterra.Game.Core.Content;
using Vocaterra.Nlp.Asr.Models;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Core.Language.Speech;

/// <summary>The guide's voice in the speech table (T04, research G6): the speaker id of the TTS model and the label that goes into the synthesis cache's key.</summary>
/// <param name="Speaker">The speaker id inside the TTS model (Kokoro speaker 28 is <c>ef_dora</c>).</param>
/// <param name="VoiceId">The voice's label (<c>ef_dora</c>).</param>
public sealed record GuideVoice(int Speaker, string VoiceId);

/// <summary>
/// The speech models of a target language (T04, research G6; <c>game/speech.json</c>): the recognizer, the synthesizer with the guide's voice, and the
/// recognition thresholds, keyed by the ASR model they were tuned on, so another recognizer brings its own numbers. The NPCs' voices stay in
/// <c>game/npcs/&lt;id&gt;.json</c>. The model ids are ids of <c>models/manifest.json</c>: <see cref="Problems"/> checks them, so a typo fails the content
/// validation and not a run.
/// </summary>
public sealed class SpeechTable
{
    /// <summary>The file's path inside the bundle folder.</summary>
    public const string RelativePath = "game/speech.json";

    /// <summary>The schema version the loader reads.</summary>
    public const int SchemaVersion = 1;

    /// <summary>The only TTS engine the game can run so far (the sherpa-onnx Kokoro export). Another engine id passes the schema and fails the validation until the game can run it.</summary>
    public const string KokoroEngine = "kokoro";

    private readonly IReadOnlyDictionary<string, double> _wordConfidence;

    /// <summary>Creates a table.</summary>
    /// <param name="asrModel">The id of the constrained-recognition model in the models manifest.</param>
    /// <param name="ttsEngine">The TTS engine's id (<see cref="KokoroEngine"/>).</param>
    /// <param name="ttsModel">The id of the TTS model in the models manifest.</param>
    /// <param name="guideVoice">The guide's voice.</param>
    /// <param name="wordConfidence">For each ASR model id, the mean word confidence under which a recognition is uncertain.</param>
    public SpeechTable(string asrModel, string ttsEngine, string ttsModel, GuideVoice guideVoice, IReadOnlyDictionary<string, double> wordConfidence)
    {
        ArgumentException.ThrowIfNullOrEmpty(asrModel);
        ArgumentException.ThrowIfNullOrEmpty(ttsEngine);
        ArgumentException.ThrowIfNullOrEmpty(ttsModel);
        ArgumentNullException.ThrowIfNull(guideVoice);
        ArgumentNullException.ThrowIfNull(wordConfidence);
        AsrModel = asrModel;
        TtsEngine = ttsEngine;
        TtsModel = ttsModel;
        GuideVoice = guideVoice;
        _wordConfidence = wordConfidence;
    }

    /// <summary>The id of the constrained-recognition model (Vosk small Spanish for Spanish).</summary>
    public string AsrModel { get; }

    /// <summary>The TTS engine's id.</summary>
    public string TtsEngine { get; }

    /// <summary>The id of the TTS model.</summary>
    public string TtsModel { get; }

    /// <summary>The guide's voice.</summary>
    public GuideVoice GuideVoice { get; }

    /// <summary>The model ids with a threshold, in id order.</summary>
    public IReadOnlyCollection<string> ThresholdModels => _wordConfidence.Keys.Order(StringComparer.Ordinal).ToList();

    /// <summary>
    /// The mean word confidence under which a recognition by <see cref="AsrModel"/> is uncertain (D-game-9; <see cref="UncertaintyRule"/>). Measured with Kokoro
    /// speech and Vosk small Spanish: correct phrases give 0.77 to 1.00, a mis-heard «hola» 0.36.
    /// </summary>
    public double WordConfidenceThreshold => _wordConfidence[AsrModel];

    /// <summary>Reads the table from the bundle's verified host file.</summary>
    /// <param name="bundle">The bundle, loaded with hash verification.</param>
    /// <exception cref="InvalidDataException">The file is missing from the bundle or malformed.</exception>
    public static SpeechTable Load(ContentBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        return Parse(GameData.HostText(bundle, RelativePath));
    }

    /// <summary>Builds the table from the file's text.</summary>
    /// <param name="json">The file's text.</param>
    /// <exception cref="InvalidDataException">The text is malformed, or no threshold is given for the ASR model.</exception>
    public static SpeechTable Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.GetProperty("schemaVersion").GetInt32() != SchemaVersion)
            {
                throw new InvalidDataException($"{RelativePath} must have schemaVersion {SchemaVersion}.");
            }

            var asr = root.GetProperty("asr").GetProperty("model").GetString()!;
            var tts = root.GetProperty("tts");
            var voice = tts.GetProperty("guideVoice");
            var thresholds = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var entry in root.GetProperty("thresholds").EnumerateObject())
            {
                thresholds[entry.Name] = entry.Value.GetProperty("wordConfidence").GetDouble();
            }

            if (!thresholds.ContainsKey(asr))
            {
                throw new InvalidDataException($"{RelativePath}: no threshold is given for the ASR model '{asr}' (thresholds are keyed by ASR model id).");
            }

            return new SpeechTable(
                asr,
                tts.GetProperty("engine").GetString()!,
                tts.GetProperty("model").GetString()!,
                new GuideVoice(voice.GetProperty("speaker").GetInt32(), voice.GetProperty("voiceId").GetString()!),
                thresholds);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException($"{RelativePath} is malformed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// The problems of the table against the models manifest: the ASR model must be one of the manifest's constrained-recognition models, the TTS model one of
    /// its TTS models, the TTS engine one the game can run, and each threshold's model a model of the manifest. With no manifest (a build that does not carry
    /// <c>models/manifest.json</c>) only the engine is checked.
    /// </summary>
    /// <param name="models">The models manifest, or <see langword="null"/> when none could be found.</param>
    public IReadOnlyList<string> Problems(ModelManifest? models)
    {
        var problems = new List<string>();
        if (!string.Equals(TtsEngine, KokoroEngine, StringComparison.Ordinal))
        {
            problems.Add(string.Create(CultureInfo.InvariantCulture, $"{RelativePath}: the TTS engine '{TtsEngine}' is not one the game can run ('{KokoroEngine}')."));
        }

        if (models is null)
        {
            return problems;
        }

        Check(models, AsrModel, ModelRoles.AsrConstrained, "the ASR model", problems);
        Check(models, TtsModel, ModelRoles.Tts, "the TTS model", problems);
        foreach (var id in ThresholdModels.Where(id => models.Find(id) is null))
        {
            problems.Add($"{RelativePath}: the threshold names the model '{id}', which models/manifest.json does not list.");
        }

        return problems;
    }

    private static void Check(ModelManifest models, string id, string role, string what, List<string> problems)
    {
        var entry = models.Find(id);
        if (entry is null)
        {
            problems.Add($"{RelativePath}: {what} '{id}' is not in models/manifest.json.");
        }
        else if (!string.Equals(entry.Role, role, StringComparison.Ordinal))
        {
            problems.Add($"{RelativePath}: {what} '{id}' has the role '{entry.Role}' in models/manifest.json, not '{role}'.");
        }
    }
}
