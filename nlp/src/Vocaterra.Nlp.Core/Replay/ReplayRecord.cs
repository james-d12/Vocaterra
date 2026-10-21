using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Nlp.Core.Replay;

/// <summary>Everything needed to replay one graded utterance: clip, prompt, host state, versions and the verdict produced.</summary>
public sealed record ReplayRecord(string ClipHash, string PromptId, string HostStateHash, VersionStamp Versions, Verdict Verdict)
{
    /// <summary>Serializes the record as canonical JSON.</summary>
    public string Serialize()
    {
        return CanonicalJson.Serialize(this);
    }

    /// <summary>Parses a record previously produced by <see cref="Serialize"/>.</summary>
    public static ReplayRecord Deserialize(string json)
    {
        return CanonicalJson.Deserialize<ReplayRecord>(json);
    }
}
