using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Vocaterra.Game;
using Vocaterra.Nlp.Pipeline;

namespace Vocaterra.MonoGame.Frontend;

/// <summary>What a session starts with: the run's profile flags, written into its <c>session_start</c> line (S11).</summary>
/// <param name="MicCheck">Whether the mic check is shown at the start.</param>
/// <param name="Muted">Whether the NPCs' voices are off (<c>--mute</c>).</param>
/// <param name="NoSpeech">Whether the speech models are not loaded (<c>--no-speech</c>).</param>
/// <param name="Greyscale">Whether <c>--greyscale</c> was given (accepted; the world is in full colour).</param>
/// <param name="TypedCredit">The credit of a correct typed use: <c>half</c> or <c>full</c>.</param>
/// <param name="Check">The check battery run in this session (<c>pre</c>, <c>post</c>, <c>recall</c>), or <see langword="null"/>.</param>
/// <param name="Scripted">Whether this is a scripted run (<c>--screenshot</c>).</param>
/// <param name="Playtest">Whether the run is the playtest profile (<c>--playtest</c>, S12).</param>
public sealed record SessionProfile(bool MicCheck, bool Muted, bool NoSpeech, bool Greyscale, string TypedCredit, string? Check, bool Scripted, bool Playtest = false);

/// <summary>
/// The session log of the front-end (S11, D-game-10): one <c>session.jsonl</c> with a JSON object per line (the session start and end, each turn,
/// each voice, phrase and world event, each check answer) and the core's <c>replay.jsonl</c> (the utterance and host-turn envelopes of the
/// conversations, written by the core's <see cref="ReplayLog"/>) in the same folder. It is the only place with a clock: every line carries the
/// wall time (<c>t</c>, ISO 8601) from the clock it is given, and nothing in the game layer reads a clock. The files are local; nothing is sent.
/// </summary>
/// <remarks>
/// The writes happen on the game's thread. Replay lines are numbered from 1 in the file; a turn names its replay line (<c>replay</c>), so the
/// report can follow a turn to its recorded evaluation. A folder that already has a log is appended to: a later run in the same folder is another
/// <c>session_start</c> and <c>session_end</c> pair, and the report reads them all.
/// </remarks>
public sealed class SessionLog : IDisposable
{
    /// <summary>The session log's file name in the session folder.</summary>
    public const string SessionFileName = "session.jsonl";

    /// <summary>The core's replay log's file name in the session folder.</summary>
    public const string ReplayFileName = "replay.jsonl";

    private static readonly JsonWriterOptions WriterOptions = new() { Indented = false, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly TextWriter _session;
    private readonly TextWriter _replayWriter;
    private readonly ReplayLog _replay;
    private readonly Func<DateTimeOffset> _clock;
    private readonly DateTimeOffset _started;
    private readonly Dictionary<int, int> _recordLines = [];
    private int _replayLines;
    private int _recordsWritten;
    private int _eventsWritten;
    private bool _disposed;

    /// <summary>Creates a log over two writers (the caller gives them; <see cref="Open"/> opens the files of a folder).</summary>
    /// <param name="session">The <c>session.jsonl</c> writer.</param>
    /// <param name="replay">The <c>replay.jsonl</c> writer.</param>
    /// <param name="clock">The wall clock of the lines (a fake one in the tests).</param>
    /// <param name="replayLines">The lines already in <c>replay.jsonl</c> (a later run in the same folder numbers on from them).</param>
    public SessionLog(TextWriter session, TextWriter replay, Func<DateTimeOffset> clock, int replayLines = 0)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(replay);
        ArgumentNullException.ThrowIfNull(clock);
        _session = session;
        _replayWriter = replay;
        _replay = new ReplayLog(replay);
        _clock = clock;
        _started = clock();
        _replayLines = replayLines;
    }

