using System.Text.Json;
using Slg.Core.Determinism;
using Slg.Core.Replay;

namespace Slg.Pipeline;

/// <summary>Reads the JSON Lines files written by <see cref="ReplayLog"/>.</summary>
public static class ReplayReader
{
    /// <summary>Reads the core record of every line; envelope lines contribute their <see cref="ReplayEnvelope.Record"/>.</summary>
    /// <exception cref="InvalidDataException">A line is not valid JSON or is neither a record nor an envelope; the message names the line number.</exception>
    public static IReadOnlyList<ReplayRecord> Read(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var records = new List<ReplayRecord>();
        foreach (var (number, line) in Lines(reader))
        {
            records.Add(Parse(number, line, static json => IsEnvelope(json)
                ? Check(CanonicalJson.Deserialize<ReplayEnvelope>(json)).Record
                : Check(CanonicalJson.Deserialize<ReplayRecord>(json))));
        }

        return records;
    }

    /// <summary>Reads every line as an envelope.</summary>
    /// <exception cref="InvalidDataException">A line is not valid JSON or is a bare record; the message names the line number.</exception>
    public static IReadOnlyList<ReplayEnvelope> ReadEnvelopes(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var envelopes = new List<ReplayEnvelope>();
        foreach (var (number, line) in Lines(reader))
        {
            envelopes.Add(Parse(number, line, static json => Check(CanonicalJson.Deserialize<ReplayEnvelope>(json))));
        }

        return envelopes;
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
        if ((object?)record.ClipHash is null || (object?)record.PromptId is null || (object?)record.GameStateHash is null
            || (object?)record.Versions is null || (object?)record.Verdict is null)
        {
            throw new JsonException("The entry is not a replay record (it needs clipHash, promptId, gameStateHash, versions and verdict).");
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

    private static bool IsEnvelope(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("record", out _);
    }
}
