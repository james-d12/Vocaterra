using System.Text.Json;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;

namespace Vocaterra.Nlp.Asr;

/// <summary>Converts the JSON that Vosk returns (single result or <c>alternatives</c> list) into an <see cref="AsrResult"/>.</summary>
public static class VoskResultParser
{
    /// <summary>
    /// Parses Vosk output. Alternatives are ordered by Vosk confidence descending then text ordinal, identical texts keep their best
    /// entry, and <see cref="AsrHypothesis.Score"/> is the softmax-normalized weight of the raw Vosk confidences (1.0 when there is a
    /// single hypothesis). The transcript and words come from the best alternative and <see cref="AsrResult.Confidence"/> is its score.
    /// Vosk omits per-word <c>conf</c> when alternatives are enabled; such words get a confidence of 1.0.
    /// </summary>
    /// <exception cref="InvalidDataException">The text is not Vosk result JSON.</exception>
    public static AsrResult Parse(string json, RecognitionMode mode, string modelHash)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(modelHash);
        try
        {
            using var document = JsonDocument.Parse(json);
            return Convert(document.RootElement, mode, modelHash);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException("Vosk returned a result that could not be parsed: " + ex.Message, ex);
        }
    }

    private static AsrResult Convert(JsonElement root, RecognitionMode mode, string modelHash)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("A Vosk result must be a JSON object.");
        }

        var candidates = new List<Candidate>();
        if (root.TryGetProperty("alternatives", out var alternatives) && alternatives.ValueKind == JsonValueKind.Array)
        {
            foreach (var alternative in alternatives.EnumerateArray())
            {
                candidates.Add(ReadCandidate(alternative));
            }
        }
        else
        {
            candidates.Add(ReadCandidate(root));
        }

        var ordered = candidates
            .OrderByDescending(static c => c.Confidence)
            .ThenBy(static c => c.Text, StringComparer.Ordinal)
            .ToList();
        var distinct = new List<Candidate>(ordered.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var candidate in ordered)
        {
            if (seen.Add(candidate.Text))
            {
                distinct.Add(candidate);
            }
        }

        if (distinct.Count == 0)
        {
            return new AsrResult(string.Empty, [new AsrHypothesis(string.Empty, 1.0)], [], 1.0, modelHash, mode);
        }

        var scores = distinct.Count == 1 ? [1.0] : Softmax.Normalize(distinct.Select(static c => c.Confidence).ToList());
        var nbest = new AsrHypothesis[distinct.Count];
        for (var i = 0; i < nbest.Length; i++)
        {
            nbest[i] = new AsrHypothesis(distinct[i].Text, scores[i]);
        }

        return new AsrResult(distinct[0].Text, nbest, distinct[0].Words, nbest[0].Score, modelHash, mode);
    }

    private static Candidate ReadCandidate(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("A Vosk hypothesis must be a JSON object.");
        }

        var text = element.TryGetProperty("text", out var textElement) && textElement.ValueKind == JsonValueKind.String
            ? textElement.GetString() ?? string.Empty
            : string.Empty;
        var confidence = element.TryGetProperty("confidence", out var confidenceElement) && confidenceElement.ValueKind == JsonValueKind.Number
            ? Finite(confidenceElement.GetDouble())
            : 0.0;
        var words = new List<AsrWord>();
        if (element.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.Array)
        {
            foreach (var word in result.EnumerateArray())
            {
                words.Add(ReadWord(word));
            }
        }

        return new Candidate(text, confidence, words);
    }

    private static AsrWord ReadWord(JsonElement word)
    {
        if (word.ValueKind != JsonValueKind.Object || !word.TryGetProperty("word", out var textElement) || textElement.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException("A Vosk word needs a 'word' string.");
        }

        return new AsrWord(
            textElement.GetString() ?? string.Empty,
            ReadNumber(word, "start", 0.0),
            ReadNumber(word, "end", 0.0),
            ReadNumber(word, "conf", 1.0));
    }

    private static double ReadNumber(JsonElement element, string name, double fallback)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? Finite(value.GetDouble()) : fallback;
    }

    private static double Finite(double value)
    {
        return double.IsFinite(value) ? value : throw new InvalidDataException("A Vosk number was not finite.");
    }

    private sealed record Candidate(string Text, double Confidence, IReadOnlyList<AsrWord> Words);
}
