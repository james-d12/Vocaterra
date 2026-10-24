using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Session;
using Vocaterra.Nlp.Asr.Models;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Response.Speech;

namespace Vocaterra.SpeechRender;

/// <summary>
/// Renders the bundle's speakable lines with Kokoro (S9): <c>&lt;lineId&gt;.wav</c> at speed 1.0 and <c>&lt;lineId&gt;.slow.wav</c> at speed 0.8, in the
/// bundle's <c>speech/</c> folder, as 16-bit mono PCM. The speakers are fixed here: the guide (28, <c>ef_dora</c>), the shopkeeper (29,
/// <c>em_alex</c>) and the villager (53, <c>em_santa</c>), the ids of the core's Kokoro voice table (docs/nlp/API.md, section 6).
/// </summary>
/// <remarks>
/// <b>Re-runs.</b> <c>content/speech-index.json</c> (outside the bundle, so it is not hashed) records for each file its text, speaker, speed,
/// rate, the adapter's model hash and the file's SHA-256. A file whose inputs and bytes match is skipped; a line whose text changed is rendered
/// again; a file of a line that no longer exists is deleted. <c>docs/game/SPEECH_CHECKLIST.md</c> is rewritten from the bundle, and the owner's
/// marks in its last column are kept for every row whose text did not change. The bundle's <c>manifest.json</c> and <c>provenance.json</c> are
/// then updated by the caller (the content tool's <c>hash</c> step, run by <c>scripts/prerender-speech.sh</c>).
/// </remarks>
internal static class Program
{
    /// <summary>Kokoro's own sample rate (the core's adapter returns it).</summary>
    private const int NativeRate = 24000;

    /// <summary>The speed of the normal file and of the slow file.</summary>
    private const float NormalSpeed = 1.0f;

    /// <summary>The speed of the <c>.slow</c> file (Más despacio).</summary>
    private const float SlowSpeed = 0.8f;

    private const string DefaultBundle = "content/game-v1";
    private const string DefaultIndex = "content/speech-index.json";
    private const string DefaultChecklist = "docs/game/SPEECH_CHECKLIST.md";
    /// <summary>The core's provenance kind for a host-owned file a model rendered (M29g).</summary>
    private const string GeneratedByModelOutput = "model-output";
    private const string ChecklistMarksHeader = "ok / cut-off / mismatch";

    private static readonly JsonSerializerOptions IndexJson = new() { WriteIndented = true };

    /// <summary>One rendered file as recorded in the index.</summary>
    private sealed record IndexEntry(string Text, string Speaker, int SpeakerId, string Voice, float Speed, int Rate, string ModelHash, string Sha256, int Bytes);

    /// <summary>The index file: the model id and every file of the speech folder, keyed by its bundle-relative path.</summary>
    private sealed record IndexFile(int SchemaVersion, string Model, SortedDictionary<string, IndexEntry> Files);

