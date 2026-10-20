using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Slg.Nlp.Embeddings;

/// <summary>
/// A SentencePiece Unigram tokenizer read from a Hugging Face <c>tokenizer.json</c>, as used by XLM-RoBERTa based models such as
/// multilingual-e5-small: NFKC, whitespace split, Metaspace prefix (<c>▁</c>), Viterbi over piece log-probabilities, then
/// <c>&lt;s&gt; ... &lt;/s&gt;</c>.
/// </summary>
/// <remarks>
/// Known approximation: the SentencePiece normalization table (<c>precompiled_charsmap</c>) is not interpreted; .NET NFKC with
/// control characters and whitespace mapped to spaces stands in for it, which can differ for a few exotic characters but not for
/// ordinary Spanish text. Ties between equal-score segmentations go to the one found first (earliest start, shortest piece), like the
/// reference implementation, and consecutive unknown characters fuse into one unknown token. TODO(M9/M13): verify against reference
/// ids once the model files are fetched.
/// </remarks>
public sealed class UnigramTokenizer : ITokenizer
{
    /// <summary>The Metaspace marker that replaces a space and starts every word.</summary>
    public const char WordMarker = '▁';

    /// <summary>The default longest sequence, in tokens including the special ones.</summary>
    public const int DefaultMaxLength = 512;

    private const double UnknownPenalty = 10.0;

    private readonly string[] _pieces;
    private readonly Dictionary<string, int>.AlternateLookup<ReadOnlySpan<char>> _lookup;
    private readonly double[] _scores;
    private readonly int _maxPieceChars;
    private readonly int _unknownId;
    private readonly double _unknownScore;
    private readonly int _beginId;
    private readonly int _endId;
    private readonly int _maxLength;

    private UnigramTokenizer(
        string[] pieces,
        Dictionary<string, int> pieceIds,
        double[] scores,
        int maxPieceChars,
        int unknownId,
        double unknownScore,
        int beginId,
        int endId,
        int maxLength)
    {
        _pieces = pieces;
        _lookup = pieceIds.GetAlternateLookup<ReadOnlySpan<char>>();
        _scores = scores;
        _maxPieceChars = maxPieceChars;
        _unknownId = unknownId;
        _unknownScore = unknownScore;
        _beginId = beginId;
        _endId = endId;
        _maxLength = maxLength;
    }

    /// <summary>Number of pieces in the vocabulary (including special tokens).</summary>
    public int VocabularySize => _pieces.Length;

