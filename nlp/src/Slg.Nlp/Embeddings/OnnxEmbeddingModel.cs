using System.Security.Cryptography;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Slg.Core.Determinism;
using Slg.Core.Stages;

namespace Slg.Nlp.Embeddings;

/// <summary>
/// Sentence embedder running an ONNX export of multilingual-e5-small on the CPU: tokenize, run, mean-pool over the attention mask,
/// L2-normalize. One intra-op and one inter-op thread, sequential execution and basic graph optimizations keep it deterministic.
/// </summary>
/// <remarks>
/// The model directory holds <c>model.onnx</c> and <c>tokenizer.json</c>. E5 models expect a task prefix, so every text is embedded as
/// <c>"query: " + text</c> (symmetric matching uses the same prefix on both sides). Real-model use is D1: gate it on <c>SLG_MODELS_DIR</c>.
/// </remarks>
public sealed class OnnxEmbeddingModel : IEmbeddingModel, IDisposable
{
    /// <summary>The model file expected in the model directory.</summary>
    public const string ModelFileName = "model.onnx";

    /// <summary>The tokenizer file expected in the model directory.</summary>
    public const string TokenizerFileName = "tokenizer.json";

    /// <summary>The E5 task prefix prepended to every text.</summary>
    public const string DefaultPrefix = "query: ";

    private const string PreferredOutput = "last_hidden_state";

    private readonly InferenceSession _session;
    private readonly ITokenizer _tokenizer;
    private readonly string _prefix;
    private readonly string _outputName;
    private readonly bool _wantsTokenTypeIds;

    /// <summary>
    /// Loads <c>model.onnx</c> and <c>tokenizer.json</c> from a directory. <paramref name="modelHash"/> defaults to the combined
    /// SHA-256 of both files, which reads them completely; pass the manifest hash to avoid that.
    /// </summary>
    public OnnxEmbeddingModel(string modelDirectory, string? modelHash = null, string prefix = DefaultPrefix)
        : this(
            Path.Combine(modelDirectory, ModelFileName),
            UnigramTokenizer.FromFile(Path.Combine(modelDirectory, TokenizerFileName)),
            modelHash ?? HashFiles(Path.Combine(modelDirectory, ModelFileName), Path.Combine(modelDirectory, TokenizerFileName)),
            prefix)
    {
    }

    /// <summary>Loads the ONNX model with a caller-supplied tokenizer and an explicit model hash.</summary>
    public OnnxEmbeddingModel(string modelPath, ITokenizer tokenizer, string modelHash, string prefix = DefaultPrefix)
    {
        ArgumentException.ThrowIfNullOrEmpty(modelPath);
        ArgumentNullException.ThrowIfNull(tokenizer);
        ArgumentException.ThrowIfNullOrEmpty(modelHash);
        ArgumentNullException.ThrowIfNull(prefix);
        _tokenizer = tokenizer;
        _prefix = prefix;
        ModelHash = modelHash;
        using var options = new SessionOptions
        {
            IntraOpNumThreads = 1,
            InterOpNumThreads = 1,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_BASIC,
        };
        _session = new InferenceSession(modelPath, options);
        _wantsTokenTypeIds = _session.InputMetadata.ContainsKey("token_type_ids");
        _outputName = ChooseOutput(_session);
        Dimension = EmbedIds(_tokenizer.Encode(string.Empty)).Length;
    }

    /// <inheritdoc />
    public int Dimension { get; }

    /// <inheritdoc />
    public string ModelHash { get; }

    /// <inheritdoc />
    public float[] Embed(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return EmbedIds(_tokenizer.Encode(_prefix + text));
    }

    /// <summary>Embeds an already tokenized sequence (including its special tokens), bypassing the tokenizer and the prefix.</summary>
    public float[] EmbedIds(IReadOnlyList<int> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (ids.Count == 0)
        {
            throw new ArgumentException("The token id sequence must not be empty.", nameof(ids));
        }

        var length = ids.Count;
        var shape = new[] { 1, length };
        var inputIds = new DenseTensor<long>(shape);
        var attention = new DenseTensor<long>(shape);
        var mask = new long[length];
        for (var i = 0; i < length; i++)
        {
            inputIds[0, i] = ids[i];
            attention[0, i] = 1;
            mask[i] = 1;
        }

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
            NamedOnnxValue.CreateFromTensor("attention_mask", attention),
        };
        if (_wantsTokenTypeIds)
        {
            inputs.Add(NamedOnnxValue.CreateFromTensor("token_type_ids", new DenseTensor<long>(shape)));
        }

        using var results = _session.Run(inputs, [_outputName]);
        var output = results[0].AsTensor<float>();
        var values = output.ToArray();
        if (output.Rank == 3)
        {
            return MeanPool(values, output.Dimensions[1], output.Dimensions[2], mask);
        }

        if (output.Rank == 2)
        {
            EmbeddingMath.NormalizeInPlace(values);
            return values;
        }

        throw new InvalidOperationException($"Unexpected output rank {output.Rank} from '{_outputName}'.");
    }

    /// <summary>Releases the ONNX session.</summary>
    public void Dispose()
    {
        _session.Dispose();
    }

    /// <summary>
    /// Mean-pools a row-major <c>[sequence, dimension]</c> hidden state over the positions whose attention mask is non-zero (summing in
    /// ascending position order in double precision) and L2-normalizes the result.
    /// </summary>
    internal static float[] MeanPool(ReadOnlySpan<float> hidden, int sequenceLength, int dimension, ReadOnlySpan<long> attentionMask)
    {
        if (hidden.Length != sequenceLength * dimension || attentionMask.Length != sequenceLength)
        {
            throw new ArgumentException("Hidden state and attention mask sizes do not match the stated shape.");
        }

        var sums = new double[dimension];
        var counted = 0;
        for (var t = 0; t < sequenceLength; t++)
        {
            if (attentionMask[t] == 0)
            {
                continue;
            }

            counted++;
            var row = hidden.Slice(t * dimension, dimension);
            for (var d = 0; d < dimension; d++)
            {
                sums[d] += row[d];
            }
        }

        var pooled = new float[dimension];
        var divisor = Math.Max(counted, 1);
        for (var d = 0; d < dimension; d++)
        {
            pooled[d] = (float)(sums[d] / divisor);
        }

        EmbeddingMath.NormalizeInPlace(pooled);
        return pooled;
    }

    private static string ChooseOutput(InferenceSession session)
    {
        var names = session.OutputMetadata.Keys.ToArray();
        Array.Sort(names, StringComparer.Ordinal);
        if (names.Length == 0)
        {
            throw new InvalidOperationException("The ONNX model declares no outputs.");
        }

        return Array.IndexOf(names, PreferredOutput) >= 0 ? PreferredOutput : names[0];
    }

    private static string HashFiles(string modelPath, string tokenizerPath)
    {
        return DeterministicHash.Combine(HashFile(modelPath), HashFile(tokenizerPath));
    }

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }
}