    private static int Main(string[] args)
    {
        var bundleDir = Path.GetFullPath(Option(args, "--bundle") ?? DefaultBundle);
        var indexPath = Path.GetFullPath(Option(args, "--index") ?? DefaultIndex);
        var checklistPath = Path.GetFullPath(Option(args, "--checklist") ?? DefaultChecklist);
        var rate = int.TryParse(Option(args, "--rate") ?? NativeRate.ToString(CultureInfo.InvariantCulture), NumberStyles.None, CultureInfo.InvariantCulture, out var parsedRate)
            ? parsedRate
            : NativeRate;
        var force = args.Contains("--force", StringComparer.Ordinal);
        var check = args.Contains("--check", StringComparer.Ordinal);
        if (rate is not (NativeRate or 16000))
        {
            Console.Error.WriteLine("--rate must be 24000 (Kokoro's rate) or 16000.");
            return 2;
        }

        var bundle = GameBundle.Load(bundleDir);
        var nouns = NounCatalog.Load(bundle);
        var phrases = PhraseSheet.Load(bundle);
        var lines = SpeechLines.Enumerate(bundle, nouns, phrases);
        var speechDir = Path.Combine(bundleDir, SpeechLines.FolderName);
        Directory.CreateDirectory(speechDir);

        var index = LoadIndex(indexPath);
        var store = SpeechEngine.FindModels(out var problem)
            ?? throw new InvalidOperationException(problem ?? "No models folder.");
        var synthesizers = new Dictionary<(string, float), KokoroSynthesizer>();
        var expected = new HashSet<string>(StringComparer.Ordinal);
        var kept = new SortedDictionary<string, IndexEntry>(StringComparer.Ordinal);
        var rendered = 0;
        var skipped = 0;
        var checklist = new List<(string LineId, string File, string Speaker, string Speed, string Text)>();
        try
        {
            foreach (var line in lines)
            {
                foreach (var variant in new[] { ("", NormalSpeed), (SpeechFiles.SlowSuffix, SlowSpeed) })
                {
                    var fileName = line.LineId + variant.Item1 + ".wav";
                    var relative = SpeechLines.FolderName + "/" + fileName;
                    var target = Path.Combine(speechDir, fileName);
                    var (speakerId, voice) = VoiceOf(line.Speaker);
                    var synth = Synthesizer(store, speakerId, variant.Item2, synthesizers);
                    var wanted = new IndexEntry(line.Text, line.Speaker, speakerId, voice, variant.Item2, rate, synth.ModelHash, string.Empty, 0);
                    expected.Add(fileName);
                    checklist.Add((line.LineId, relative, line.Speaker, variant.Item2 == NormalSpeed ? "1.0" : "0.8", line.Text));

                    if (!force && index.Files.TryGetValue(relative, out var previous) && SameInputs(previous, wanted)
                        && File.Exists(target) && Sha256(File.ReadAllBytes(target)) == previous.Sha256)
                    {
                        kept[relative] = previous;
                        skipped++;
                        continue;
                    }

                    if (check)
                    {
                        Console.WriteLine($"would render: {relative}");
                        rendered++;
                        continue;
                    }

                    var audio = synth.Synthesize(new SynthesisRequest(line.Text, voice, null));
                    var samples = rate == audio.SampleRate ? audio.Samples : AudioClips.Resample(audio.Samples, audio.SampleRate, rate);
                    var bytes = WavBytes(samples, rate);
                    WriteAtomically(target, bytes);
                    kept[relative] = wanted with { Sha256 = Sha256(bytes), Bytes = bytes.Length };
                    rendered++;
                    Console.WriteLine($"rendered: {relative} ({voice}, speed {variant.Item2.ToString("0.0", CultureInfo.InvariantCulture)}) {line.Text}");
                }
            }
        }
        finally
        {
            foreach (var synth in synthesizers.Values)
            {
                synth.Dispose();
            }
        }

        if (check)
        {
            Console.WriteLine($"renderer: {lines.Count} lines, {expected.Count} files; {rendered} would be rendered, {skipped} unchanged (check only)");
            return rendered == 0 ? 0 : 1;
        }

        var deleted = DeleteStale(speechDir, expected);
        Console.WriteLine($"renderer: {lines.Count} lines, {expected.Count} files; rendered {rendered}, unchanged {skipped}, deleted {deleted}");

        WriteIndex(indexPath, new IndexFile(1, SpeechEngine.KokoroModelId, kept));
        WriteChecklist(checklistPath, checklist);
        WriteProvenance(Path.Combine(bundleDir, "provenance.json"), kept);
        Console.WriteLine($"speech folder: {speechDir} ({ReadableSize(FolderBytes(speechDir))})");
        return 0;
    }

    /// <summary>The Kokoro speaker and voice label of a speaker (the core's voice table, docs/nlp/API.md section 6).</summary>
    /// <param name="speaker">One of the game's speakers.</param>
    private static (int SpeakerId, string Voice) VoiceOf(string speaker) => speaker switch
    {
        SpeechSpeakers.Guide => (28, "ef_dora"),
        SpeechSpeakers.Shopkeeper => (29, "em_alex"),
        SpeechSpeakers.Villager => (53, "em_santa"),
        _ => throw new InvalidOperationException($"No Kokoro voice for the speaker '{speaker}'."),
    };

    private static KokoroSynthesizer Synthesizer(ModelStore store, int speakerId, float speed, Dictionary<(string, float), KokoroSynthesizer> cache)
    {
        var key = (speakerId.ToString(CultureInfo.InvariantCulture), speed);
        if (!cache.TryGetValue(key, out var synth))
        {
            synth = KokoroSynthesizer.FromStore(store, SpeechEngine.KokoroModelId, speakerId, speed);
            cache[key] = synth;
        }

        return synth;
    }

