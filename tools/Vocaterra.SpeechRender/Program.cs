using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;
using Vocaterra.Nlp.Asr.Models;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Response.Speech;

namespace Vocaterra.SpeechRender;

/// <summary>
/// Renders the bundle's speakable lines with Kokoro (S9): <c>&lt;lineId&gt;.wav</c> at the speaker's speed (1.0) and <c>&lt;lineId&gt;.slow.wav</c> at 0.8 of it,
/// in the bundle's <c>speech/</c> folder, as 16-bit mono PCM. A speaker is an NPC, and its Kokoro speaker id, voice name and speed are its NPC file's
/// <c>voice</c> (VILLAGE_PLAN V01; they were fixed here): the guide 28 <c>ef_dora</c>, the shopkeeper 29 <c>em_alex</c>, the villager 53 <c>em_santa</c>, and the
/// village's fisher, baker, smith and farmer in the provisional voices of the trial (D-game-22 (d)), with a <c>gain</c> for a voice the model renders louder
/// (VILLAGE_PLAN V11). A speaker with no NPC file or no voice stops the run before anything is rendered.
/// </summary>
/// <remarks>
/// <b>Re-runs.</b> <c>content/speech-index.json</c> (outside the bundle, so it is not hashed) records for each file its text, speaker, speed,
/// rate, the voice's gain when it is not 1 (VILLAGE_PLAN V11), the adapter's model hash and the file's SHA-256. A file whose inputs and bytes match is skipped;
/// a line whose text or voice changed is rendered again; a file of a line that no longer exists is deleted. <c>docs/game/SPEECH_CHECKLIST.md</c> is rewritten from the bundle, and the owner's
/// marks in its last column are kept for every row whose text and recording did not change (a file rendered again with other bytes, a new voice for one, loses
/// its mark: VILLAGE_PLAN V09). The bundle's <c>manifest.json</c> and <c>provenance.json</c> are
/// then updated by the caller (the content tool's <c>hash</c> step, run by <c>scripts/prerender-speech.sh</c>).
/// <para>
/// <b>The voice trial</b> (VILLAGE_PLAN V06). <c>--trial &lt;folder&gt;</c> renders the folder's <c>trial.json</c> instead of the bundle: one line for every candidate
/// voice and speed, as <c>&lt;folder&gt;/&lt;file&gt;</c> at Kokoro's own rate (24 kHz mono), for the owner to hear. It writes nothing else. The checklist lists the
/// trial's provisional voices, every one the trial names an NPC for (D-game-22 (d): the voices were recorded before the owner heard them), until the owner's
/// decision takes the NPC out of the trial's entry.
/// </para>
/// </remarks>
internal static class Program
{
    /// <summary>Kokoro's own sample rate (the core's adapter returns it).</summary>
    private const int NativeRate = 24000;

    /// <summary>The speed of the <c>.slow</c> file (Más despacio), as a fraction of the speaker's own speed (0.8 of 1.0 for every voice so far).</summary>
    private const float SlowFactor = 0.8f;

    private const string DefaultBundle = "content/game-v1";
    private const string DefaultIndex = "content/speech-index.json";
    private const string DefaultChecklist = "docs/game/SPEECH_CHECKLIST.md";
    private const string DefaultTrial = "docs/game/voice-trial";
    private const string TrialFileName = "trial.json";
    /// <summary>The core's provenance kind for a host-owned file a model rendered (M29g).</summary>
    private const string GeneratedByModelOutput = "model-output";
    private const string ChecklistMarksHeader = "ok / cut-off / mismatch";