    /// <summary>Reads a tokenizer from a <c>tokenizer.json</c> file.</summary>
    public static UnigramTokenizer FromFile(string path, int maxLength = DefaultMaxLength)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return FromJson(File.ReadAllText(path, Encoding.UTF8), maxLength);
    }

    /// <summary>Reads a tokenizer from <c>tokenizer.json</c> text; throws <see cref="NotSupportedException"/> for non-Unigram models.</summary>
    public static UnigramTokenizer FromJson(string json, int maxLength = DefaultMaxLength)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLength, 3);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("model", out var model) || model.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException("tokenizer.json has no 'model' object.");
        }

        var type = model.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : null;
        if (!string.Equals(type, "Unigram", StringComparison.Ordinal))
        {
            throw new NotSupportedException($"Only Unigram tokenizers are supported; tokenizer.json declares '{type}'.");
        }

        var specials = SpecialTokens(root);
        var vocab = model.GetProperty("vocab");
        var scores = new double[vocab.GetArrayLength()];
        var pieces = new string[scores.Length];
        var ids = new Dictionary<string, int>(StringComparer.Ordinal);
        var maxChars = 1;
        var minScore = double.PositiveInfinity;
        var index = 0;
        foreach (var item in vocab.EnumerateArray())
        {
            var piece = item[0].GetString() ?? throw new FormatException($"Vocabulary entry {index} has no piece.");
            var score = item[1].GetDouble();
            scores[index] = score;
            pieces[index] = piece;
            if (!specials.ContainsKey(piece))
            {
                ids.TryAdd(piece, index);
                maxChars = Math.Max(maxChars, piece.Length);
                minScore = Math.Min(minScore, score);
            }

            index++;
        }

        var unknownId = model.TryGetProperty("unk_id", out var unk) && unk.ValueKind == JsonValueKind.Number
            ? unk.GetInt32()
            : IdOf("<unk>", specials, ids);
        if (unknownId < 0 || unknownId >= scores.Length)
        {
            throw new FormatException("tokenizer.json declares no usable unknown token.");
        }

        var beginId = IdOf("<s>", specials, ids);
        var endId = IdOf("</s>", specials, ids);
        if (beginId < 0 || endId < 0)
        {
            throw new FormatException("tokenizer.json must define the '<s>' and '</s>' tokens.");
        }

        if (double.IsPositiveInfinity(minScore))
        {
            minScore = 0.0;
        }

        return new UnigramTokenizer(pieces, ids, scores, maxChars, unknownId, minScore - UnknownPenalty, beginId, endId, maxLength);
    }

    /// <inheritdoc />
    public int[] Encode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var body = new List<int>();
        foreach (var word in Words(text))
        {
            Segment(WordMarker + word, body);
            if (body.Count >= _maxLength - 2)
            {
                break;
            }
        }

        if (body.Count > _maxLength - 2)
        {
            body.RemoveRange(_maxLength - 2, body.Count - (_maxLength - 2));
        }

        var result = new int[body.Count + 2];
        result[0] = _beginId;
        body.CopyTo(result, 1);
        result[^1] = _endId;
        return result;
    }

    /// <summary>Returns the piece strings for an id sequence, special tokens included; for diagnostics and tests.</summary>
    public IReadOnlyList<string> PiecesOf(IReadOnlyList<int> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        return ids.Select(id => (uint)id < (uint)_pieces.Length ? _pieces[id] : "#" + id.ToString(CultureInfo.InvariantCulture)).ToArray();
    }

    private static int IdOf(string piece, Dictionary<string, int> specials, Dictionary<string, int> ids)
    {
        return specials.TryGetValue(piece, out var special) ? special : ids.TryGetValue(piece, out var id) ? id : -1;
    }

    private static Dictionary<string, int> SpecialTokens(JsonElement root)
    {
        var specials = new Dictionary<string, int>(StringComparer.Ordinal);
        if (root.TryGetProperty("added_tokens", out var added) && added.ValueKind == JsonValueKind.Array)
        {
            foreach (var token in added.EnumerateArray())
            {
                var special = token.TryGetProperty("special", out var flag) && flag.ValueKind == JsonValueKind.True;
                if (special && token.TryGetProperty("content", out var content) && token.TryGetProperty("id", out var id))
                {
                    specials.TryAdd(content.GetString() ?? string.Empty, id.GetInt32());
                }
            }
        }

        return specials;
    }

    private static List<string> Words(string text)
    {
        var folded = text.Normalize(NormalizationForm.FormKC);
        var words = new List<string>();
        var builder = new StringBuilder();
        foreach (var c in folded)
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c))
            {
                if (builder.Length > 0)
                {
                    words.Add(builder.ToString());
                    builder.Clear();
                }

                continue;
            }

            builder.Append(c);
        }

        if (builder.Length > 0)
        {
            words.Add(builder.ToString());
        }

        return words;
    }

    private static int[] ScalarBoundaries(string word)
    {
        var bounds = new List<int>(word.Length + 1);
        for (var i = 0; i < word.Length; i++)
        {
            bounds.Add(i);
            if (char.IsHighSurrogate(word[i]) && i + 1 < word.Length && char.IsLowSurrogate(word[i + 1]))
            {
                i++;
            }
        }

        bounds.Add(word.Length);
        return [.. bounds];
    }

    private void Segment(string word, List<int> output)
    {
        var bounds = ScalarBoundaries(word);
        var count = bounds.Length - 1;
        var best = new double[count + 1];
        var from = new int[count + 1];
        var pieceId = new int[count + 1];
        Array.Fill(best, double.NegativeInfinity);
        best[0] = 0.0;
        for (var start = 0; start < count; start++)
        {
            var hasSingle = false;
            for (var end = start + 1; end <= count && bounds[end] - bounds[start] <= _maxPieceChars; end++)
            {
                if (!_lookup.TryGetValue(word.AsSpan(bounds[start], bounds[end] - bounds[start]), out var id))
                {
                    continue;
                }

                if (end == start + 1)
                {
                    hasSingle = true;
                }

                var candidate = best[start] + _scores[id];
                if (candidate > best[end])
                {
                    best[end] = candidate;
                    from[end] = start;
                    pieceId[end] = id;
                }
            }

            if (!hasSingle)
            {
                var candidate = best[start] + _unknownScore;
                if (candidate > best[start + 1])
                {
                    best[start + 1] = candidate;
                    from[start + 1] = start;
                    pieceId[start + 1] = _unknownId;
                }
            }
        }

        var reversed = new List<int>();
        for (var end = count; end > 0; end = from[end])
        {
            var id = pieceId[end];
            if (id == _unknownId && reversed.Count > 0 && reversed[^1] == _unknownId)
            {
                continue;
            }

            reversed.Add(id);
        }

        reversed.Reverse();
        output.AddRange(reversed);
    }
}
