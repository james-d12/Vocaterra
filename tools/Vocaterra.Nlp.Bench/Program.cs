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
using Vocaterra.Nlp.Es;

namespace Vocaterra.Nlp.Bench;

/// <summary>
/// Entry point of the ASR benchmark: runs a recognizer over <c>tests/golden/manifest.jsonl</c> and prints a table plus a JSON summary of word
/// error rate, learner-error preservation and latency percentiles. Exit codes: 0 success, 1 the input is invalid, 2 the command line is wrong.
/// </summary>
public static class Program
{
    /// <summary>Exit code for success.</summary>
    public const int ExitOk = 0;

    /// <summary>Exit code for invalid input or a missing model.</summary>
    public const int ExitFailed = 1;

    /// <summary>Exit code for a malformed command line.</summary>
    public const int ExitUsage = 2;

    private const string Usage =
        """
        Vocaterra.Nlp.Bench - ASR benchmark over the golden manifest

        Usage:
          --manifest <manifest.jsonl> --content <dir> [--models <dir>] [--mode constrained|open] [--audio-dir <dir>]

          --mode       force one recognition mode for every entry (default: each prompt's own mode)
          --audio-dir  where the clips are looked up (default: the manifest's directory)
          --models     the models directory; only needed when the manifest has entries
        """;

    private static readonly string[] ValueOptions = ["--manifest", "--content", "--models", "--mode", "--audio-dir"];

    /// <summary>Process entry point.</summary>
    public static int Main(string[] args)
    {
        return Run(args);
    }

    /// <summary>Runs the benchmark on the console streams.</summary>
    public static int Run(string[] args)
    {
        return Run(args, Console.Out, Console.Error);
    }

    /// <summary>Runs the benchmark, writing the table and summary to <paramref name="output"/> and problems to <paramref name="error"/>.</summary>
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
        {
            (args.Length == 0 ? error : output).WriteLine(Usage);
            return args.Length == 0 ? ExitUsage : ExitOk;
        }

        try
        {
            var line = CommandLine.Parse(args, 0, ValueOptions);
            var manifestPath = line.Required("--manifest");
            var contentDir = line.Required("--content");
            var mode = ParseMode(line.Value("--mode"));
            var audioDir = line.Value("--audio-dir") ?? Path.GetDirectoryName(Path.GetFullPath(manifestPath)) ?? ".";

            IReadOnlyList<ManifestEntry> entries;
            using (var reader = new StreamReader(manifestPath, Encoding.UTF8))
            {
                entries = ManifestReader.Read(reader);
            }

            var content = SpanishContent.Load(contentDir);
            using var models = new ModelSet(line.Value("--models"), content.LanguageId);
            var report = BenchRunner.Run(entries, content, models.For, entry => LoadClip(audioDir, entry), mode);
            ReportWriter.Write(report, output);
            return ExitOk;
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

    private static RecognitionMode? ParseMode(string? text)
    {
        return text switch
        {
            null => null,
            "constrained" => RecognitionMode.Constrained,
            "open" => RecognitionMode.Open,
            _ => throw new UsageException($"--mode must be 'constrained' or 'open', not '{text}'."),
        };
    }

    private static PcmClip? LoadClip(string audioDir, ManifestEntry entry)
    {
        var path = Path.Combine(audioDir, entry.Clip.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(path) ? new WavFileCaptureSource(path).Capture() : null;
    }

    /// <summary>Opens the recognizers on demand, so a manifest without entries needs no model.</summary>
    private sealed class ModelSet(string? modelsDir, string language) : IDisposable
    {
        private const string VoskId = "vosk-model-small-es-0.42";
        private const string ParakeetId = "sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8";

        // The language each model of this tool is run for, by model id (the multilingual Parakeet is run for Spanish here). A bundle of another
        // language is refused before a model is opened.
        private static readonly IReadOnlyDictionary<string, string> ModelLanguages = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [VoskId] = "es",
            [ParakeetId] = "es",
        };

        private readonly Dictionary<string, IWordRecognizer> _opened = new(StringComparer.Ordinal);

        public IWordRecognizer For(RecognitionMode mode)
        {
            if (modelsDir is null)
            {
                throw new InvalidOperationException("The manifest has entries to run, so --models is required.");
            }

            var manifestPath = Path.Combine(modelsDir, ModelStore.ManifestFileName);
            var store = new ModelStore(modelsDir, File.Exists(manifestPath) ? ModelManifest.Load(manifestPath) : ModelManifest.Empty);
            var id = mode == RecognitionMode.Open && Directory.Exists(store.ModelDirectory(ParakeetId)) ? ParakeetId : VoskId;
            if (!string.Equals(ModelLanguages[id], language, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"The speech model '{id}' is for language '{ModelLanguages[id]}', and the bundle is in '{language}'.");
            }

            if (_opened.TryGetValue(id, out var existing))
            {
                return existing;
            }

            if (!Directory.Exists(store.ModelDirectory(id)))
            {
                throw new DirectoryNotFoundException($"Model '{store.ModelDirectory(id)}' does not exist (run scripts/fetch-models.sh).");
            }

            var pinned = store.Manifest.Find(id) is { IsPinned: true };
            IWordRecognizer recognizer = id == ParakeetId
                ? pinned ? SherpaOnnxOfflineRecognizer.FromStore(store, id) : new SherpaOnnxOfflineRecognizer(store.ModelDirectory(id))
                : pinned ? VoskWordRecognizer.FromStore(store, id) : new VoskWordRecognizer(store.ModelDirectory(id));
            _opened[id] = recognizer;
            return recognizer;
        }

        public void Dispose()
        {
            foreach (var recognizer in _opened.Values)
            {
                (recognizer as IDisposable)?.Dispose();
            }
        }
    }
}

/// <summary>Formats a <see cref="BenchReport"/> as a text table followed by a canonical JSON summary line.</summary>
public static class ReportWriter
{
    private static readonly string[] Headers = ["clip", "prompt", "mode", "WER", "errors kept", "latency ms", "status"];

    /// <summary>Writes the table (headers even when there are no rows) and then one line of JSON.</summary>
    public static void Write(BenchReport report, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(output);
        var rows = new List<string[]> { Headers };
        foreach (var entry in report.Entries)
        {
            rows.Add(
            [
                entry.Clip,
                entry.PromptId,
                entry.Mode.ToString(),
                entry.Wer is { } wer ? wer.ToString("F3", CultureInfo.InvariantCulture) : "-",
                entry.ErrorsExpected == 0 ? "-" : $"{entry.ErrorsKept.ToString(CultureInfo.InvariantCulture)}/{entry.ErrorsExpected.ToString(CultureInfo.InvariantCulture)}",
                string.Equals(entry.Status, BenchRunner.StatusOk, StringComparison.Ordinal) ? entry.LatencyMs.ToString("F1", CultureInfo.InvariantCulture) : "-",
                entry.Status,
            ]);
        }

        var widths = new int[Headers.Length];
        foreach (var row in rows)
        {
            for (var i = 0; i < row.Length; i++)
            {
                widths[i] = Math.Max(widths[i], row[i].Length);
            }
        }

        foreach (var row in rows)
        {
            var line = new StringBuilder();
            for (var i = 0; i < row.Length; i++)
            {
                line.Append(i == row.Length - 1 ? row[i] : row[i].PadRight(widths[i] + 2));
            }

            output.WriteLine(line.ToString().TrimEnd());
        }

        output.WriteLine(CanonicalJson.Serialize(new Summary(report.Entries.Count, report.Groups)));
    }

    private sealed record Summary(int Entries, IReadOnlyList<GroupSummary> Groups);
}
