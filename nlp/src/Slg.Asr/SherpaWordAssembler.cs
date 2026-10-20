using System.Text;
using Slg.Core.Stages;

namespace Slg.Asr;

/// <summary>Joins sentencepiece tokens (word-initial pieces start with U+2581) and their timestamps into timed words.</summary>
public static class SherpaWordAssembler
{
    /// <summary>The sentencepiece word-boundary marker, LOWER ONE EIGHTH BLOCK (U+2581).</summary>
    public const char WordMarker = '▁';

    /// <summary>
    /// Builds words from tokens. A word starts at the first token with a non-empty piece after a marker (or at the start) and ends
    /// where its last token ends: start plus duration when durations are given, otherwise the start of the next word, otherwise
    /// <paramref name="clipSeconds"/>. Missing timestamps give zero times. Confidence is fixed at 1.0 because sherpa-onnx offline
    /// results carry no token probabilities.
    /// </summary>
    public static IReadOnlyList<AsrWord> Assemble(
        IReadOnlyList<string> tokens,
        IReadOnlyList<float> timestamps,
        IReadOnlyList<float> durations,
        double clipSeconds)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(timestamps);
        ArgumentNullException.ThrowIfNull(durations);

        var hasTimes = timestamps.Count == tokens.Count;
        var hasDurations = hasTimes && durations.Count == tokens.Count;
        var drafts = new List<Draft>();
        var text = new StringBuilder();
        var start = 0.0;
        var end = 0.0;
        var open = false;

        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i] ?? string.Empty;
            if (token.Length > 0 && token[0] == WordMarker && open)
            {
                drafts.Add(new Draft(text.ToString(), start, end, hasDurations));
                text.Clear();
                open = false;
            }

            var piece = token.Replace(WordMarker.ToString(), string.Empty, StringComparison.Ordinal);
            if (piece.Length == 0)
            {
                continue;
            }

            var tokenStart = hasTimes ? timestamps[i] : 0.0;
            if (!open)
            {
                start = tokenStart;
                open = true;
            }

            text.Append(piece);
            end = hasDurations ? tokenStart + durations[i] : tokenStart;
        }

        if (open)
        {
            drafts.Add(new Draft(text.ToString(), start, end, hasDurations));
        }

        var words = new List<AsrWord>(drafts.Count);
        for (var i = 0; i < drafts.Count; i++)
        {
            var draft = drafts[i];
            var wordEnd = draft.HasDurations
                ? draft.End
                : i + 1 < drafts.Count ? drafts[i + 1].Start : Math.Max(draft.Start, clipSeconds);
            words.Add(new AsrWord(draft.Text, draft.Start, Math.Max(draft.Start, wordEnd), 1.0));
        }

        return words;
    }

    private sealed record Draft(string Text, double Start, double End, bool HasDurations);
}
