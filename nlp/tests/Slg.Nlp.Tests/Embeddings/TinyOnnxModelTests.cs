namespace Slg.Nlp.Tests.Embeddings;

using System.Security.Cryptography;
using Slg.Core.Determinism;
using Slg.Nlp.Embeddings;

/// <summary>
/// Exercises the ONNX plumbing (session options, input names, output handling, mean pooling) with a hand-built 338-byte model that
/// only gathers rows of a 6 x 4 table, so no model download is needed.
/// </summary>
public sealed class TinyOnnxModelTests : IDisposable
{
    // ONNX ModelProto: Gather(embedding[6,4], input_ids) -> last_hidden_state[batch, seq, 4]; attention_mask is declared but unused.
    private const string PlainModelBase64 =
        "CAgSCHNsZy10ZXN0Or8CCjkKCWVtYmVkZGluZwoJaW5wdXRfaWRzEhFsYXN0X2hpZGRlbl9zdGF0ZRoGZ2F0aGVyIgZHYXRoZXISDXRpbnlfZW1iZWRkZXIqcwgGCAQQAUIJZW1iZWRkaW5nSmAAAIA/AAAAAAAAgD8AAACAAAAAQAAAAEAAAIA/AACAvwAAQEAAAIBAAACAPwAAAMAAAIBAAACAPwAAgD8AAEDAAACgQAAAQEAAAIA/AACAwAAAwEAAAAAAAACAPwAAoMBaIwoJaW5wdXRfaWRzEhYKFAgHEhAKBxIFYmF0Y2gKBRIDc2VxWigKDmF0dGVudGlvbl9tYXNrEhYKFAgHEhAKBxIFYmF0Y2gKBRIDc2VxYi8KEWxhc3RfaGlkZGVuX3N0YXRlEhoKGAgBEhQKBxIFYmF0Y2gKBRIDc2VxCgIIBEICEA0=";

    // The same graph with an additional declared token_type_ids input.
    private const string TokenTypeModelBase64 =
        "CAgSCHNsZy10ZXN0OukCCjkKCWVtYmVkZGluZwoJaW5wdXRfaWRzEhFsYXN0X2hpZGRlbl9zdGF0ZRoGZ2F0aGVyIgZHYXRoZXISDXRpbnlfZW1iZWRkZXIqcwgGCAQQAUIJZW1iZWRkaW5nSmAAAIA/AAAAAAAAgD8AAACAAAAAQAAAAEAAAIA/AACAvwAAQEAAAIBAAACAPwAAAMAAAIBAAACAPwAAgD8AAEDAAACgQAAAQEAAAIA/AACAwAAAwEAAAAAAAACAPwAAoMBaIwoJaW5wdXRfaWRzEhYKFAgHEhAKBxIFYmF0Y2gKBRIDc2VxWigKDmF0dGVudGlvbl9tYXNrEhYKFAgHEhAKBxIFYmF0Y2gKBRIDc2VxWigKDnRva2VuX3R5cGVfaWRzEhYKFAgHEhAKBxIFYmF0Y2gKBRIDc2VxYi8KEWxhc3RfaGlkZGVuX3N0YXRlEhoKGAgBEhQKBxIFYmF0Y2gKBRIDc2VxCgIIBEICEA0=";

