using System.Text.Json;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Replay;

namespace Vocaterra.Nlp.Pipeline;

/// <summary>Reads the JSON Lines files written by <see cref="ReplayLog"/>.</summary>
public static class ReplayReader
{
    /// <summary>
    /// Reads the core record of every line; envelope lines contribute their <see cref="ReplayEnvelope.Record"/>. Host-turn entries
    /// (<see cref="HostTurnEnvelope"/>) have no core record and are skipped; use <see cref="ReadEntries"/> to see them.
    /// </summary>
    /// <exception cref="InvalidDataException">A line is not valid JSON or is neither a record nor an envelope; the message names the line number.</exception>
    public static IReadOnlyList<ReplayRecord> Read(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var records = new List<ReplayRecord>();
        foreach (var (number, line) in Lines(reader))
        {
            if (Parse(number, line, static json => IsHostTurn(json)))
            {
                continue;
            }

            records.Add(Parse(number, line, static json => IsEnvelope(json)
                ? Check(CanonicalJson.Deserialize<ReplayEnvelope>(json)).Record
                : Check(CanonicalJson.Deserialize<ReplayRecord>(json))));
        }

        return records;
    }

    /// <summary>Reads every line as an utterance envelope; host-turn entries are skipped (use <see cref="ReadEntries"/> to see them).</summary>
    /// <exception cref="InvalidDataException">A line is not valid JSON or is a bare record; the message names the line number.</exception>
    public static IReadOnlyList<ReplayEnvelope> ReadEnvelopes(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var envelopes = new List<ReplayEnvelope>();
        foreach (var (number, line) in Lines(reader))
        {
            if (Parse(number, line, static json => IsHostTurn(json)))
            {
                continue;
            }

            envelopes.Add(Parse(number, line, static json => Check(CanonicalJson.Deserialize<ReplayEnvelope>(json))));
        }

        return envelopes;
    }

    /// <summary>Reads every line as an utterance envelope or a host-turn entry, in file order.</summary>
    /// <exception cref="InvalidDataException">A line is not valid JSON, is a bare record, or lacks required members; the message names the line number.</exception>
    public static IReadOnlyList<ReplayEntry> ReadEntries(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var entries = new List<ReplayEntry>();
        foreach (var (number, line) in Lines(reader))
        {
            entries.Add(Parse(number, line, static json => IsHostTurn(json)
                ? new ReplayEntry(null, Check(CanonicalJson.Deserialize<HostTurnEnvelope>(json)))
                : new ReplayEntry(Check(CanonicalJson.Deserialize<ReplayEnvelope>(json)), null)));
        }

        return entries;
    }

    private static IEnumerable<(int Number, string Line)> Lines(TextReader reader)
    {
        var number = 0;
        while (reader.ReadLine() is { } line)
        {
            number++;
            if (!string.IsNullOrWhiteSpace(line))
            {
                yield return (number, line);
            }
        }
    }

    private static T Parse<T>(int number, string line, Func<string, T> parse)
    {
        try
        {
            return parse(line);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Replay log line {number}: {ex.Message}", ex);
        }
    }

    // A missing property arrives as null even though the members are declared non-nullable, so check explicitly.
    private static ReplayRecord Check(ReplayRecord record)
    {
        if ((object?)record.ClipHash is null || (object?)record.PromptId is null || (object?)record.HostStateHash is null
            || (object?)record.Versions is null || (object?)record.Verdict is null)
        {
            throw new JsonException("The entry is not a replay record (it needs clipHash, promptId, hostStateHash, versions and verdict).");
        }

        return record;
    }

    private static ReplayEnvelope Check(ReplayEnvelope envelope)
    {
        if ((object?)envelope.Record is null || (object?)envelope.Asr is null || (object?)envelope.Context is null || (object?)envelope.Result is null)
        {
            throw new JsonException("The entry is not a replay envelope (it needs record, asr, context and result).");
        }

        Check(envelope.Record);
        return envelope;
    }

    private static HostTurnEnvelope Check(HostTurnEnvelope envelope)
    {
        if ((object?)envelope.HostEvent is null || (object?)envelope.Context is null || (object?)envelope.Result is null)
        {
            throw new JsonException("The entry is not a host-turn entry (it needs hostEvent, context and result).");
        }

        return envelope;
    }

    private static bool IsHostTurn(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty("kind", out var kind)
            && kind.ValueKind == JsonValueKind.String
            && string.Equals(kind.GetString(), HostTurnEnvelope.KindName, StringComparison.Ordinal);
    }

    private static bool IsEnvelope(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("record", out _);
    }
}