    private static bool SameInputs(IndexEntry previous, IndexEntry wanted) =>
        previous.Text == wanted.Text && previous.Speaker == wanted.Speaker && previous.SpeakerId == wanted.SpeakerId
        && previous.Voice == wanted.Voice && previous.Speed == wanted.Speed && previous.Rate == wanted.Rate && previous.ModelHash == wanted.ModelHash;

    /// <summary>Little-endian 16-bit mono PCM in a canonical 44-byte RIFF header (what the core's <c>StaticLineStore</c> reads).</summary>
    private static byte[] WavBytes(short[] samples, int rate)
    {
        var dataBytes = samples.Length * 2;
        var bytes = new byte[44 + dataBytes];
        Encoding.ASCII.GetBytes("RIFF").CopyTo(bytes, 0);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 36 + dataBytes);
        Encoding.ASCII.GetBytes("WAVEfmt ").CopyTo(bytes, 8);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), 16);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(20), 1);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(22), 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), rate);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(28), rate * 2);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(32), 2);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(34), 16);
        Encoding.ASCII.GetBytes("data").CopyTo(bytes, 36);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(40), dataBytes);
        for (var i = 0; i < samples.Length; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(44 + (2 * i)), samples[i]);
        }

        return bytes;
    }

    private static void WriteAtomically(string path, byte[] bytes)
    {
        var temp = path + ".tmp";
        File.WriteAllBytes(temp, bytes);
        File.Move(temp, path, overwrite: true);
    }

    private static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static IndexFile LoadIndex(string path)
    {
        if (!File.Exists(path))
        {
            return new IndexFile(1, SpeechEngine.KokoroModelId, new SortedDictionary<string, IndexEntry>(StringComparer.Ordinal));
        }

        var loaded = JsonSerializer.Deserialize<IndexFile>(File.ReadAllText(path, Encoding.UTF8))
            ?? throw new InvalidDataException($"The speech index '{path}' is empty.");
        return loaded with { Files = new SortedDictionary<string, IndexEntry>(loaded.Files, StringComparer.Ordinal) };
    }

    private static void WriteIndex(string path, IndexFile index)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(index, IndexJson) + "\n", new UTF8Encoding(false));
    }

    /// <summary>
    /// Rewrites the bundle's provenance: every non-speech item as it was, then one <c>model-output</c> item per speech file (the core's kind for
    /// host-owned files a model rendered, M29g: it takes the model id, the voice, the speaker id as text and a source).
    /// </summary>
    private static void WriteProvenance(string path, SortedDictionary<string, IndexEntry> speech)
    {
        var items = new List<string>();
        if (File.Exists(path))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
            foreach (var item in document.RootElement.GetProperty("items").EnumerateArray())
            {
                var itemPath = item.GetProperty("path").GetString() ?? string.Empty;
                if (itemPath.StartsWith(SpeechLines.FolderName + "/", StringComparison.Ordinal))
                {
                    continue;
                }

                items.Add(ProvenanceLine(itemPath, item.GetProperty("generatedBy").GetString() ?? string.Empty, null));
            }
        }

        foreach (var (relative, entry) in speech)
        {
            items.Add(SpeechProvenanceLine(relative, entry));
        }

        File.WriteAllText(path, "{\n  \"items\": [\n    " + string.Join(",\n    ", items) + "\n  ]\n}\n", new UTF8Encoding(false));
    }

    private static string SpeechProvenanceLine(string relative, IndexEntry entry)
    {
        var name = Path.GetFileNameWithoutExtension(relative);
        var lineId = name.EndsWith(SpeechFiles.SlowSuffix, StringComparison.Ordinal) ? name[..^SpeechFiles.SlowSuffix.Length] : name;
        var speed = entry.Speed.ToString("0.0", CultureInfo.InvariantCulture);
        var speaker = entry.SpeakerId.ToString(CultureInfo.InvariantCulture);
        return $"{{ \"path\": \"{relative}\", \"generatedBy\": \"{GeneratedByModelOutput}\", \"approvedBy\": null, \"approvedOn\": null, \"model\": \"{SpeechEngine.KokoroModelId}\", \"voice\": \"{entry.Voice}\", \"speaker\": \"{speaker}\", \"source\": \"{lineId} (speed {speed})\" }}";
    }

    private static string ProvenanceLine(string path, string generatedBy, string? source)
    {
        var extra = source is null ? string.Empty : $", \"source\": \"{source}\"";
        return $"{{ \"path\": \"{path}\", \"generatedBy\": \"{generatedBy}\", \"approvedBy\": null, \"approvedOn\": null{extra} }}";
    }

    /// <summary>Deletes the WAV files of the folder that no current line names; returns how many.</summary>
    private static int DeleteStale(string speechDir, HashSet<string> expected)
    {
        var deleted = 0;
        foreach (var file in Directory.GetFiles(speechDir, "*.wav"))
        {
            if (!expected.Contains(Path.GetFileName(file)))
            {
                File.Delete(file);
                deleted++;
            }
        }

        return deleted;
    }

    /// <summary>
    /// Writes the listening table. The owner's marks in the last column are kept for every row whose file and Spanish text are unchanged, so a
    /// re-render does not erase them.
    /// </summary>
    private static void WriteChecklist(string path, List<(string LineId, string File, string Speaker, string Speed, string Text)> rows)
    {
        var marks = ReadMarks(path);
        var builder = new StringBuilder();
        builder.Append("# Speech checklist (S9, D-game-12 step 4)\n\n");
        builder.Append("The owner listens to every file for cut-offs and for text and audio that do not match (the interim content-trust check of\n");
        builder.Append("D-game-12). Mark each row `ok`, `cut-off` or `mismatch` in the last column, with a note after it if needed. The table is written by\n");
        builder.Append("`scripts/prerender-speech.sh` from the bundle: the marks of a row are kept while its file and its text are unchanged, and a row whose\n");
        builder.Append("text changed is listed again without a mark. The files are Kokoro model output (kokoro-multi-lang-v1_0); the voices are\n");
        builder.Append("`ef_dora` (the guide, and the word cards and model answers), `em_alex` (the shopkeeper) and `em_santa` (the villager).\n\n");
        builder.Append($"| Line id | File | Speaker | Speed | Spanish | {ChecklistMarksHeader} |\n");
        builder.Append("| --- | --- | --- | --- | --- | --- |\n");
        foreach (var row in rows.OrderBy(static r => r.LineId, StringComparer.Ordinal).ThenBy(static r => r.Speed, StringComparer.Ordinal))
        {
            var mark = marks.TryGetValue(row.File, out var kept) && kept.Text == row.Text ? kept.Mark : string.Empty;
            builder.Append($"| {row.LineId} | {row.File} | {row.Speaker} | {row.Speed} | {row.Text} | {mark} |\n");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
    }

    private static Dictionary<string, (string Text, string Mark)> ReadMarks(string path)
    {
        var marks = new Dictionary<string, (string Text, string Mark)>(StringComparer.Ordinal);
        if (!File.Exists(path))
        {
            return marks;
        }

        foreach (var raw in File.ReadLines(path, Encoding.UTF8))
        {
            if (!raw.StartsWith("| speech/", StringComparison.Ordinal))
            {
                continue;
            }

            // The row is "| line | file | speaker | speed | Spanish | mark |": the file is the second cell.
            var cells = raw.Split(" | ");
            if (cells.Length < 6)
            {
                continue;
            }

            var file = cells[1].Trim();
            var text = cells[4];
            var mark = cells[5].TrimEnd(' ', '|');
            marks[file] = (text, mark);
        }

        return marks;
    }

    private static long FolderBytes(string folder) => Directory.GetFiles(folder, "*.wav").Sum(static f => new FileInfo(f).Length);

    private static string ReadableSize(long bytes) => string.Create(CultureInfo.InvariantCulture, $"{bytes / 1048576.0:0.00} MB");

    private static string? Option(string[] args, string name)
    {
        for (var i = 0; i + 1 < args.Length; i++)
        {
            if (string.Equals(args[i], name, StringComparison.Ordinal))
            {
                return args[i + 1];
            }
        }

        return null;
    }
}