    /// <summary>The folder the sessions are kept in when <c>--session-dir</c> is not given: <c>~/.local/share/vocaterra/sessions</c>.</summary>
    public static string DefaultRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "vocaterra", "sessions");

    /// <summary>A new session folder under <see cref="DefaultRoot"/>, named by the local date and time and the session number (<c>20261022-093015-s1</c>).</summary>
    /// <param name="localNow">The local date and time of the start.</param>
    /// <param name="session">The play session number.</param>
    public static string DefaultDirectory(DateTime localNow, int session) =>
        Path.Combine(DefaultRoot, localNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-s" + session.ToString(CultureInfo.InvariantCulture));

    /// <summary>Opens (creating, or appending to) the two files of a session folder.</summary>
    /// <param name="directory">The session folder.</param>
    /// <param name="clock">The wall clock of the lines.</param>
    public static SessionLog Open(string directory, Func<DateTimeOffset> clock)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);
        ArgumentNullException.ThrowIfNull(clock);
        Directory.CreateDirectory(directory);
        var replayPath = Path.Combine(directory, ReplayFileName);
        var existing = File.Exists(replayPath) ? File.ReadLines(replayPath).Count() : 0;
        var session = new StreamWriter(Path.Combine(directory, SessionFileName), append: true, new UTF8Encoding(false));
        var replay = new StreamWriter(replayPath, append: true, new UTF8Encoding(false));
        return new SessionLog(session, replay, clock, existing);
    }

    /// <summary>The number of replay lines written so far, this run and the ones before it in the folder.</summary>
    public int ReplayLines => _replayLines;

    /// <summary>The wire name of a turn's input kind: <c>typed</c>, <c>spoken</c> or <c>button</c>.</summary>
    public static string InputName(InputKind input) => input switch
    {
        InputKind.Typed => "typed",
        InputKind.Spoken => "spoken",
        InputKind.Button => "button",
        _ => throw new ArgumentOutOfRangeException(nameof(input), input, "Unknown input kind."),
    };

    /// <summary>The first line of a run: its play session, the world seed, the bundle's hash, the stored time and the profile flags.</summary>
    /// <param name="session">The play session number (the save's session plus one on a load).</param>
    /// <param name="seed">The world seed.</param>
    /// <param name="bundleHash">The bundle's content hash.</param>
    /// <param name="storedTime">The stored time at the start (Unix seconds).</param>
    /// <param name="profile">The profile flags.</param>
    public void SessionStart(int session, ulong seed, string bundleHash, long storedTime, SessionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        WriteLine(Json("session_start", w =>
        {
            w.WriteNumber("session", session);
            w.WriteNumber("seed", seed);
            w.WriteString("bundle", bundleHash);
            w.WriteNumber("stored_time", storedTime);
            w.WriteStartObject("profile");
            w.WriteBoolean("mic_check", profile.MicCheck);
            w.WriteBoolean("muted", profile.Muted);
            w.WriteBoolean("no_speech", profile.NoSpeech);
            w.WriteBoolean("greyscale", profile.Greyscale);
            w.WriteString("typed_credit", profile.TypedCredit);
            if (profile.Check is null)
            {
                w.WriteNull("check");
            }
            else
            {
                w.WriteString("check", profile.Check);
            }

            w.WriteBoolean("scripted", profile.Scripted);
            w.WriteBoolean("playtest", profile.Playtest);
            w.WriteEndObject();
        }));
    }

    /// <summary>The start of a check battery: its kind, its session and its number of items.</summary>
    /// <param name="check">The check kind (<c>pre</c>...).</param>
    /// <param name="session">The play session number.</param>
    /// <param name="items">The number of items in the battery.</param>
    public void CheckStart(string check, int session, int items) =>
        WriteLine(Json("check_start", w =>
        {
            w.WriteString("check", check);
            w.WriteNumber("session", session);
            w.WriteNumber("items", items);
        }));

    /// <summary>The end of a check battery: how many items were answered, and how many were skipped (Esc).</summary>
    /// <param name="check">The check kind.</param>
    /// <param name="answered">The items answered.</param>
    /// <param name="skipped">The items left unanswered.</param>
    public void CheckEnd(string check, int answered, int skipped) =>
        WriteLine(Json("check_end", w =>
        {
            w.WriteString("check", check);
            w.WriteNumber("answered", answered);
            w.WriteNumber("skipped", skipped);
        }));

    /// <summary>One answer to a check item (measurement only: no progress, no learner model, no world).</summary>
    /// <param name="result">The answer.</param>
    /// <param name="check">The check kind.</param>
    /// <param name="session">The play session number.</param>
    /// <param name="tick">The simulation tick (the world is still at the start for a pre-check).</param>
    public void CheckResult(CheckResult result, string check, int session, long tick)
    {
        ArgumentNullException.ThrowIfNull(result);
        var replay = result.Replay is null ? (int?)null : WriteReplay(result.Replay);
        WriteLine(Json("check_result", w =>
        {
            w.WriteString("check", check);
            w.WriteNumber("session", session);
            w.WriteNumber("tick", tick);
            w.WriteString("item", result.ItemId);
            w.WriteString("kind", CheckNames.Of(result.Kind));
            w.WriteString("phrase", result.Phrase);
            w.WriteString("input", CheckInputNames.Of(result.Input));
            if (result.Option is { } option)
            {
                w.WriteNumber("option", option + 1);
            }
            else
            {
                w.WriteNull("option");
            }

            w.WriteBoolean("correct", result.Correct);
            WriteNullable(w, "transcript", result.Transcript);
            WriteNullable(w, "intent", result.Intent);
            w.WriteBoolean("communicated", result.Communicated);
            w.WriteBoolean("dont_know", result.DontKnow);
            w.WriteStartArray("errors");
            foreach (var tag in result.ErrorTags)
            {
                w.WriteStringValue(tag);
            }

            w.WriteEndArray();
            WriteNullable(w, "confidence", result.Confidence);
            w.WriteNumber("latency_ms", Math.Round(result.LatencyMs, 1, MidpointRounding.AwayFromZero));
            if (replay is { } line)
            {
                w.WriteNumber("replay", line);
            }
            else
            {
                w.WriteNull("replay");
            }
        }));
    }

    /// <summary>
    /// Writes what the conversations recorded and the events they logged since the last call: the replay records first (so a turn can name its
    /// line), then the events. Call it once per frame.
    /// </summary>
    /// <param name="session">The game session.</param>
    public void Pump(GuideSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        FlushReplay(session);
        FlushEvents(session);
    }

    /// <summary>
    /// One finished turn, as the session applied it: the turn line (its input, transcript, verdict, latency, support and the phrase's
    /// classification), after the replay records and the events logged before it, and before the events it logged itself.
    /// </summary>
    /// <param name="applied">The applied turn.</param>
    /// <param name="session">The game session the turn belongs to.</param>
    public void Turn(AppliedTurn applied, GuideSession session)
    {
        ArgumentNullException.ThrowIfNull(applied);
        ArgumentNullException.ThrowIfNull(session);
        var outcome = applied.Outcome;
        if (outcome.Request.Check is not null || outcome.Request.IsMicCheck || outcome.Cancelled)
        {
            return; // a check answer is logged by its check screen; a mic check is no turn; a cancelled turn never happened
        }

        FlushReplay(session);
        var turn = applied.Turn;
        if (turn is null)
        {
            WriteLine(Json("turn_failed", w =>
            {
                w.WriteNumber("tick", outcome.Request.Tick);
                w.WriteString("npc", outcome.Request.Npc);
                w.WriteString("problem", outcome.Problem ?? "no turn");
            }));
            FlushEvents(session);
            return;
        }

        var phrase = PhraseOf(session, turn.Npc, outcome.Request.Tick);
        int? replayLine = outcome.ReplayIndex is { } index && _recordLines.TryGetValue(index, out var line) ? line : null;
        WriteLine(Json("turn", w =>
        {
            w.WriteString("npc", turn.Npc);
            w.WriteNumber("tick", outcome.Request.Tick);
            w.WriteString("prompt", turn.Prompt);
            if (replayLine is { } at)
            {
                w.WriteNumber("replay", at);
            }
            else
            {
                w.WriteNull("replay");
            }

            w.WriteString("input", InputName(turn.Input));
            w.WriteString("transcript", turn.Line);
            w.WriteString("intent", turn.Intent);
            w.WriteBoolean("communicated", turn.Communicated);
            w.WriteNumber("correctness", Math.Round(turn.Correctness, 3, MidpointRounding.AwayFromZero));
            w.WriteStartArray("errors");
            foreach (var error in turn.Errors)
            {
                w.WriteStringValue(error.Tag);
            }

            w.WriteEndArray();
            WriteNullable(w, "confidence", turn.Confidence is { } c ? Math.Round(c, 3, MidpointRounding.AwayFromZero) : null);
            w.WriteNumber("latency_ms", Math.Round(outcome.Timings.TotalMilliseconds, 1, MidpointRounding.AwayFromZero));
            w.WriteString("state", turn.AgentState);
            WriteNullable(w, "repair", turn.RepairKind is { } repair ? SnakeCase(repair.ToString()) : null);
            w.WriteBoolean("uncertain", turn.Uncertain);
            w.WriteBoolean("choice", turn.Choice);
            w.WriteBoolean("fail_forward", turn.FailForward);
            w.WriteStartArray("host_events");
            foreach (var hostEvent in turn.HostEvents)
            {
                w.WriteStringValue(hostEvent);
            }

            w.WriteEndArray();
            WriteNullable(w, "phrase", phrase?.PhraseId);
            WriteNullable(w, "classification", phrase is null ? null : GameEventNames.Of(phrase.Kind));
            WriteNullable(w, "support", phrase is null ? null : SupportLevelNames.Of(phrase.Support));
        }));
        FlushEvents(session);
    }

    /// <summary>The end of a run: the objective, the phrase counts, the ticks and the wall time it took.</summary>
    /// <param name="session">The game session.</param>
    /// <param name="tick">The simulation tick at the end.</param>
    public void End(VoxelSession session, long tick)
    {
        ArgumentNullException.ThrowIfNull(session);
        Pump(session);
        var now = _clock();
        var completedAt = session.Events.OfType<WorldEvent>().FirstOrDefault(e => e.Name == WorldEvents.ObjectiveComplete)?.Tick;
        WriteLine(Json("session_end", w =>
        {
            w.WriteNumber("tick", tick);
            w.WriteNumber("stored_time", session.StoredTime.UnixSeconds);
            w.WriteNumber("session", session.PlaySession);
            w.WriteNumber("wall_seconds", Math.Round((now - _started).TotalSeconds, 1, MidpointRounding.AwayFromZero));
            w.WriteBoolean("objective_complete", session.Objective.Complete);
            if (completedAt is { } complete)
            {
                w.WriteNumber("objective_complete_tick", complete);
            }
            else
            {
                w.WriteNull("objective_complete_tick");
            }

            w.WriteStartArray("objective");
            foreach (var step in session.Objective.Steps)
            {
                w.WriteStartObject();
                w.WriteString("step", step.Label);
                w.WriteNumber("count", Math.Min(step.Count, step.Target));
                w.WriteNumber("target", step.Target);
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteStartObject("phrases");
            foreach (var record in session.PhraseProgress.Records.Where(static r => r.Seen).OrderBy(static r => r.Id, StringComparer.Ordinal))
            {
                w.WriteStartObject(record.Id);
                w.WriteNumber("card_shown", record.CardShown);
                w.WriteNumber("supported", record.Supported);
                w.WriteNumber("unaided", record.Unaided);
                w.WriteNumber("choice_only", record.ChoiceOnly);
                w.WriteNumber("dont_know", record.DontKnow);
                w.WriteNumber("transfer", record.Transfer);
                w.WriteNumber("fail_forward", record.FailForward);
                w.WriteEndObject();
            }

            w.WriteEndObject();
        }));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _session.Dispose();
        _replayWriter.Dispose();
    }

    // The replay records the conversations recorded since the last flush, in order, each on its own line; a record's line is remembered for its turn.
    private void FlushReplay(GuideSession session)
    {
        var records = session.Conversation.Records;
        for (; _recordsWritten < records.Count; _recordsWritten++)
        {
            _recordLines[_recordsWritten] = WriteReplay(records[_recordsWritten]);
        }
    }

    // The events logged since the last flush, each on its own line.
    private void FlushEvents(GuideSession session)
    {
        var events = session.Events;
        for (; _eventsWritten < events.Count; _eventsWritten++)
        {
            WriteEvent(events[_eventsWritten]);
        }
    }

    private int WriteReplay(ReplayEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.Utterance is { } utterance)
        {
            _replay.Append(utterance);
        }
        else if (entry.HostTurn is { } hostTurn)
        {
            _replay.Append(hostTurn);
        }

        _replay.Flush();
        return ++_replayLines;
    }

    private void WriteEvent(GameEvent gameEvent)
    {
        switch (gameEvent)
        {
            case PhraseUse use:
                WriteLine(Json("phrase", w =>
                {
                    w.WriteNumber("tick", use.Tick);
                    w.WriteNumber("stored_time", use.Time);
                    w.WriteString("phrase", use.PhraseId);
                    w.WriteString("kind", GameEventNames.Of(use.Kind));
                    w.WriteString("npc", use.Npc);
                    w.WriteString("prompt", use.Prompt);
                    w.WriteString("line", use.Line);
                    WriteNullable(w, "detail", use.Detail);
                    w.WriteString("input", InputName(use.Input));
                    w.WriteBoolean("correct", use.Correct);
                    w.WriteString("support", SupportLevelNames.Of(use.Support));
                }));
                break;
            case VoiceEvent voice:
                WriteLine(Json("voice", w =>
                {
                    w.WriteNumber("tick", voice.Tick);
                    w.WriteNumber("stored_time", voice.Time);
                    w.WriteString("kind", VoiceEventNames.Of(voice.Kind));
                    WriteNullable(w, "npc", voice.Npc);
                    WriteNullable(w, "detail", voice.Detail);
                }));
                break;
            case WorldEvent world:
                WriteLine(Json("world", w =>
                {
                    w.WriteNumber("tick", world.Tick);
                    w.WriteNumber("stored_time", world.Time);
                    w.WriteString("name", world.Name);
                    WriteNullable(w, "npc", world.Npc);
                    WriteNullable(w, "detail", world.Detail);
                }));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(gameEvent), gameEvent, "Unknown game event.");
        }
    }

    // The phrase use a turn made (the classification of its line), among the events logged for the turn: the same tick and NPC.
    private static PhraseUse? PhraseOf(GuideSession session, string npc, long tick)
    {
        foreach (var use in session.Events.OfType<PhraseUse>().Reverse())
        {
            if (use.Tick == tick && string.Equals(use.Npc, npc, StringComparison.Ordinal))
            {
                return use;
            }

            if (use.Tick < tick)
            {
                break;
            }
        }

        return null;
    }

    private static void WriteNullable(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is null)
        {
            writer.WriteNull(name);
        }
        else
        {
            writer.WriteString(name, value);
        }
    }

    private static void WriteNullable(Utf8JsonWriter writer, string name, double? value)
    {
        if (value is { } number)
        {
            writer.WriteNumber(name, number);
        }
        else
        {
            writer.WriteNull(name);
        }
    }

    private static string SnakeCase(string name)
    {
        var builder = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(c));
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    // One JSON object: its type and the wall time first, then the body.
    private string Json(string type, Action<Utf8JsonWriter> body)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteString("type", type);
            writer.WriteString("t", _clock().ToString("O", CultureInfo.InvariantCulture));
            body(writer);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private void WriteLine(string json)
    {
        _session.Write(json);
        _session.Write('\n');
        _session.Flush();
    }
}
