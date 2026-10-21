using System.Globalization;
using System.Text;
using Vocaterra.Nlp.Asr;
using Vocaterra.Nlp.Asr.Models;
using Vocaterra.Nlp.Audio;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Decision.ConversationAgents;
using Vocaterra.Nlp.Decision.Learning;
using Vocaterra.Nlp.Pipeline;
using Vocaterra.Nlp.Pronunciation;

namespace Vocaterra.Nlp.Cli;

/// <summary>
/// Entry point of the demo host: <c>eval</c> runs one utterance through the whole pipeline, <c>replay</c> re-evaluates a replay log.
/// Exit codes: 0 success, 1 the evaluation failed or a replay differs, 2 the command line is wrong.
/// </summary>
public static class Program
{
    /// <summary>Exit code for success.</summary>
    public const int ExitOk = 0;

    /// <summary>Exit code for a failed evaluation, invalid input or a replay that differs from the recording.</summary>
    public const int ExitFailed = 1;

    /// <summary>Exit code for a malformed command line.</summary>
    public const int ExitUsage = 2;

    private const string Usage =
        """
        Vocaterra.Nlp.Cli - voice pipeline demo host

        Usage:
          eval --prompt <id> --content <dir> (--transcript "<text>" | --wav <file> --models <dir>)
               [--wav <file>] [--models <dir>] [--replay-out <file>]
               [--seed <n>] [--now <unixSeconds>] [--agent-state <id>] [--counter <n>] [--repeat <tag>=<n>]...
              Evaluates one utterance. Without --models the word recognizer is scripted from --transcript, so no model is needed.
              Prints the verdict JSON on the first line, then the agent lines, effects and repair step.
          replay <envelope.jsonl> --content <dir>
              Re-evaluates every logged utterance from its recorded recognizer output; prints OK, or the differing JSON paths and exits 1.

        Example:
          eval --transcript "yo tiene dos espada" --prompt offer-items-open --content content/v1
        """;

    /// <summary>Process entry point.</summary>
    public static int Main(string[] args)
    {
        return Run(args);
    }

    /// <summary>Runs the tool on the console streams.</summary>
    public static int Run(string[] args)
    {
        return Run(args, Console.Out, Console.Error);
    }

    /// <summary>Runs the tool, writing results to <paramref name="output"/> and problems and usage errors to <paramref name="error"/>.</summary>
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        if (args.Length == 0)
        {
            error.WriteLine(Usage);
            return ExitUsage;
        }

