using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Replay;

namespace Vocaterra.Nlp.Pipeline;

/// <summary>Appends replay entries to a text writer as JSON Lines in <see cref="CanonicalJson"/> form (one entry per LF-terminated line).</summary>
public sealed class ReplayLog
{
    private readonly TextWriter _writer;

    /// <summary>Creates a log over the writer; the caller owns and disposes the writer.</summary>
    public ReplayLog(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        _writer = writer;
    }

    /// <summary>Appends a bare core record, which can be read back with <see cref="ReplayReader.Read"/> but not replayed.</summary>
    public void Append(ReplayRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        WriteLine(CanonicalJson.Serialize(record));
    }

    /// <summary>Appends an envelope, which can be read back with <see cref="ReplayReader.ReadEnvelopes"/> and replayed.</summary>
    public void Append(ReplayEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        WriteLine(CanonicalJson.Serialize(envelope));
    }

    /// <summary>
    /// Appends a host-initiated turn entry. Readers that predate the entry kind do not understand it (see <c>docs/nlp/replay.md</c>), so a log that must stay
    /// readable by them should not contain host turns; <see cref="ReplayReader.ReadEntries"/> reads both kinds.
    /// </summary>
    public void Append(HostTurnEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        WriteLine(CanonicalJson.Serialize(envelope));
    }

    /// <summary>Flushes the underlying writer.</summary>
    public void Flush()
    {
        _writer.Flush();
    }

    private void WriteLine(string json)
    {
        _writer.Write(json);
        _writer.Write('\n');
    }
}