    private static readonly JsonSerializerOptions IndexJson = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions TrialJson = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// One rendered file as recorded in the index. <paramref name="Gain"/> is the voice's gain (VILLAGE_PLAN V11), written only when it is not 1, so the entries of
    /// the voices at the model's own level read as they did before it.
    /// </summary>
    private sealed record IndexEntry(
        string Text,
        string Speaker,
        int SpeakerId,
        string Voice,
        float Speed,
        int Rate,
        string ModelHash,
        string Sha256,
        int Bytes,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] float? Gain = null);

    /// <summary>The index file: the model id and every file of the speech folder, keyed by its bundle-relative path.</summary>
    private sealed record IndexFile(int SchemaVersion, string Model, SortedDictionary<string, IndexEntry> Files);

    /// <summary>
    /// One recording of the voice trial: its file in the trial folder, the Kokoro voice (its name and its speaker id in the model's list), the speed, the line it
    /// says (a key of <see cref="Trial.Lines"/>) and, for a provisional choice, the NPC it is for and that NPC's name.
    /// </summary>
    private sealed record TrialVoice(string File, string Voice, int Speaker, float Speed, string Line, string? Npc = null, string? Name = null);

    /// <summary>The voice trial's file (<c>docs/game/voice-trial/trial.json</c>): what it is for, the lines by key, and the recordings.</summary>
    private sealed record Trial(string About, Dictionary<string, string> Lines, List<TrialVoice> Voices);

    private static int Main(string[] args)
    {
        if (Option(args, "--trial") is { } trialFolder)
        {
            return RenderTrial(Path.GetFullPath(trialFolder));
        }

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
        var data = GameData.Load(bundle, LocaleBundle.Load(LocaleBundle.DirectoryBeside(bundleDir)).Text(LocaleBundle.DefaultCode));
        var lines = data.Speech;
        if (UnvoicedSpeaker(lines, data.Npcs) is { } unvoiced)
        {
            Console.Error.WriteLine(unvoiced);
            return 2;
        }

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
        var newRecordings = new HashSet<string>(StringComparer.Ordinal); // files whose bytes changed: the owner hears them again (VILLAGE_PLAN V09)
        try
        {
            foreach (var line in lines)
            {
                var spec = VoiceOf(line.Speaker, data.Npcs);
                foreach (var variant in new[] { ("", spec.Speed), (SpeechFiles.SlowSuffix, spec.Speed * SlowFactor) })
                {
                    var fileName = line.LineId + variant.Item1 + ".wav";
                    var relative = SpeechLines.FolderName + "/" + fileName;
                    var target = Path.Combine(speechDir, fileName);
                    var synth = Synthesizer(store, spec.Speaker, variant.Item2, synthesizers);
                    var wanted = new IndexEntry(line.Text, line.Speaker, spec.Speaker, spec.Name, variant.Item2, rate, synth.ModelHash, string.Empty, 0, GainOf(spec));
                    expected.Add(fileName);
                    checklist.Add((line.LineId, relative, line.Speaker, variant.Item2.ToString("0.0#", CultureInfo.InvariantCulture), line.Text));

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

                    var audio = synth.Synthesize(new SynthesisRequest(line.Text, spec.Name, null));
                    var samples = Scaled(rate == audio.SampleRate ? audio.Samples : AudioClips.Resample(audio.Samples, audio.SampleRate, rate), spec.Gain);
                    var bytes = WavBytes(samples, rate);
                    WriteAtomically(target, bytes);
                    var sha = Sha256(bytes);
                    if (!index.Files.TryGetValue(relative, out var before) || before.Sha256 != sha)
                    {
                        newRecordings.Add(relative);
                    }

                    kept[relative] = wanted with { Sha256 = sha, Bytes = bytes.Length };
                    rendered++;
                    Console.WriteLine($"rendered: {relative} ({spec.Name}, speed {variant.Item2.ToString("0.0#", CultureInfo.InvariantCulture)}) {line.Text}");
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
        WriteChecklist(checklistPath, checklist, newRecordings, data.Npcs, LoadTrial(Path.GetFullPath(Option(args, "--trial-folder") ?? DefaultTrial)));
        WriteProvenance(Path.Combine(bundleDir, "provenance.json"), kept);
        Console.WriteLine($"speech folder: {speechDir} ({ReadableSize(FolderBytes(speechDir))})");
        return 0;
    }

    /// <summary>The Kokoro speaker, voice name and speed of a speaker: its NPC file's voice (the core's voice table, docs/nlp/API.md section 6).</summary>
    /// <param name="speaker">One of the game's speakers (an NPC's id).</param>
    /// <param name="npcs">The NPCs.</param>
    /// <exception cref="InvalidOperationException">The speaker is no NPC, or its file gives no voice with a name (<see cref="UnvoicedSpeaker"/> says so first).</exception>
    private static NpcVoiceSpec VoiceOf(string speaker, NpcRoster npcs) =>
        npcs.Find(speaker)?.Voice is { Name.Length: > 0 } voice
            ? voice
            : throw new InvalidOperationException(NoVoice(speaker, npcs));

    /// <summary>The message for the first speaker of the lines that has no voice to record it with, or <see langword="null"/> when every speaker has one.</summary>
    /// <param name="lines">The speakable lines.</param>
    /// <param name="npcs">The NPCs.</param>
    private static string? UnvoicedSpeaker(IReadOnlyList<SpeechLine> lines, NpcRoster npcs) =>
        lines.Select(static line => line.Speaker).Distinct(StringComparer.Ordinal).Where(speaker => npcs.Find(speaker)?.Voice is not { Name.Length: > 0 })
            .Select(speaker => NoVoice(speaker, npcs)).FirstOrDefault();

    private static string NoVoice(string speaker, NpcRoster npcs) =>
        npcs.Find(speaker) is null
            ? $"No Kokoro voice for the speaker '{speaker}': it is not an NPC of {NpcRoster.IndexPath} ({string.Join(", ", npcs.Ids)}), so nothing says which voice records its lines."
            : $"No Kokoro voice for the speaker '{speaker}': {NpcRoster.Folder}{speaker}.json has no voice with a name (voice: {{ \"speaker\", \"name\", \"speed\" }}).";

    /// <summary>Renders the voice trial of a folder (its <c>trial.json</c>), one file a recording at Kokoro's own rate; returns the exit code.</summary>
    private static int RenderTrial(string folder)
    {
        if (LoadTrial(folder) is not { } trial)
        {
            Console.Error.WriteLine($"--trial: there is no {TrialFileName} in {folder}.");
            return 2;
        }

        if (trial.Voices.FirstOrDefault(voice => !trial.Lines.ContainsKey(voice.Line)) is { } lineless)
        {
            Console.Error.WriteLine($"--trial: {lineless.File} says the line '{lineless.Line}', which {TrialFileName} does not have ({string.Join(", ", trial.Lines.Keys)}).");
            return 2;
        }

        var store = SpeechEngine.FindModels(out var problem)
            ?? throw new InvalidOperationException(problem ?? "No models folder.");
        foreach (var voice in trial.Voices)
        {
            var text = trial.Lines[voice.Line];
            using var synth = KokoroSynthesizer.FromStore(store, SpeechEngine.KokoroModelId, voice.Speaker, voice.Speed);
            var audio = synth.Synthesize(new SynthesisRequest(text, voice.Voice, null));
            var samples = audio.SampleRate == NativeRate ? audio.Samples : AudioClips.Resample(audio.Samples, audio.SampleRate, NativeRate);
            WriteAtomically(Path.Combine(folder, voice.File), WavBytes(samples, NativeRate));
            Console.WriteLine($"trial: {voice.File} ({voice.Voice}, speaker {voice.Speaker}, speed {voice.Speed.ToString("0.0#", CultureInfo.InvariantCulture)}) {text}");
        }

        Console.WriteLine($"trial folder: {folder} ({trial.Voices.Count} files, {ReadableSize(FolderBytes(folder))})");
        return 0;
    }

    /// <summary>The voice trial of a folder, or <see langword="null"/> when it has no <c>trial.json</c>.</summary>
    private static Trial? LoadTrial(string folder)
    {
        var path = Path.Combine(folder, TrialFileName);
        return File.Exists(path)
            ? JsonSerializer.Deserialize<Trial>(File.ReadAllText(path, Encoding.UTF8), TrialJson) ?? throw new InvalidDataException($"The voice trial '{path}' is empty.")
            : null;
    }

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
        && previous.Voice == wanted.Voice && previous.Speed == wanted.Speed && previous.Rate == wanted.Rate && previous.ModelHash == wanted.ModelHash
        && (previous.Gain ?? 1.0f) == (wanted.Gain ?? 1.0f);

    /// <summary>The gain the index records for a voice: <see langword="null"/> for 1, the model's own level (VILLAGE_PLAN V11).</summary>
    private static float? GainOf(NpcVoiceSpec spec) => spec.Gain == 1.0f ? null : spec.Gain;

    /// <summary>
    /// The samples scaled by a voice's gain (VILLAGE_PLAN V11), rounded and held inside 16 bits; the same array for a gain of 1. Kokoro renders the Italian
    /// <c>if_sara</c> about 10 dB louder than the Spanish voices, with peaks near full scale, so the baker's lines are brought down to their level.
    /// </summary>
    private static short[] Scaled(short[] samples, float gain)
    {
        if (gain == 1.0f)
        {
            return samples;
        }

        var scaled = new short[samples.Length];
        for (var i = 0; i < samples.Length; i++)
        {
            scaled[i] = (short)Math.Clamp(Math.Round(samples[i] * (double)gain, MidpointRounding.AwayFromZero), short.MinValue, short.MaxValue);
        }

        return scaled;
    }

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
        var speed = entry.Speed.ToString("0.0#", CultureInfo.InvariantCulture);
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
    /// Writes the listening table. The owner's marks in the last column are kept for every row whose Spanish text and recording are unchanged, so a
    /// re-render does not erase them; a row whose file was rendered again with other bytes (<paramref name="newRecordings"/>: a new voice, gain or speed)
    /// loses its mark, since what the owner heard is gone (VILLAGE_PLAN V09). The speakers come first: each NPC's voice, then the trial's provisional voices.
    /// </summary>
    private static void WriteChecklist(
        string path, List<(string LineId, string File, string Speaker, string Speed, string Text)> rows, IReadOnlySet<string> newRecordings, NpcRoster npcs, Trial? trial)
    {
        var marks = ReadMarks(path);
        var builder = new StringBuilder();
        builder.Append("# Speech checklist (S9, D-game-12 step 4)\n\n");
        builder.Append("The owner listens to every file for cut-offs and for text and audio that do not match (the interim content-trust check of\n");
        builder.Append("D-game-12). Mark each row `ok`, `cut-off` or `mismatch` in the last column, with a note after it if needed. The table is written by\n");
        builder.Append("`scripts/prerender-speech.sh` from the bundle: the marks of a row are kept while its text and its recording are unchanged, and a row\n");
        builder.Append("whose text changed or whose file was recorded again differently is listed again without a mark. The files are Kokoro model output\n");
        builder.Append("(kokoro-multi-lang-v1_0), in the voices below.\n\n");
        AppendSpeakers(builder, npcs, trial);
        builder.Append("## Lines\n\n");
        builder.Append($"| Line id | File | Speaker | Speed | Spanish | {ChecklistMarksHeader} |\n");
        builder.Append("| --- | --- | --- | --- | --- | --- |\n");
        foreach (var row in rows.OrderBy(static r => r.LineId, StringComparer.Ordinal).ThenBy(static r => r.Speed, StringComparer.Ordinal))
        {
            var mark = !newRecordings.Contains(row.File) && marks.TryGetValue(row.File, out var kept) && kept.Text == row.Text ? kept.Mark : string.Empty;
            builder.Append($"| {row.LineId} | {row.File} | {row.Speaker} | {row.Speed} | {row.Text} | {mark} |\n");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
    }

    // The speakers: each NPC of the roster with its file's voice and what it says, then the voice trial's provisional choices (VILLAGE_PLAN V06,
    // D-game-22 (d)): every voice the trial names an NPC for, recorded or not yet, with the trial folder the owner hears them in and what to do
    // with a voice the owner rejects. A voice leaves the list when the owner's decision takes its NPC out of the trial's entry.
    private static void AppendSpeakers(StringBuilder builder, NpcRoster npcs, Trial? trial)
    {
        builder.Append("## Speakers\n\n");
        builder.Append("| Speaker | Voice | Kokoro speaker | Speed (slow) | Says |\n");
        builder.Append("| --- | --- | --- | --- | --- |\n");
        foreach (var npc in npcs.All.Where(static npc => npc.Voice is not null))
        {
            var voice = npc.Voice!;
            var says = npc.Id == SpeechSpeakers.Guide
                ? "every pool no other NPC has, its prompts, the word cards and the model answers"
                : $"the pools `{npc.PoolPrefix}_*` and `guide.lesson.{npc.PoolPrefix}.*`, and its prompts";
            var gain = voice.Gain == 1.0f ? string.Empty : $", gain {Speed(voice.Gain)}";
            builder.Append(CultureInfo.InvariantCulture, $"| {npc.Id} ({npc.Called}) | `{voice.Name}`{gain} | {voice.Speaker} | {Speed(voice.Speed)} ({Speed(voice.Speed * SlowFactor)}) | {says} |\n");
        }

        builder.Append('\n');
        var provisional = trial?.Voices.Where(static voice => voice.Npc is not null).ToList() ?? [];
        if (provisional.Count == 0)
        {
            return;
        }

        builder.Append("**Provisional voices** (VILLAGE_PLAN V06, D-game-22 (d)). The voices below were chosen from the voice trial and their NPCs' lines recorded\n");
        builder.Append($"in them before the owner heard them: hear each in the trial, `{DefaultTrial}/` (its README; the trial is at the model's own level,\n");
        builder.Append("the recordings carry the gain of the table above), then the NPC's rows. A voice the owner rejects is changed in the NPC's file,\n");
        builder.Append("`content/game-v1/game/npcs/<id>.json` (`voice`: `speaker`, `name`, `speed`, and `gain` for a voice louder or quieter than the Spanish\n");
        builder.Append("three), and `scripts/limited.sh scripts/prerender-speech.sh` renders that NPC's lines again: the speech index records each file's voice, so\n");
        builder.Append("only its files are rendered (`--force` renders every file and is not needed), and their rows lose their marks. The bundle's hash moves, so\n");
        builder.Append("the content loop follows (`content/README.md`, \"The edit loop\"). A voice is listed here while the trial's `trial.json` names its NPC.\n\n");
        builder.Append("| NPC | Voice | Kokoro speaker | Speed | Trial file |\n");
        builder.Append("| --- | --- | --- | --- | --- |\n");
        foreach (var voice in provisional)
        {
            var recorded = npcs.Find(voice.Npc!) is null ? ", no NPC file yet" : string.Empty;
            builder.Append(CultureInfo.InvariantCulture, $"| {voice.Npc} ({voice.Name}){recorded} | `{voice.Voice}` | {voice.Speaker} | {Speed(voice.Speed)} | `voice-trial/{voice.File}` |\n");
        }

        builder.Append('\n');
    }

    private static string Speed(float speed) => speed.ToString("0.0#", CultureInfo.InvariantCulture);

    private static Dictionary<string, (string Text, string Mark)> ReadMarks(string path)
    {
        var marks = new Dictionary<string, (string Text, string Mark)>(StringComparer.Ordinal);
        if (!File.Exists(path))
        {
            return marks;
        }

        foreach (var raw in File.ReadLines(path, Encoding.UTF8))
        {
            // A row of the lines is "| line | file | speaker | speed | Spanish | mark |": the file is the second cell, and only those rows have a file of the speech
            // folder there. (The test was once that the row starts with "| speech/", which no row does, so every mark was lost on a re-render; V06.)
            var cells = raw.Split(" | ");
            if (!raw.StartsWith("| ", StringComparison.Ordinal) || cells.Length < 6 || !cells[1].StartsWith(SpeechLines.FolderName + "/", StringComparison.Ordinal))
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