        try
        {
            switch (args[0])
            {
                case "help" or "--help" or "-h":
                    output.WriteLine(Usage);
                    return ExitOk;
                case "eval":
                    return Eval(CommandLine.Parse(args, 1, valueOptions: EvalOptions), output, error);
                case "replay":
                    return ReplayCommand(CommandLine.Parse(args, 1, valueOptions: ["--content"]), output, error);
                default:
                    error.WriteLine($"Unknown command '{args[0]}'.");
                    error.WriteLine(Usage);
                    return ExitUsage;
            }
        }
        catch (UsageException ex)
        {
            error.WriteLine(ex.Message);
            error.WriteLine(Usage);
            return ExitUsage;
        }
        catch (Exception ex) when (ex is ContentValidationException or IOException or UnauthorizedAccessException or InvalidDataException
            or KeyNotFoundException or ArgumentException or InvalidOperationException or FormatException)
        {
            error.WriteLine($"error: {ex.Message}");
            return ExitFailed;
        }
    }

    private static readonly string[] EvalOptions =
    [
        "--wav", "--transcript", "--prompt", "--content", "--models", "--replay-out", "--seed", "--now", "--agent-state", "--counter", "--repeat",
    ];

    private static int Eval(CommandLine line, TextWriter output, TextWriter error)
    {
        var contentDir = line.Required("--content");
        var promptId = line.Required("--prompt");
        var wavPath = line.Value("--wav");
        var transcript = line.Value("--transcript");
        var modelsDir = line.Value("--models");
        if (modelsDir is null && transcript is null)
        {
            throw new UsageException("Without --models the word recognizer is scripted, so --transcript is required.");
        }

        if (modelsDir is not null && wavPath is null)
        {
            throw new UsageException("--models needs --wav: the recognizers need audio.");
        }

        var seed = line.Number("--seed", 0UL, ulong.Parse);
        var now = line.Number("--now", 0L, long.Parse);
        var counter = line.Number("--counter", 0L, long.Parse);
        // The counts the learner had before this utterance, as a host carries them from the previous ConversationContext.Next.
        var repeats = ParseRepeats(line.Values("--repeat"));

        var bundle = ContentLoader.Load(contentDir);
        if (!bundle.Prompts.TryGetValue(promptId, out var prompt))
        {
            error.WriteLine($"error: unknown prompt '{promptId}'. Known prompts: {string.Join(", ", bundle.Prompts.Keys)}.");
            return ExitFailed;
        }

        var clip = wavPath is null ? PcmClip.FromSamples([]) : new WavFileCaptureSource(wavPath).Capture();
        using var models = new ModelSet();
        IWordRecognizer words;
        IPhonemeRecognizer? phonemes = null;
        if (modelsDir is null)
        {
            words = new ScriptedWordRecognizer(ScriptedResult(transcript!, prompt.Mode));
        }
        else
        {
            models.Open(modelsDir, prompt.Mode);
            words = models.Words!;
            phonemes = models.Phonemes;
        }

        var agentId = prompt.AgentId ?? "player";
        var machine = bundle.Agents.GetValueOrDefault(agentId);
        var agentState = line.Value("--agent-state") is { } stateId
            ? new AgentState(agentId, stateId, counter)
            : machine is null ? new AgentState(agentId, "none", counter) : machine.InitialState() with { InteractionCounter = counter };
        var context = ConversationContext.Create(seed, agentId, agentState, new StoredTimestamp(now), LearnerModel.New(), repeats);

        var pipeline = new UtterancePipeline(new PipelineServices(words, phonemes, null, null, bundle));
        var trace = pipeline.Trace(clip, prompt, context);
        Print(trace.Result, output);

        if (line.Value("--replay-out") is { } replayPath)
        {
            using var stream = new FileStream(replayPath, FileMode.Append, FileAccess.Write, FileShare.Read);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            new ReplayLog(writer).Append(ReplayEnvelope.From(trace, context));
        }

        return ExitOk;
    }

    private static int ReplayCommand(CommandLine line, TextWriter output, TextWriter error)
    {
        if (line.Positionals.Count != 1)
        {
            throw new UsageException("replay takes exactly one envelope file.");
        }

        var path = line.Positionals[0];
        var bundle = ContentLoader.Load(line.Required("--content"));
        IReadOnlyList<ReplayEnvelope> envelopes;
        using (var reader = new StreamReader(path, Encoding.UTF8))
        {
            envelopes = ReplayReader.ReadEnvelopes(reader);
        }

        // The recognizers are placeholders: the replayer swaps in the recorded outputs, and the clip's audio is never needed.
        var placeholder = new ReplayWordRecognizer(new Dictionary<string, AsrResult>(StringComparer.Ordinal));
        var pipeline = new UtterancePipeline(new PipelineServices(placeholder, null, null, null, bundle));
        var failed = false;
        for (var i = 0; i < envelopes.Count; i++)
        {
            var diff = Replayer.Replay(envelopes[i], pipeline, static hash => new PcmClip([], PcmClip.DefaultSampleRate, hash));
            if (diff is not null)
            {
                failed = true;
                output.WriteLine($"DIFF entry {(i + 1).ToString(CultureInfo.InvariantCulture)}: {diff}");
            }
        }

        if (failed)
        {
            return ExitFailed;
        }

        output.WriteLine("OK");
        return ExitOk;
    }

    private static void Print(PipelineResult result, TextWriter output)
    {
        output.WriteLine(CanonicalJson.Serialize(result.Verdict));
        foreach (var line in result.Response.Lines)
        {
            output.WriteLine($"agent: {line.Text}");
        }

        foreach (var effect in result.Effects)
        {
            output.WriteLine($"effect: {effect.EffectId} {effect.Kind} {effect.Value.ToString("R", CultureInfo.InvariantCulture)}");
        }

        if (result.Repair is { } repair)
        {
            output.WriteLine($"repair: {repair.Kind} {repair.AgentLine}");
        }

        output.WriteLine($"decision: rating {result.Decision.Rating}, learner skills {result.Decision.Learner.Skills.Count}, feedback items {result.Decision.Feedback.Items.Count}");
    }

    private static AsrResult ScriptedResult(string transcript, RecognitionMode mode)
    {
        return new AsrResult(transcript, [new AsrHypothesis(transcript, 1.0)], [], 1.0, "scripted-transcript", mode);
    }

    private static Dictionary<string, int> ParseRepeats(IReadOnlyList<string> values)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            var split = value.LastIndexOf('=');
            if (split <= 0 || !int.TryParse(value.AsSpan(split + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var count))
            {
                throw new UsageException($"--repeat expects <tag>=<count>, got '{value}'.");
            }

            counts[value[..split]] = count;
        }

        return counts;
    }

    /// <summary>The disposable recognizers opened from a models directory.</summary>
    private sealed class ModelSet : IDisposable
    {
        private const string VoskId = "vosk-model-small-es-0.42";
        private const string ParakeetId = "sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8";
        private const string PhonemeId = "wav2vec2-xlsr-53-espeak-cv-ft";

        private readonly List<IDisposable> _owned = [];

        public IWordRecognizer? Words { get; private set; }

        public IPhonemeRecognizer? Phonemes { get; private set; }

        public void Open(string modelsDir, RecognitionMode mode)
        {
            var manifestPath = Path.Combine(modelsDir, ModelStore.ManifestFileName);
            var store = new ModelStore(modelsDir, File.Exists(manifestPath) ? ModelManifest.Load(manifestPath) : ModelManifest.Empty);
            if (mode == RecognitionMode.Open && Directory.Exists(store.ModelDirectory(ParakeetId)))
            {
                var sherpa = Pinned(store, ParakeetId)
                    ? SherpaOnnxOfflineRecognizer.FromStore(store, ParakeetId)
                    : new SherpaOnnxOfflineRecognizer(store.ModelDirectory(ParakeetId));
                _owned.Add(sherpa);
                Words = sherpa;
            }
            else
            {
                if (!Directory.Exists(store.ModelDirectory(VoskId)))
                {
                    throw new DirectoryNotFoundException($"No speech model found: '{store.ModelDirectory(VoskId)}' does not exist (run scripts/fetch-models.sh).");
                }

                var vosk = Pinned(store, VoskId)
                    ? VoskWordRecognizer.FromStore(store, VoskId)
                    : new VoskWordRecognizer(store.ModelDirectory(VoskId));
                _owned.Add(vosk);
                Words = vosk;
            }

            if (Directory.Exists(store.ModelDirectory(PhonemeId)))
            {
                var phoneme = new Wav2Vec2PhonemeRecognizer(store.ModelDirectory(PhonemeId));
                _owned.Add(phoneme);
                Phonemes = phoneme;
            }
        }

        public void Dispose()
        {
            foreach (var owned in _owned)
            {
                owned.Dispose();
            }
        }

        private static bool Pinned(ModelStore store, string id)
        {
            return store.Manifest.Find(id) is { IsPinned: true };
        }
    }
}