    private const string TinyTokenizerJson = """
        {
          "added_tokens": [
            { "id": 0, "content": "<s>", "special": true },
            { "id": 1, "content": "<pad>", "special": true },
            { "id": 2, "content": "</s>", "special": true },
            { "id": 3, "content": "<unk>", "special": true }
          ],
          "model": {
            "type": "Unigram",
            "unk_id": 3,
            "vocab": [["<s>", 0.0], ["<pad>", 0.0], ["</s>", 0.0], ["<unk>", 0.0], ["▁hola", -4.0], ["▁mundo", -5.0]]
          }
        }
        """;

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "slg-tiny-onnx-" + Guid.NewGuid().ToString("N"));

    public TinyOnnxModelTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    private static float[] Row(int i) => [i + 1f, (i * 2) % 5, 1f, -i];

    private static float[] ExpectedMeanOf(params int[] rows)
    {
        var sums = new double[4];
        foreach (var row in rows)
        {
            var values = Row(row);
            for (var d = 0; d < 4; d++)
            {
                sums[d] += values[d];
            }
        }

        var norm = Math.Sqrt(sums.Sum(v => v * v));
        return sums.Select(v => (float)(v / norm)).ToArray();
    }

    private string WriteModel(string base64, string name = "model.onnx")
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllBytes(path, Convert.FromBase64String(base64));
        return path;
    }

    private static void AssertClose(float[] expected, float[] actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], actual[i], 5);
        }
    }

    [Fact]
    public void RunsTheSessionAndMeanPoolsTheHiddenStates()
    {
        var tokenizer = new RecordingTokenizer(text => text.Length % 2 == 0 ? [0, 2, 3] : [1, 5]);
        using var model = new OnnxEmbeddingModel(WriteModel(PlainModelBase64), tokenizer, "tiny-hash", prefix: string.Empty);

        Assert.Equal(4, model.Dimension);
        Assert.Equal("tiny-hash", model.ModelHash);
        AssertClose(ExpectedMeanOf(0, 2, 3), model.Embed("ab"));
        AssertClose(ExpectedMeanOf(1, 5), model.Embed("abc"));
        AssertClose(ExpectedMeanOf(5), model.EmbedIds([5]));
    }

    [Fact]
    public void OutputIsDeterministicAndUnitLength()
    {
        var tokenizer = new RecordingTokenizer(_ => [0, 4, 5, 2]);
        using var model = new OnnxEmbeddingModel(WriteModel(PlainModelBase64), tokenizer, "tiny-hash");

        var first = model.Embed("hola mundo");
        var second = model.Embed("hola mundo");

        Assert.Equal(first, second);
        Assert.Equal(1.0, first.Sum(v => (double)v * v), 5);
    }

    [Fact]
    public void PrefixIsPrependedToEveryText()
    {
        var tokenizer = new RecordingTokenizer(_ => [0, 2]);
        using var model = new OnnxEmbeddingModel(WriteModel(PlainModelBase64), tokenizer, "tiny-hash");

        model.Embed("hola");

        Assert.Equal("query: hola", tokenizer.Seen[^1]);
    }

    [Fact]
    public void ModelsThatDeclareTokenTypeIdsAreFedThem()
    {
        var tokenizer = new RecordingTokenizer(_ => [0, 4, 2]);
        using var model = new OnnxEmbeddingModel(WriteModel(TokenTypeModelBase64), tokenizer, "tiny-hash", prefix: string.Empty);

        AssertClose(ExpectedMeanOf(0, 4, 2), model.Embed("x"));
    }

    [Fact]
    public void LoadsFromADirectoryAndHashesBothFiles()
    {
        var modelPath = WriteModel(PlainModelBase64);
        var tokenizerPath = Path.Combine(_directory, "tokenizer.json");
        File.WriteAllText(tokenizerPath, TinyTokenizerJson);

        using var model = new OnnxEmbeddingModel(_directory);

        var expectedHash = DeterministicHash.Combine(
            Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(modelPath))),
            Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(tokenizerPath))));
        Assert.Equal(expectedHash, model.ModelHash);
        Assert.Equal(4, model.Dimension);
        // "query: hola" -> <s> unk(▁query:) ▁hola </s>
        AssertClose(ExpectedMeanOf(0, 3, 4, 2), model.Embed("hola"));

        using var pinned = new OnnxEmbeddingModel(_directory, "manifest-hash");
        Assert.Equal("manifest-hash", pinned.ModelHash);
    }

    [Fact]
    public void RejectsEmptyIdSequences()
    {
        using var model = new OnnxEmbeddingModel(WriteModel(PlainModelBase64), new RecordingTokenizer(_ => [0, 2]), "tiny-hash");

        Assert.Throws<ArgumentException>(() => model.EmbedIds([]));
    }

    private sealed class RecordingTokenizer : ITokenizer
    {
        private readonly Func<string, int[]> _encode;

        public RecordingTokenizer(Func<string, int[]> encode)
        {
            _encode = encode;
        }

        public List<string> Seen { get; } = [];

        public int[] Encode(string text)
        {
            Seen.Add(text);
            return _encode(text);
        }
    }
}
